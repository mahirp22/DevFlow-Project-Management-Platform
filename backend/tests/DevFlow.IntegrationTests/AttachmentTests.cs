using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevFlow.Application.Attachments;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using DevFlow.Infrastructure.Attachments;
using DevFlow.Infrastructure.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class AttachmentTests(PostgresFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string Checksum = Convert.ToBase64String(new byte[32]);
    private static HttpClient Client(ApiFactory factory, string subject)
    { var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject)); return client; }
    private async Task<(UserProfile User, Workspace Workspace, Project Project, TaskItem Task)> Seed()
    {
        await using var db = fixture.CreateContext(); var s = await TestData.AddProjectAsync(db);
        var task = new TaskItem(s.Project.Id, 1, "Attachments", s.User.Id); db.Add(task); await db.SaveChangesAsync(Ct);
        return (s.User, s.Workspace, s.Project, task);
    }
    private static string Path(Guid w, Guid p, Guid t) => $"/api/workspaces/{w}/projects/{p}/tasks/{t}/attachments";
    private static Task<HttpResponseMessage> Start(HttpClient c, string path, string filename = "notes.txt", string type = "text/plain", long size = 12) =>
        c.PostAsJsonAsync(path, new UploadRequest(filename, type, size, Checksum), Ct);

    [Fact]
    public async Task Upload_is_private_until_verified_and_finalization_is_idempotent()
    {
        var seed = await Seed(); var storage = new TestStorage();
        await using var factory = new ApiFactory(fixture.ConnectionString, attachments: storage);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id, seed.Task.Id);
        var response = await Start(client, path); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var started = (await response.Content.ReadFromJsonAsync<UploadStarted>(Ct))!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(path + "/" + started.Id + "/download", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/" + started.Id + "/finalize", null, Ct)).StatusCode);
        storage.Metadata = new(12, "text/plain", Checksum);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path + "/" + started.Id + "/finalize", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path + "/" + started.Id + "/finalize", null, Ct)).StatusCode);
        var files = (await client.GetFromJsonAsync<AttachmentDto[]>(path, Ct))!;
        Assert.True(Assert.Single(files).Ready);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path + "/" + started.Id + "/download", null, Ct)).StatusCode);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Activities.CountAsync(x => x.TaskId == seed.Task.Id, Ct));
        var stored = await db.Attachments.SingleAsync(x => x.Id == started.Id, Ct);
        Assert.DoesNotContain("notes.txt", stored.ObjectKey);
        Assert.StartsWith("attachments/", stored.ObjectKey);
    }

    [Theory]
    [InlineData(13, "text/plain", false)]
    [InlineData(12, "image/png", false)]
    [InlineData(12, "text/plain", true)]
    public async Task Finalize_rejects_unexpected_object_metadata(long size, string type, bool wrongChecksum)
    {
        var seed = await Seed(); var storage = new TestStorage { Metadata = new(size, type, wrongChecksum ? "different" : Checksum) };
        await using var factory = new ApiFactory(fixture.ConnectionString, attachments: storage);
        using var client = Client(factory, seed.User.CognitoSubject); var path = Path(seed.Workspace.Id, seed.Project.Id, seed.Task.Id);
        var started = (await (await Start(client, path)).Content.ReadFromJsonAsync<UploadStarted>(Ct))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/" + started.Id + "/finalize", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Tenant_roles_uploader_and_input_limits_are_enforced()
    {
        var seed = await Seed(); var foreign = await Seed(); var storage = new TestStorage();
        await using var db = fixture.CreateContext();
        var viewer = new UserProfile(Guid.NewGuid().ToString(), "Viewer", "viewer@example.invalid");
        var member = new UserProfile(Guid.NewGuid().ToString(), "Member", "member@example.invalid");
        db.AddRange(viewer, member, new WorkspaceMembership(seed.Workspace.Id, viewer.Id, WorkspaceRole.Member), new ProjectMembership(seed.Workspace.Id, seed.Project.Id, viewer.Id, ProjectRole.Viewer),
            new WorkspaceMembership(seed.Workspace.Id, member.Id, WorkspaceRole.Member), new ProjectMembership(seed.Workspace.Id, seed.Project.Id, member.Id, ProjectRole.Member));
        await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(fixture.ConnectionString, attachments: storage);
        using var owner = Client(factory, seed.User.CognitoSubject); using var readOnly = Client(factory, viewer.CognitoSubject);
        using var writer = Client(factory, member.CognitoSubject); using var outsider = Client(factory, foreign.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id, seed.Task.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await Start(readOnly, path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(Path(seed.Workspace.Id, seed.Project.Id, foreign.Task.Id), Ct)).StatusCode);
        foreach (var name in new[] { "../notes.txt", "program.exe", "page.html" }) Assert.Equal(HttpStatusCode.BadRequest, (await Start(owner, path, name)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(owner, path, size: 10485761)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Start(owner, path, size: 0)).StatusCode);
        var started = (await (await Start(owner, path)).Content.ReadFromJsonAsync<UploadStarted>(Ct))!;
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.PostAsync(path + "/" + started.Id + "/finalize", null, Ct)).StatusCode);
        Assert.Empty((await readOnly.GetFromJsonAsync<AttachmentDto[]>(path, Ct))!);
        storage.Metadata = new(12, "text/plain", Checksum);
        await owner.PostAsync(path + "/" + started.Id + "/finalize", null, Ct);
        Assert.Equal(HttpStatusCode.OK, (await readOnly.PostAsync(path + "/" + started.Id + "/download", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync(path + "/" + started.Id + "/download", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Cleanup_removes_only_abandoned_objects_and_expired_uploads_cannot_finalize()
    {
        var seed = await Seed(); var storage = new TestStorage { Metadata = new(12, "text/plain", Checksum) };
        await using var factory = new ApiFactory(fixture.ConnectionString, attachments: storage);
        using var client = Client(factory, seed.User.CognitoSubject); var path = Path(seed.Workspace.Id, seed.Project.Id, seed.Task.Id);
        var pending = (await (await Start(client, path)).Content.ReadFromJsonAsync<UploadStarted>(Ct))!;
        var complete = (await (await Start(client, path)).Content.ReadFromJsonAsync<UploadStarted>(Ct))!;
        await client.PostAsync(path + "/" + complete.Id + "/finalize", null, Ct);
        await using var db = fixture.CreateContext();
        var old = DateTimeOffset.UtcNow.AddDays(-2);
        await db.Attachments.Where(x => x.Id == pending.Id || x.Id == complete.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, old), Ct);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/" + pending.Id + "/finalize", null, Ct)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        Assert.Equal(1, await service.Cleanup(Ct));
        Assert.Single(storage.Deleted);
        Assert.Equal(0, await service.Cleanup(Ct));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path + "/" + complete.Id + "/download", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Unconfigured_storage_and_full_tasks_fail_without_issuing_upload_permissions()
    {
        var seed = await Seed();
        await using var disabledFactory = new ApiFactory(fixture.ConnectionString, attachments: new DisabledAttachmentStorage());
        using var disabled = Client(disabledFactory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id, seed.Task.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Start(disabled, path)).StatusCode);
        await using var db = fixture.CreateContext();
        for (var i = 0; i < 20; i++) db.Add(new TaskAttachment(seed.Task.Id, seed.User.Id, "notes.txt", "text/plain", 12, Checksum, "attachments/" + Guid.NewGuid()));
        await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(fixture.ConnectionString, attachments: new TestStorage());
        using var client = Client(factory, seed.User.CognitoSubject);
        Assert.Equal(HttpStatusCode.Conflict, (await Start(client, path)).StatusCode);
    }

    private sealed class TestStorage : IAttachmentStorage
    {
        public bool Configured => true;
        public ObjectMetadata? Metadata { get; set; }
        public List<string> Deleted { get; } = [];
        public Task<UploadGrant> CreateUpload(string key, string contentType, long size, string checksum, CancellationToken ct) =>
            Task.FromResult(new UploadGrant("https://storage.example.invalid", new Dictionary<string, string> { ["key"] = key }, DateTimeOffset.UtcNow.AddMinutes(5)));
        public Task<ObjectMetadata?> Inspect(string key, CancellationToken ct) => Task.FromResult(Metadata);
        public Task<DownloadGrant> Download(string key, string fileName, CancellationToken ct) => Task.FromResult(new DownloadGrant("https://storage.example.invalid/file", DateTimeOffset.UtcNow.AddMinutes(1)));
        public Task DeleteAbandoned(string key, CancellationToken ct) { Deleted.Add(key); return Task.CompletedTask; }
    }
}
