using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Projects;

public sealed class ProjectService(DevFlowDbContext db, AccessGuard guard) : IProjectService
{
    public async Task<IReadOnlyList<ProjectDto>> List(string subject, Guid workspaceId, CancellationToken ct)
    {
        await guard.Workspace(subject, workspaceId, false, ct);
        var user = await guard.User(subject, ct);
        var role = await db.WorkspaceMemberships.Where(x => x.WorkspaceId == workspaceId && x.UserId == user.Id).Select(x => x.Role).SingleAsync(ct);
        var admin = role is WorkspaceRole.Owner or WorkspaceRole.Admin;
        return await db.Projects.Where(x => x.WorkspaceId == workspaceId && !x.IsDeleted
            && (admin || db.ProjectMemberships.Any(m => m.ProjectId == x.Id && m.UserId == user.Id)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ProjectDto(x.Id, x.WorkspaceId, x.Key, x.Name, x.Description, x.IsArchived)).ToListAsync(ct);
    }
    public async Task<ProjectDto> Get(string subject, Guid workspaceId, Guid projectId, CancellationToken ct) =>
        AccessGuard.Map(await guard.Project(subject, workspaceId, projectId, "read", ct));
    public async Task<ProjectDto> Create(string subject, Guid workspaceId, ProjectRequest request, CancellationToken ct)
    {
        await guard.Workspace(subject, workspaceId, true, ct);
        var user = await guard.User(subject, ct);
        var project = new Project(workspaceId, request.Key, request.Name);
        project.Update(request.Name, request.Description, request.IsArchived);
        db.AddRange(project, new ProjectMembership(workspaceId, project.Id, user.Id, ProjectRole.Manager));
        await db.SaveChangesAsync(ct);
        return AccessGuard.Map(project);
    }
    public async Task<ProjectDto> Update(string subject, Guid workspaceId, Guid projectId, ProjectRequest request, CancellationToken ct)
    {
        var project = await guard.Project(subject, workspaceId, projectId, "manage", ct);
        if (!string.Equals(project.Key, request.Key, StringComparison.OrdinalIgnoreCase))
            throw new RequestFailure(400, "Project keys cannot be changed after creation.");
        project.Update(request.Name, request.Description, request.IsArchived);
        await db.SaveChangesAsync(ct);
        return AccessGuard.Map(project);
    }
    public async Task Delete(string subject, Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        var project = await guard.Project(subject, workspaceId, projectId, "manage", ct);
        project.Delete();
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<MemberDto>> Members(string subject, Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct);
        return await (from member in db.ProjectMemberships join user in db.Users on member.UserId equals user.Id
            where member.ProjectId == projectId orderby user.DisplayName
            select new MemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString())).ToListAsync(ct);
    }
    public async Task ChangeRole(string subject, Guid workspaceId, Guid projectId, Guid userId, ProjectRoleRequest request, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "manage", ct);
        var member = await db.ProjectMemberships.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, ct)
            ?? throw new RequestFailure(404, "Member not found.");
        member.ChangeRole(request.Role);
        await db.SaveChangesAsync(ct);
    }
    public async Task RemoveMember(string subject, Guid workspaceId, Guid projectId, Guid userId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "manage", ct);
        var member = await db.ProjectMemberships.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, ct)
            ?? throw new RequestFailure(404, "Member not found.");
        // Reassign active tasks first; deleted tasks have already cleared their assignee.
        if (await db.Tasks.AnyAsync(x => x.ProjectId == projectId && x.AssigneeUserId == userId, ct))
            throw new RequestFailure(409, "Reassign this member's tasks before removing them.");
        db.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}
