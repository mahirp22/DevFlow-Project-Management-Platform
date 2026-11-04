using DevFlow.Application.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DevFlow.Api.Authentication;

public sealed record ProjectResource(Guid WorkspaceId, Guid ProjectId);
public sealed record ProjectPermission(string Permission) : IAuthorizationRequirement;

// Resource-based authorization for project operations. Future CRUD controllers must invoke
// AuthorizeAsync with the resource from the route, not trust frontend permission flags.
public sealed class ProjectPermissionHandler(IResourceAccess access, IHttpContextAccessor http)
    : AuthorizationHandler<ProjectPermission, ProjectResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ProjectPermission requirement, ProjectResource resource)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (context.User.Identity?.IsAuthenticated != true || subject is null) return;
        var permissions = await access.ProjectAsync(subject, resource.WorkspaceId, resource.ProjectId,
            http.HttpContext?.RequestAborted ?? CancellationToken.None);
        var allowed = requirement.Permission switch
        {
            "read" => permissions?.CanRead == true,
            "write" => permissions?.CanWrite == true,
            "manage" => permissions?.CanManage == true,
            _ => false
        };
        if (allowed) context.Succeed(requirement);
    }
}
