using System.ComponentModel.DataAnnotations;
using DevFlow.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/tasks")]
public sealed class TasksController(ITaskService service) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    [HttpGet]
    public async Task<ActionResult<PageResult<TaskDto>>> List(Guid workspaceId, Guid projectId, [FromQuery] TaskFilter filter, CancellationToken ct) =>
        Ok(await service.List(Subject, workspaceId, projectId, filter, ct));
    [HttpGet("/api/me/tasks")]
    public async Task<ActionResult<PageResult<TaskDto>>> Mine([FromQuery] TaskFilter filter, CancellationToken ct) =>
        Ok(await service.Mine(Subject, filter, ct));
    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<TaskDto>> Get(Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct) =>
        Ok(await service.Get(Subject, workspaceId, projectId, taskId, ct));
    [HttpPost, ProducesResponseType<TaskDto>(201)]
    public async Task<ActionResult<TaskDto>> Create(Guid workspaceId, Guid projectId, TaskRequest request, CancellationToken ct)
    {
        var result = await service.Create(Subject, workspaceId, projectId, request, ct);
        return Created($"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/{result.Id}", result);
    }
    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<TaskDto>> Update(Guid workspaceId, Guid projectId, Guid taskId, TaskRequest request, CancellationToken ct) =>
        Ok(await service.Update(Subject, workspaceId, projectId, taskId, request, ct));
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid projectId, Guid taskId, [FromQuery, Range(1, uint.MaxValue)] uint version, CancellationToken ct)
    { await service.Delete(Subject, workspaceId, projectId, taskId, version, ct); return NoContent(); }
    [HttpGet("{taskId:guid}/comments")]
    public async Task<ActionResult<PageResult<CommentDto>>> Comments(Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct, [FromQuery, Range(1,100000)] int page = 1) =>
        Ok(await service.Comments(Subject, workspaceId, projectId, taskId, page, ct));
    [HttpPost("{taskId:guid}/comments"), ProducesResponseType<CommentDto>(201)]
    public async Task<ActionResult<CommentDto>> Comment(Guid workspaceId, Guid projectId, Guid taskId, CommentRequest request, CancellationToken ct)
    {
        var result = await service.Comment(Subject, workspaceId, projectId, taskId, request, ct);
        return Created($"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/{taskId}/comments", result);
    }
    [HttpGet("{taskId:guid}/activity")]
    public async Task<ActionResult<PageResult<ActivityDto>>> Activity(Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct, [FromQuery, Range(1,100000)] int page = 1) =>
        Ok(await service.Activity(Subject, workspaceId, projectId, taskId, page, ct));
}
