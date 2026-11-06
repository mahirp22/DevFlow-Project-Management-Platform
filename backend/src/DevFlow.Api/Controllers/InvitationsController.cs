using DevFlow.Application.Identity;
using DevFlow.Application.Projects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/invitations")]
public sealed class InvitationsController(IInvitationService service, IIdentityDirectory identity) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> List(Guid workspaceId, Guid projectId, CancellationToken ct) =>
        Ok(await service.List(Subject, workspaceId, projectId, ct));
    [HttpPost, ProducesResponseType<InvitationCreated>(201)]
    public async Task<ActionResult<InvitationCreated>> Create(Guid workspaceId, Guid projectId, InvitationRequest request, CancellationToken ct)
    {
        var result = await service.Create(Subject, workspaceId, projectId, request, ct);
        Response.Headers.CacheControl = "no-store";
        return Created($"/api/workspaces/{workspaceId}/projects/{projectId}/invitations", result);
    }
    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Revoke(Guid workspaceId, Guid projectId, Guid invitationId, CancellationToken ct)
    { await service.Revoke(Subject, workspaceId, projectId, invitationId, ct); return NoContent(); }
    [HttpPost("/api/invitations/accept")]
    public async Task<ActionResult<ProjectDto>> Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token)) return Unauthorized();
        var verified = await identity.GetVerifiedAsync(token, Subject, ct);
        if (verified is null) return Forbid();
        return Ok(await service.Accept(verified, request.Token, ct));
    }
}
