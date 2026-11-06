using DevFlow.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/board")]
public sealed class BoardController(IBoardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BoardDto>> Get(Guid workspaceId, Guid projectId, CancellationToken ct) =>
        Ok(await service.Get(User.FindFirst("sub")!.Value, workspaceId, projectId, ct));
    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<BoardDto>> Move(Guid workspaceId, Guid projectId, Guid taskId, MoveTaskRequest request, CancellationToken ct) =>
        Ok(await service.Move(User.FindFirst("sub")!.Value, workspaceId, projectId, taskId, request, ct));
}
