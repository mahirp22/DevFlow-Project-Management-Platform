using DevFlow.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.IntegrationTests;

// Test-only operations prove the policies enforce permissions, not just report UI flags.
[ApiController]
[Authorize]
[Route("test/authorization/{workspaceId:guid}/projects/{projectId:guid}")]
public sealed class AuthorizationProbeController(IAuthorizationService authorization) : ControllerBase
{
    [HttpPost("{permission}")]
    public async Task<IActionResult> Check(Guid workspaceId, Guid projectId, string permission)
    {
        if (permission is not ("read" or "write" or "manage")) return NotFound();
        var result = await authorization.AuthorizeAsync(User, new ProjectResource(workspaceId, projectId), "project:" + permission);
        return result.Succeeded ? NoContent() : Forbid();
    }
}
