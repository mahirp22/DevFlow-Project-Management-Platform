using DevFlow.Application.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevFlow.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttachmentsController(IAttachmentService service) : ControllerBase
{
    private string Subject => User.FindFirst("sub")!.Value;
    [HttpGet("/api/attachments/configuration")]
    public ActionResult<AttachmentConfiguration> Configuration() => Ok(service.Configuration);
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct) =>
        Ok(await service.List(Subject, workspaceId, projectId, taskId, ct));
    [HttpPost, ProducesResponseType<UploadStarted>(201)]
    public async Task<ActionResult<UploadStarted>> Start(Guid workspaceId, Guid projectId, Guid taskId, UploadRequest request, CancellationToken ct)
    {
        var result = await service.Start(Subject, workspaceId, projectId, taskId, request, ct);
        return Created($"/api/workspaces/{workspaceId}/projects/{projectId}/tasks/{taskId}/attachments", result);
    }
    [HttpPost("{attachmentId:guid}/finalize")]
    public async Task<ActionResult<AttachmentDto>> Finalize(Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct) =>
        Ok(await service.Finalize(Subject, workspaceId, projectId, taskId, attachmentId, ct));
    [HttpPost("{attachmentId:guid}/download")]
    public async Task<ActionResult<DownloadGrant>> Download(Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct) =>
        Ok(await service.Download(Subject, workspaceId, projectId, taskId, attachmentId, ct));
}
