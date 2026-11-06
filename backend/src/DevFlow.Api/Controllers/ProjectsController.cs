using DevFlow.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/projects")]
public sealed class ProjectsController(IProjectService service, ITaskService tasks) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List(Guid workspaceId, CancellationToken ct) => Ok(await service.List(Subject, workspaceId, ct));
    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid workspaceId, Guid projectId, CancellationToken ct) => Ok(await service.Get(Subject, workspaceId, projectId, ct));
    [HttpPost, ProducesResponseType<ProjectDto>(201)]
    public async Task<ActionResult<ProjectDto>> Create(Guid workspaceId, ProjectRequest request, CancellationToken ct)
    {
        var result = await service.Create(Subject, workspaceId, request, ct);
        return Created($"/api/workspaces/{workspaceId}/projects/{result.Id}", result);
    }
    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid workspaceId, Guid projectId, ProjectRequest request, CancellationToken ct) =>
        Ok(await service.Update(Subject, workspaceId, projectId, request, ct));
    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid projectId, CancellationToken ct)
    { await service.Delete(Subject, workspaceId, projectId, ct); return NoContent(); }
    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> Members(Guid workspaceId, Guid projectId, CancellationToken ct) =>
        Ok(await service.Members(Subject, workspaceId, projectId, ct));
    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Role(Guid workspaceId, Guid projectId, Guid userId, ProjectRoleRequest request, CancellationToken ct)
    { await service.ChangeRole(Subject, workspaceId, projectId, userId, request, ct); return NoContent(); }
    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid workspaceId, Guid projectId, Guid userId, CancellationToken ct)
    { await service.RemoveMember(Subject, workspaceId, projectId, userId, ct); return NoContent(); }
    [HttpGet("{projectId:guid}/analytics")]
    public async Task<ActionResult<AnalyticsDto>> Analytics(Guid workspaceId, Guid projectId, CancellationToken ct) =>
        Ok(await tasks.Analytics(Subject, workspaceId, projectId, ct));
}
