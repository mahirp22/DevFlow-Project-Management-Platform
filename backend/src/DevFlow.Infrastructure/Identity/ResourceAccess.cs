using DevFlow.Application.Identity;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevFlow.Infrastructure.Identity;

public sealed class ResourceAccessService(DevFlowDbContext db) : IResourceAccess
{
    public async Task<ResourceAccess?> WorkspaceAsync(string subject, Guid workspaceId, CancellationToken cancellationToken)
    {
        var role = await (from membership in db.WorkspaceMemberships
            join user in db.Users on membership.UserId equals user.Id
            where user.CognitoSubject == subject && membership.WorkspaceId == workspaceId
            select (WorkspaceRole?)membership.Role).SingleOrDefaultAsync(cancellationToken);
        if (role is null) return null;
        var admin = role is WorkspaceRole.Owner or WorkspaceRole.Admin;
        return new ResourceAccess(true, admin, admin);
    }

    public async Task<ResourceAccess?> ProjectAsync(string subject, Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var workspace = await WorkspaceAsync(subject, workspaceId, cancellationToken);
        if (workspace is null) return null;
        // Both identifiers are required: a project from another tenant never matches.
        if (!await db.Projects.AnyAsync(x => x.Id == projectId && x.WorkspaceId == workspaceId && !x.IsDeleted, cancellationToken))
            return null;
        if (workspace.CanManage) return new ResourceAccess(true, true, true);

        var role = await (from membership in db.ProjectMemberships
            join user in db.Users on membership.UserId equals user.Id
            where user.CognitoSubject == subject && membership.WorkspaceId == workspaceId
                && membership.ProjectId == projectId
            select (ProjectRole?)membership.Role).SingleOrDefaultAsync(cancellationToken);
        return role switch
        {
            ProjectRole.Manager => new ResourceAccess(true, true, true),
            ProjectRole.Member => new ResourceAccess(true, true, false),
            ProjectRole.Viewer => new ResourceAccess(true, false, false),
            _ => null
        };
    }
}
