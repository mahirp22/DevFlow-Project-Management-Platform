using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using DevFlow.Application.Attachments;
namespace DevFlow.Infrastructure.Attachments;

public sealed class AttachmentSettings
{
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "";
    public bool Configured => !string.IsNullOrWhiteSpace(BucketName) && !string.IsNullOrWhiteSpace(Region);
    public void Validate()
    {
        if (string.IsNullOrEmpty(BucketName) && string.IsNullOrEmpty(Region)) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(BucketName, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")
            || !System.Text.RegularExpressions.Regex.IsMatch(Region, "^[a-z]{2}(-[a-z]+)+-[0-9]+$"))
            throw new InvalidOperationException("Configure Attachments:BucketName and Attachments:Region together. Use a bucket name without dots.");
    }
}

public sealed class S3AttachmentStorage(IAmazonS3 s3, AttachmentSettings settings) : IAttachmentStorage
{
    public bool Configured => settings.Configured;
    private async Task UnversionedBucket(CancellationToken ct)
    {
        var result = await s3.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = settings.BucketName }, ct);
        // Cleanup deletes actual objects; a versioned bucket would leave old versions/delete markers behind.
        if (result.VersioningConfig?.Status == VersionStatus.Enabled || result.VersioningConfig?.Status == VersionStatus.Suspended)
            throw new AttachmentStorageUnavailableException();
    }
    private static async Task<T> Safe<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) { throw; }
        catch (AttachmentStorageUnavailableException) { throw; }
        catch (Exception) { throw new AttachmentStorageUnavailableException(); }
    }
    public Task<UploadGrant> CreateUpload(string key, string contentType, long size, string checksum, CancellationToken ct) => Safe(async () =>
    {
        await UnversionedBucket(ct);
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        var response = await s3.CreatePresignedPostAsync(new CreatePresignedPostRequest
        {
            BucketName = settings.BucketName, Key = key, Expires = expires.UtcDateTime,
            Fields = new Dictionary<string, string>
            {
                ["Content-Type"] = contentType, ["x-amz-checksum-sha256"] = checksum,
                ["x-amz-server-side-encryption"] = "AES256", ["success_action_status"] = "204",
                ["Content-Disposition"] = "attachment"
            },
            Conditions = [S3PostCondition.ContentLengthRange(size, size),
                S3PostCondition.ExactMatch("Content-Type", contentType),
                S3PostCondition.ExactMatch("x-amz-checksum-sha256", checksum),
                S3PostCondition.ExactMatch("x-amz-server-side-encryption", "AES256"),
                S3PostCondition.ExactMatch("success_action_status", "204"),
                S3PostCondition.ExactMatch("Content-Disposition", "attachment")]
        });
        return new UploadGrant(response.Url, response.Fields, expires);
    });
    public Task<ObjectMetadata?> Inspect(string key, CancellationToken ct) => Safe<ObjectMetadata?>(async () =>
    {
        await UnversionedBucket(ct);
        try
        {
            var value = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = settings.BucketName,
                Key = key, ChecksumMode = ChecksumMode.ENABLED }, ct);
            return new(value.ContentLength, value.Headers.ContentType, value.ChecksumSHA256);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return null; }
    });
    public Task<DownloadGrant> Download(string key, string fileName, CancellationToken ct) => Safe(async () =>
    {
        await UnversionedBucket(ct);
        var expires = DateTimeOffset.UtcNow.AddSeconds(60);
        var url = await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = settings.BucketName, Key = key, Verb = HttpVerb.GET, Expires = expires.UtcDateTime,
            Protocol = Protocol.HTTPS,
            ResponseHeaderOverrides = new ResponseHeaderOverrides { ContentType = "application/octet-stream",
                ContentDisposition = "attachment; filename=\"download\"; filename*=UTF-8''" + Uri.EscapeDataString(fileName),
                CacheControl = "private, no-store" }
        });
        return new DownloadGrant(url, expires);
    });
    public async Task DeleteAbandoned(string key, CancellationToken ct) => await Safe(async () =>
    {
        await UnversionedBucket(ct);
        return await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = settings.BucketName, Key = key }, ct);
    });
}

public sealed class DisabledAttachmentStorage : IAttachmentStorage
{
    public bool Configured => false;
    public Task<UploadGrant> CreateUpload(string key, string contentType, long size, string checksum, CancellationToken ct) => throw new AttachmentStorageUnavailableException();
    public Task<ObjectMetadata?> Inspect(string key, CancellationToken ct) => throw new AttachmentStorageUnavailableException();
    public Task<DownloadGrant> Download(string key, string fileName, CancellationToken ct) => throw new AttachmentStorageUnavailableException();
    public Task DeleteAbandoned(string key, CancellationToken ct) => throw new AttachmentStorageUnavailableException();
}
