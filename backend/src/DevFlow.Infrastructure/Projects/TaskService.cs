using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevFlow.Domain.Enums.TaskStatus;
namespace DevFlow.Infrastructure.Projects;

public sealed class TaskService(DevFlowDbContext db, AccessGuard guard) : ITaskService
{
    internal IQueryable<TaskDto> Rows(IQueryable<TaskItem> tasks) =>
        from task in tasks join project in db.Projects on task.ProjectId equals project.Id
        join user in db.Users on task.AssigneeUserId equals user.Id into assignees
        from assignee in assignees.DefaultIfEmpty()
        select new TaskDto(task.Id, project.Id, project.WorkspaceId, project.Key, task.Number, task.Title,
            task.Description, task.Status, task.Priority, task.DueDate, task.AssigneeUserId,
            assignee == null ? null : assignee.DisplayName, task.Version, task.UpdatedAt);

    private static IQueryable<TaskItem> Filter(IQueryable<TaskItem> tasks, TaskFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search)) tasks = tasks.Where(x => x.Title.ToLower().Contains(filter.Search.Trim().ToLower()));
        if (filter.Status is not null) tasks = tasks.Where(x => x.Status == filter.Status);
        if (filter.Priority is not null) tasks = tasks.Where(x => x.Priority == filter.Priority);
        if (filter.AssigneeUserId is not null) tasks = tasks.Where(x => x.AssigneeUserId == filter.AssigneeUserId);
        if (filter.Overdue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            tasks = tasks.Where(x => x.DueDate < today && x.Status != TaskStatus.Completed);
        }
        return tasks;
    }
    private async Task<PageResult<TaskDto>> Page(IQueryable<TaskItem> tasks, TaskFilter filter, CancellationToken ct)
    {
        tasks = Filter(tasks, filter);
        var total = await tasks.CountAsync(ct);
        var rows = await Rows(tasks.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)).ToListAsync(ct);
        return new(rows, total, filter.Page, filter.PageSize);
    }
    public async Task<PageResult<TaskDto>> List(string subject, Guid workspaceId, Guid projectId, TaskFilter filter, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        return await Page(db.Tasks.AsNoTracking().Where(x => x.ProjectId == projectId && !x.IsDeleted), filter, ct);
    }
    public async Task<PageResult<TaskDto>> Mine(string subject, TaskFilter filter, CancellationToken ct)
    {
        var user = await guard.User(subject, ct);
        var tasks = db.Tasks.AsNoTracking().Where(t => !t.IsDeleted && t.AssigneeUserId == user.Id
            && db.Projects.Any(p => p.Id == t.ProjectId && !p.IsDeleted && !p.IsArchived)
            && db.ProjectMemberships.Any(m => m.ProjectId == t.ProjectId && m.UserId == user.Id));
        return await Page(tasks, filter, ct);
    }
    public async Task<TaskDto> Get(string subject, Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        return await Rows(db.Tasks.AsNoTracking().Where(x => x.Id == taskId && x.ProjectId == projectId && !x.IsDeleted))
            .SingleOrDefaultAsync(ct) ?? throw new RequestFailure(404, "Task not found.");
    }
    public async Task<TaskDto> Create(string subject, Guid workspaceId, Guid projectId, TaskRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock just the project row: numbers remain unique and are not reused after deletion.
        await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE").ToListAsync(ct);
        var project = await guard.Project(subject, workspaceId, projectId, "write", ct);
        var user = await guard.User(subject, ct);
        await guard.Assignee(projectId, request.AssigneeUserId, ct);
        var task = new TaskItem(projectId, project.AllocateTaskNumber(), request.Title, user.Id, request.Priority, request.DueDate, request.AssigneeUserId);
        task.Update(request.Title, request.Description, request.Status, request.Priority, request.DueDate, request.AssigneeUserId);
        task.Move(request.Status, (await db.Tasks.Where(x => x.ProjectId == projectId && !x.IsDeleted && x.Status == request.Status).MaxAsync(x => (long?)x.Position, ct) ?? 0) + 1);
        db.AddRange(task, new TaskActivity(task.Id, user.Id, "Created task."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(subject, workspaceId, projectId, task.Id, ct);
    }
    public async Task<TaskDto> Update(string subject, Guid workspaceId, Guid projectId, Guid taskId, TaskRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE").ToListAsync(ct);
        await guard.Project(subject, workspaceId, projectId, "write", ct);
        var user = await guard.User(subject, ct);
        var task = await guard.Task(projectId, taskId, ct);
        RequireVersion(task, request.Version);
        await guard.Assignee(projectId, request.AssigneeUserId, ct);
        var changes = new List<string>();
        if (task.Title != request.Title.Trim()) changes.Add("title");
        if (task.Description != request.Description) changes.Add("description");
        if (task.Status != request.Status) changes.Add($"status: {task.Status} → {request.Status}");
        if (task.Priority != request.Priority) changes.Add($"priority: {task.Priority} → {request.Priority}");
        if (task.DueDate != request.DueDate) changes.Add($"deadline: {task.DueDate?.ToString("yyyy-MM-dd") ?? "none"} → {request.DueDate?.ToString("yyyy-MM-dd") ?? "none"}");
        if (task.Status != request.Status)
            task.Move(request.Status, (await db.Tasks.Where(x => x.ProjectId == projectId && !x.IsDeleted && x.Status == request.Status).MaxAsync(x => (long?)x.Position, ct) ?? 0) + 1);
        if (task.AssigneeUserId != request.AssigneeUserId) changes.Add("assignee");
        task.Update(request.Title, request.Description, request.Status, request.Priority, request.DueDate, request.AssigneeUserId);
        if (changes.Count > 0) db.Add(new TaskActivity(taskId, user.Id, "Updated " + string.Join("; ", changes) + "."));
        // EF saves the task and its history atomically; xmin detects intervening writers.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await Get(subject, workspaceId, projectId, taskId, ct);
    }
    public async Task Delete(string subject, Guid workspaceId, Guid projectId, Guid taskId, uint version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE").ToListAsync(ct);
        await guard.Project(subject, workspaceId, projectId, "write", ct);
        var user = await guard.User(subject, ct);
        var task = await guard.Task(projectId, taskId, ct);
        RequireVersion(task, version);
        task.Delete();
        db.Add(new TaskActivity(taskId, user.Id, "Deleted task."));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private static void RequireVersion(TaskItem task, uint? version)
    {
        if (version is null or 0) throw new RequestFailure(400, "The current task version is required.");
        if (task.Version != version) throw new RequestFailure(409, "This task changed. Reload it before saving again.");
    }
    public async Task<PageResult<CommentDto>> Comments(string subject, Guid workspaceId, Guid projectId, Guid taskId, int page, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        await guard.Task(projectId, taskId, ct);
        var query = from comment in db.Comments join user in db.Users on comment.AuthorUserId equals user.Id
            where comment.TaskId == taskId orderby comment.CreatedAt descending, comment.Id
            select new CommentDto(comment.Id, comment.Body, user.DisplayName, comment.CreatedAt);
        return new(await query.Skip((page - 1) * 25).Take(25).ToListAsync(ct), await query.CountAsync(ct), page, 25);
    }
    public async Task<CommentDto> Comment(string subject, Guid workspaceId, Guid projectId, Guid taskId, CommentRequest request, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "write", ct);
        await guard.Task(projectId, taskId, ct);
        var user = await guard.User(subject, ct);
        var comment = new TaskComment(taskId, user.Id, request.Body);
        db.AddRange(comment, new TaskActivity(taskId, user.Id, "Added a comment."));
        await db.SaveChangesAsync(ct);
        return new(comment.Id, comment.Body, user.DisplayName, comment.CreatedAt);
    }
    public async Task<PageResult<ActivityDto>> Activity(string subject, Guid workspaceId, Guid projectId, Guid taskId, int page, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        await guard.Task(projectId, taskId, ct);
        var query = from activity in db.Activities join user in db.Users on activity.ActorUserId equals user.Id
            where activity.TaskId == taskId orderby activity.CreatedAt descending, activity.Id
            select new ActivityDto(activity.Id, activity.Summary, user.DisplayName, activity.CreatedAt);
        return new(await query.Skip((page - 1) * 25).Take(25).ToListAsync(ct), await query.CountAsync(ct), page, 25);
    }
    public async Task<AnalyticsDto> Analytics(string subject, Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        var tasks = db.Tasks.AsNoTracking().Where(x => x.ProjectId == projectId && !x.IsDeleted);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await tasks.GroupBy(x => x.Status).Select(g => new StatusCount(g.Key, g.Count())).ToListAsync(ct);
        var workload = await (from task in tasks.Where(x => x.Status != TaskStatus.Completed)
            join user in db.Users on task.AssigneeUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            group task by new { task.AssigneeUserId, Name = user == null ? "Unassigned" : user.DisplayName } into g
            select new WorkloadCount(g.Key.Name, g.Count())).ToListAsync(ct);
        return new(counts.Sum(x => x.Count), await tasks.CountAsync(x => x.DueDate < today && x.Status != TaskStatus.Completed, ct),
            counts.Where(x => x.Status == TaskStatus.Completed).Sum(x => x.Count), counts, workload);
    }
}
