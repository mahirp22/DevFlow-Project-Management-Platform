using DevFlow.Domain.Validation;
namespace DevFlow.Domain.Entities;

public sealed class TaskAttachment
{
    private TaskAttachment() { }
    public TaskAttachment(Guid taskId, Guid uploaderId, string fileName, string contentType, long size, string checksum, string objectKey)
    {
        TaskId = DomainGuard.RequiredId(taskId, nameof(taskId));
        UploaderUserId = DomainGuard.RequiredId(uploaderId, nameof(uploaderId));
        FileName = DomainGuard.RequiredText(fileName, 180, nameof(fileName));
        ContentType = DomainGuard.RequiredText(contentType, 100, nameof(contentType));
        if (size is < 1 or > 10485760) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        Checksum = DomainGuard.RequiredText(checksum, 44, nameof(checksum));
        ObjectKey = DomainGuard.RequiredText(objectKey, 300, nameof(objectKey));
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TaskId { get; private set; }
    public Guid UploaderUserId { get; private set; }
    public string FileName { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long Size { get; private set; }
    public string Checksum { get; private set; } = "";
    public string ObjectKey { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; } = DateTimeOffset.UtcNow.AddMinutes(15);
    public DateTimeOffset? FinalizedAt { get; private set; }
    public DateTimeOffset? LastCleanedAt { get; private set; }
    public void FinalizeUpload() => FinalizedAt ??= DateTimeOffset.UtcNow;
    public void MarkCleaned() => LastCleanedAt = DateTimeOffset.UtcNow;
}
