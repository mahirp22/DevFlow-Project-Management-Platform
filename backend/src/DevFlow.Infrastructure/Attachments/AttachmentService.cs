using DevFlow.Application.Attachments;
using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Infrastructure.Persistence;
using DevFlow.Infrastructure.Projects;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Attachments;

public sealed class AttachmentService(DevFlowDbContext db, AccessGuard guard, IAttachmentStorage storage) : IAttachmentService
{
    public AttachmentConfiguration Configuration => new(storage.Configured, 10485760, 20);
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    { [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".txt"] = "text/plain" };
    private static void Validate(UploadRequest request)
    {
        var name = request.FileName;
        if (name != name.Trim() || name.Any(c => char.IsControl(c) || "/\\:*?\"<>|".Contains(c))
            || !Types.TryGetValue(Path.GetExtension(name), out var expected) || request.ContentType != expected)
            throw new RequestFailure(400, "Choose a PDF, PNG, JPEG or TXT file with a simple filename and matching content type.");
        if (Convert.ToBase64String(Convert.FromBase64String(request.Checksum)) != request.Checksum)
            throw new RequestFailure(400, "Invalid SHA-256 checksum.");
    }
    private async Task<AttachmentDto> Dto(TaskAttachment attachment, CancellationToken ct) => new(attachment.Id,
        attachment.FileName, attachment.ContentType, attachment.Size,
        await db.Users.Where(x => x.Id == attachment.UploaderUserId).Select(x => x.DisplayName).SingleAsync(ct),
        attachment.FinalizedAt is not null, attachment.CreatedAt);
    public async Task<IReadOnlyList<AttachmentDto>> List(string subject, Guid workspaceId, Guid projectId, Guid taskId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct); await guard.Task(projectId, taskId, ct);
        var user = await guard.User(subject, ct); var now = DateTimeOffset.UtcNow;
        return await (from a in db.Attachments join u in db.Users on a.UploaderUserId equals u.Id
            where a.TaskId == taskId && (a.FinalizedAt != null || (a.UploaderUserId == user.Id && a.ExpiresAt > now))
            orderby a.CreatedAt descending
            select new AttachmentDto(a.Id, a.FileName, a.ContentType, a.Size, u.DisplayName, a.FinalizedAt != null, a.CreatedAt)).ToListAsync(ct);
    }
    public async Task<UploadStarted> Start(string subject, Guid workspaceId, Guid projectId, Guid taskId, UploadRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockProject(projectId, ct);
        await guard.Project(subject, workspaceId, projectId, "write", ct); await guard.Task(projectId, taskId, ct);
        var user = await guard.User(subject, ct); Validate(request);
        if (!storage.Configured) throw new AttachmentStorageUnavailableException();
        var now = DateTimeOffset.UtcNow;
        if (await db.Attachments.CountAsync(x => x.TaskId == taskId && (x.FinalizedAt != null || x.ExpiresAt > now), ct) >= 20)
            throw new RequestFailure(409, "This task already has 20 attachments or pending uploads.");
        var key = $"attachments/{workspaceId:N}/{projectId:N}/{taskId:N}/{Guid.NewGuid():N}";
        var attachment = new TaskAttachment(taskId, user.Id, request.FileName, request.ContentType, request.Size, request.Checksum, key);
        var upload = await storage.CreateUpload(key, request.ContentType, request.Size, request.Checksum, ct);
        db.Add(attachment); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(attachment.Id, upload);
    }
    public async Task<AttachmentDto> Finalize(string subject, Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockProject(projectId, ct);
        await guard.Project(subject, workspaceId, projectId, "write", ct); await guard.Task(projectId, taskId, ct);
        var user = await guard.User(subject, ct);
        var attachment = await db.Attachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.TaskId == taskId, ct)
            ?? throw new RequestFailure(404, "Attachment not found.");
        if (attachment.UploaderUserId != user.Id) throw new RequestFailure(403, "Only the uploader can finish this upload.");
        if (attachment.FinalizedAt is not null) return await Dto(attachment, ct);
        if (attachment.ExpiresAt <= DateTimeOffset.UtcNow) throw new RequestFailure(409, "This upload expired. Choose the file again.");
        var metadata = await storage.Inspect(attachment.ObjectKey, ct)
            ?? throw new RequestFailure(409, "The file has not reached S3. Upload it before finishing.");
        if (metadata.Size != attachment.Size || metadata.ContentType != attachment.ContentType || metadata.Checksum != attachment.Checksum)
            throw new RequestFailure(409, "The uploaded file does not match the approved size, type or checksum.");
        attachment.FinalizeUpload();
        db.Add(new TaskActivity(taskId, user.Id, "Attached file: " + attachment.FileName));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return await Dto(attachment, ct);
    }
    public async Task<DownloadGrant> Download(string subject, Guid workspaceId, Guid projectId, Guid taskId, Guid attachmentId, CancellationToken ct)
    {
        await guard.Project(subject, workspaceId, projectId, "read", ct); await guard.Task(projectId, taskId, ct);
        var attachment = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId && x.TaskId == taskId && x.FinalizedAt != null, ct)
            ?? throw new RequestFailure(404, "Attachment not found.");
        return await storage.Download(attachment.ObjectKey, attachment.FileName, ct);
    }
    private Task LockProject(Guid projectId, CancellationToken ct) =>
        db.Projects.FromSqlInterpolated($"SELECT * FROM \"Projects\" WHERE \"Id\" = {projectId} FOR UPDATE").ToListAsync(ct);
    public async Task<int> Cleanup(CancellationToken ct)
    {
        if (!storage.Configured) return 0;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Retain tombstones and revisit daily: a previously authorized upload can arrive late.
        var abandoned = await db.Attachments.FromSqlInterpolated($"""
            SELECT * FROM "TaskAttachments" WHERE "FinalizedAt" IS NULL AND "ExpiresAt" < {cutoff}
            AND ("LastCleanedAt" IS NULL OR "LastCleanedAt" < {cutoff})
            ORDER BY "LastCleanedAt" NULLS FIRST, "ExpiresAt" LIMIT 100 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        foreach (var attachment in abandoned)
        { await storage.DeleteAbandoned(attachment.ObjectKey, ct); attachment.MarkCleaned(); }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return abandoned.Count;
    }
}
