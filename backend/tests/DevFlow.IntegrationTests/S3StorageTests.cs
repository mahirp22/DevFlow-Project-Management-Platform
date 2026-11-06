using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DevFlow.Application.Attachments;
using DevFlow.Infrastructure.Attachments;
using System.Text;
using System.Text.Json;
namespace DevFlow.IntegrationTests;

public sealed class S3StorageTests
{
    [Fact]
    public async Task Real_sdk_signs_exact_key_size_checksum_type_and_encryption_without_network_access()
    {
        using var client = new OfflineS3();
        var storage = new S3AttachmentStorage(client, new AttachmentSettings { BucketName = "devflow-test-bucket", Region = "ca-central-1" });
        var checksum = Convert.ToBase64String(new byte[32]);
        var grant = await storage.CreateUpload("attachments/test-id", "text/plain", 12, checksum, TestContext.Current.CancellationToken);
        using var policy = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(grant.Fields.Single(x => string.Equals(x.Key, "policy", StringComparison.OrdinalIgnoreCase)).Value)));
        var json = policy.RootElement.GetProperty("conditions").GetRawText();
        Assert.Contains("content-length-range", json);
        Assert.Contains("12,12", json.Replace(" ", ""));
        Assert.Contains("attachments/test-id", json);
        Assert.Contains("x-amz-checksum-sha256", json);
        Assert.Equal(checksum, grant.Fields["x-amz-checksum-sha256"]);
        Assert.Equal("text/plain", grant.Fields["Content-Type"]);
        Assert.Equal("AES256", grant.Fields["x-amz-server-side-encryption"]);
        Assert.Equal("attachment", grant.Fields["Content-Disposition"]);
        Assert.StartsWith("https://", grant.Url);
        Assert.InRange(grant.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
        var download = await storage.Download("attachments/test-id", "notes.txt", TestContext.Current.CancellationToken);
        Assert.Contains("response-content-disposition", download.Url);
        Assert.Contains("response-content-type", download.Url);
        Assert.InRange(download.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(60));
    }
    [Fact]
    public async Task Versioned_buckets_are_rejected_instead_of_leaving_hidden_versions_during_cleanup()
    {
        using var client = new OfflineS3 { Versioned = true };
        var storage = new S3AttachmentStorage(client, new AttachmentSettings { BucketName = "devflow-test-bucket", Region = "ca-central-1" });
        await Assert.ThrowsAsync<AttachmentStorageUnavailableException>(() => storage.CreateUpload("attachments/test", "text/plain", 12, Convert.ToBase64String(new byte[32]), TestContext.Current.CancellationToken));
    }
    private sealed class OfflineS3() : AmazonS3Client(new BasicAWSCredentials("test-only-access-key", "test-only-secret-key"), RegionEndpoint.CACentral1)
    {
        public bool Versioned { get; init; }
        public override Task<GetBucketVersioningResponse> GetBucketVersioningAsync(GetBucketVersioningRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GetBucketVersioningResponse { VersioningConfig = new S3BucketVersioningConfig { Status = Versioned ? VersionStatus.Enabled : VersionStatus.Off } });
    }
}
