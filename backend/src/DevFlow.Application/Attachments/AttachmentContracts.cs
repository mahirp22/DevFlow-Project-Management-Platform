using System.ComponentModel.DataAnnotations;
namespace DevFlow.Application.Attachments;

public sealed record UploadRequest([Required, StringLength(180)] string FileName,
    [Required, StringLength(100)] string ContentType, [Range(1, 10485760)] long Size,
    [Required, RegularExpression("^[A-Za-z0-9+/]{43}=$")] string Checksum);
public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, string Uploader, bool Ready, DateTimeOffset CreatedAt);
public sealed record UploadGrant(string Url, IReadOnlyDictionary<string, string> Fields, DateTimeOffset ExpiresAt);
public sealed record UploadStarted(Guid Id, UploadGrant Upload);
public sealed record DownloadGrant(string Url, DateTimeOffset ExpiresAt);
public sealed record ObjectMetadata(long Size, string ContentType, string? Checksum);
public sealed record AttachmentConfiguration(bool Configured, long MaximumBytes, int MaximumFiles);

public interface IAttachmentStorage
{
    bool Configured { get; }
    Task<UploadGrant> CreateUpload(string key, string contentType, long size, string checksum, CancellationToken ct);
    Task<ObjectMetadata?> Inspect(string key, CancellationToken ct);
    Task<DownloadGrant> Download(string key, string fileName, CancellationToken ct);
    Task DeleteAbandoned(string key, CancellationToken ct);
}
public interface IAttachmentService
{
    AttachmentConfiguration Configuration { get; }
    Task<IReadOnlyList<AttachmentDto>> List(string subject, Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct);
    Task<UploadStarted> Start(string subject, Guid workspaceId, Guid projectId, Guid taskId, UploadRequest request, CancellationToken ct);
    Task<AttachmentDto> Finalize(string subject, Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct);
    Task<DownloadGrant> Download(string subject, Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct);
    Task<int> Cleanup(CancellationToken ct);
}
public sealed class AttachmentStorageUnavailableException : Exception
{
    public AttachmentStorageUnavailableException() : base("Attachment storage is unavailable. Check the server's S3 configuration and AWS login.") { }
}
