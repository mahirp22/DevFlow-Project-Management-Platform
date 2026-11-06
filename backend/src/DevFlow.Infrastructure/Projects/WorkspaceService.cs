using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Projects;

public sealed class WorkspaceService(DevFlowDbContext db, AccessGuard guard) : IWorkspaceService
{
    public async Task<IReadOnlyList<WorkspaceDto>> List(string subject, CancellationToken ct)
    {
        return await (from membership in db.WorkspaceMemberships
            join user in db.Users on membership.UserId equals user.Id
            join workspace in db.Workspaces on membership.WorkspaceId equals workspace.Id
            where user.CognitoSubject == subject
            orderby workspace.Name, workspace.Id
            select new WorkspaceDto(workspace.Id, workspace.Name, membership.Role)).ToListAsync(ct);
    }
    public async Task<WorkspaceDto> Create(string subject, WorkspaceRequest request, CancellationToken ct)
    {
        var user = await guard.User(subject, ct);
        var workspace = new Workspace(request.Name, user.Id);
        db.AddRange(workspace, new WorkspaceMembership(workspace.Id, user.Id, WorkspaceRole.Owner));
        await db.SaveChangesAsync(ct);
        return new(workspace.Id, workspace.Name, WorkspaceRole.Owner);
    }
    public async Task Rename(string subject, Guid workspaceId, WorkspaceRequest request, CancellationToken ct)
    {
        await guard.Workspace(subject, workspaceId, true, ct);
        var workspace = await db.Workspaces.SingleAsync(x => x.Id == workspaceId, ct);
        workspace.Rename(request.Name);
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<MemberDto>> Members(string subject, Guid workspaceId, CancellationToken ct)
    {
        await guard.Workspace(subject, workspaceId, false, ct);
        return await (from member in db.WorkspaceMemberships join user in db.Users on member.UserId equals user.Id
            where member.WorkspaceId == workspaceId orderby user.DisplayName
            select new MemberDto(user.Id, user.DisplayName, user.Email, member.Role.ToString())).ToListAsync(ct);
    }
    public async Task ChangeRole(string subject, Guid workspaceId, Guid userId, WorkspaceRoleRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize role changes so two owners cannot concurrently remove the last owner.
        await db.Workspaces.FromSqlInterpolated($"SELECT * FROM \"Workspaces\" WHERE \"Id\" = {workspaceId} FOR UPDATE").ToListAsync(ct);
        await guard.Workspace(subject, workspaceId, true, ct);
        var caller = await guard.User(subject, ct);
        var callerRole = await db.WorkspaceMemberships.SingleAsync(x => x.WorkspaceId == workspaceId && x.UserId == caller.Id, ct);
        var target = await db.WorkspaceMemberships.SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.UserId == userId, ct)
            ?? throw new RequestFailure(404, "Member not found.");
        if (callerRole.Role != WorkspaceRole.Owner)
            throw new RequestFailure(403, "Only workspace owners can change workspace roles.");
        if (target.Role == WorkspaceRole.Owner && request.Role != WorkspaceRole.Owner
            && await db.WorkspaceMemberships.CountAsync(x => x.WorkspaceId == workspaceId && x.Role == WorkspaceRole.Owner, ct) <= 1)
            throw new RequestFailure(409, "A workspace must retain at least one owner.");
        target.ChangeRole(request.Role);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
