using System.Security.Cryptography;
using System.Text;
using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Infrastructure.Projects;

public sealed class BoardService(DevFlowDbContext db, AccessGuard guard) : IBoardService
{
    private const int MaximumCards = 500;
    private async Task<List<TaskItem>> Tasks(Guid projectId, CancellationToken ct)
    {
        var rows = await db.Tasks.Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .OrderBy(x => x.Status).ThenBy(x => x.Position).ThenBy(x => x.Number).Take(MaximumCards + 1).ToListAsync(ct);
        if (rows.Count > MaximumCards) throw new RequestFailure(409, "This board exceeds 500 tasks. Use the paginated task list.");
        return rows;
    }
    private static string Revision(IEnumerable<TaskItem> tasks) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join('|', tasks.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.Version}")))));
    private async Task<BoardDto> Map(List<TaskItem> rows, CancellationToken ct)
    {
        // Read names once, keeping the task snapshot and its revision consistent.
        var users = await db.Users.Where(x => rows.Select(t => t.AssigneeUserId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var projectId = rows.FirstOrDefault()?.ProjectId;
        var project = projectId is null ? null : await db.Projects.FindAsync([projectId.Value], ct);
        return new(Revision(rows), rows.Select(t => new BoardCard(new TaskDto(t.Id, t.ProjectId, project!.WorkspaceId,
            project.Key, t.Number, t.Title, t.Description, t.Status, t.Priority, t.DueDate, t.AssigneeUserId,
            t.AssigneeUserId is { } id ? users.GetValueOrDefault(id) : null, t.Version, t.UpdatedAt), t.Position)).ToList());
    }
    public async Task<BoardDto> Get(string subject, Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        return await Map(await Tasks(projectId, ct), ct);
    }
    public async Task<BoardDto> Move(string subject, Guid workspaceId, Guid projectId, Guid taskId, MoveTaskRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE").ToListAsync(ct);
        await guard.Project(subject, workspaceId, projectId, "write", ct);
        var user = await guard.User(subject, ct);
        var rows = await Tasks(projectId, ct);
        if (!string.Equals(Revision(rows), request.Revision, StringComparison.OrdinalIgnoreCase))
            throw new RequestFailure(409, "The board changed. Reload it and try your move again.");
        var task = rows.SingleOrDefault(x => x.Id == taskId) ?? throw new RequestFailure(404, "Task not found.");
        var target = rows.Where(x => x.Status == request.Status && x.Id != taskId).ToList();
        var index = request.BeforeTaskId is null ? target.Count : target.FindIndex(x => x.Id == request.BeforeTaskId);
        if (index < 0) throw new RequestFailure(400, "The destination task must belong to the selected column.");
        var previous = task.Status;
        target.Insert(index, task);
        for (var i = 0; i < target.Count; i++)
            if (target[i].Position != i + 1 || target[i].Status != request.Status)
                target[i].Move(request.Status, i + 1);
        db.Add(new TaskActivity(task.Id, user.Id, previous == request.Status
            ? "Reordered task on the board." : $"Moved task from {previous} to {request.Status}."));
        await db.SaveChangesAsync(ct);
        var result = await Map(rows.OrderBy(x => x.Status).ThenBy(x => x.Position).ThenBy(x => x.Number).ToList(), ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
