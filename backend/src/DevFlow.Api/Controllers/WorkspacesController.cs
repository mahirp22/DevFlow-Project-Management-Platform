using DevFlow.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceService service) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> List(CancellationToken ct) => Ok(await service.List(Subject, ct));
    [HttpPost, ProducesResponseType<WorkspaceDto>(201)]
    public async Task<ActionResult<WorkspaceDto>> Create(WorkspaceRequest request, CancellationToken ct)
    {
        var result = await service.Create(Subject, request, ct);
        return Created($"/api/workspaces/{result.Id}/access", result);
    }
    [HttpPut("{workspaceId:guid}"), ProducesResponseType(204)]
    public async Task<IActionResult> Rename(Guid workspaceId, WorkspaceRequest request, CancellationToken ct)
    { await service.Rename(Subject, workspaceId, request, ct); return NoContent(); }
    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> Members(Guid workspaceId, CancellationToken ct) =>
        Ok(await service.Members(Subject, workspaceId, ct));
    [HttpPut("{workspaceId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Role(Guid workspaceId, Guid userId, WorkspaceRoleRequest request, CancellationToken ct)
    { await service.ChangeRole(Subject, workspaceId, userId, request, ct); return NoContent(); }
}
