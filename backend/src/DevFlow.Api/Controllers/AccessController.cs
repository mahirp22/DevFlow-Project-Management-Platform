using DevFlow.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceId:guid}")]
public sealed class AccessController(IResourceAccess access) : ControllerBase
{
    [HttpGet("access")]
    [ProducesResponseType<ResourceAccess>(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ResourceAccess>> Workspace(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await access.WorkspaceAsync(User.FindFirst("sub")!.Value, workspaceId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("projects/{projectId:guid}/access")]
    [ProducesResponseType<ResourceAccess>(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ResourceAccess>> Project(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var result = await access.ProjectAsync(User.FindFirst("sub")!.Value, workspaceId, projectId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
