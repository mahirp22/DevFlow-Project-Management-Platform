using DevFlow.Application.Identity;
using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Projects;

// All use cases authorize before reading or changing tenant data.
public sealed class AccessGuard(DevFlowDbContext db, IResourceAccess access)
{
    public async Task<UserProfile> User(string subject, CancellationToken ct) =>
        await db.Users.SingleOrDefaultAsync(x => x.CognitoSubject == subject, ct)
            ?? throw new RequestFailure(409, "Set up your profile on the Account page first.");

    public async Task Workspace(string subject, Guid id, bool manage, CancellationToken ct)
    {
        var permission = await access.WorkspaceAsync(subject, id, ct)
            ?? throw new RequestFailure(404, "Workspace not found.");
        if (manage && !permission.CanManage) throw new RequestFailure(403, "Workspace administration requires an owner or admin.");
    }
    public async Task<Project> Project(string subject, Guid workspaceId, Guid projectId, string permission, CancellationToken ct)
    {
        var permissions = await access.ProjectAsync(subject, workspaceId, projectId, ct)
            ?? throw new RequestFailure(404, "Project not found.");
        if ((permission == "write" && !permissions.CanWrite) || (permission == "manage" && !permissions.CanManage))
            throw new RequestFailure(403, "You do not have permission for this operation.");
        var project = await db.Projects.SingleAsync(x => x.Id == projectId && !x.IsDeleted, ct);
        if (permission == "write" && project.IsArchived)
            throw new RequestFailure(409, "This project is archived. Restore it before making changes.");
        return project;
    }
    public Task<TaskItem> Task(Guid projectId, Guid taskId, CancellationToken ct) =>
        FindTask(projectId, taskId, ct);
    private async Task<TaskItem> FindTask(Guid projectId, Guid taskId, CancellationToken ct) =>
        await db.Tasks.SingleOrDefaultAsync(x => x.Id == taskId && x.ProjectId == projectId && !x.IsDeleted, ct)
            ?? throw new RequestFailure(404, "Task not found.");
    public async Task Assignee(Guid projectId, Guid? userId, CancellationToken ct)
    {
        if (userId is not null && !await db.ProjectMemberships.AnyAsync(x => x.ProjectId == projectId && x.UserId == userId, ct))
            throw new RequestFailure(400, "The assignee must be a member of this project.");
    }
    public static ProjectDto Map(Project p) => new(p.Id, p.WorkspaceId, p.Key, p.Name, p.Description, p.IsArchived);
}
