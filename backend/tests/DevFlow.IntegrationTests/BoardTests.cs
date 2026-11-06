using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevFlow.Application.Projects;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using TaskStatus = DevFlow.Domain.Enums.TaskStatus;
namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class BoardTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static HttpClient Client(ApiFactory factory, string subject)
    { var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject)); return client; }
    private static async Task<BoardDto> Board(HttpClient client, string path) => (await client.GetFromJsonAsync<BoardDto>(path, Json, Ct))!;
    private static Task<HttpResponseMessage> Move(HttpClient client, string path, Guid task, TaskStatus status, Guid? before, string revision) =>
        client.PutAsJsonAsync(path + "/" + task, new MoveTaskRequest(status, before, revision), Json, Ct);

    [Fact]
    public async Task Moves_persist_order_history_and_detect_stale_board_revisions()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var tasks = Enumerable.Range(1, 3).Select(n => new TaskItem(seed.Project.Id, n, "Task " + n, seed.User.Id)).ToArray();
        db.AddRange(tasks); await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = $"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}/board";
        var initial = await Board(client, path);
        Assert.Equal(tasks.Select(t => t.Id), initial.Cards.Select(c => c.Task.Id));
        var result = await Move(client, path, tasks[2].Id, TaskStatus.ToDo, tasks[0].Id, initial.Revision);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var reordered = await Board(client, path);
        Assert.Equal(new[] { tasks[2].Id, tasks[0].Id, tasks[1].Id }, reordered.Cards.Select(c => c.Task.Id));
        Assert.NotEqual(initial.Revision, reordered.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await Move(client, path, tasks[0].Id, TaskStatus.Completed, null, initial.Revision)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Move(client, path, tasks[0].Id, TaskStatus.InReview, null, reordered.Revision)).StatusCode);
        var moved = await Board(client, path);
        Assert.Equal(TaskStatus.InReview, moved.Cards.Single(c => c.Task.Id == tasks[0].Id).Task.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Move(client, path, tasks[1].Id, TaskStatus.Completed, tasks[0].Id, moved.Revision)).StatusCode);
        Assert.Equal(2, await db.Activities.CountAsync(x => tasks.Select(t => t.Id).Contains(x.TaskId), Ct));
    }

    [Fact]
    public async Task Concurrent_moves_from_the_same_revision_have_one_winner()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var task = new TaskItem(seed.Project.Id, 1, "Concurrent", seed.User.Id); db.Add(task); await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = $"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}/board";
        var board = await Board(client, path);
        var responses = await Task.WhenAll(Move(client, path, task.Id, TaskStatus.Completed, null, board.Revision), Move(client, path, task.Id, TaskStatus.InProgress, null, board.Revision));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Board_rejects_viewer_mutations_foreign_access_and_archived_moves()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var viewer = new UserProfile(Guid.NewGuid().ToString(), "Viewer", "viewer@example.invalid");
        var task = new TaskItem(seed.Project.Id, 1, "Read only", seed.User.Id);
        db.AddRange(task, viewer, new WorkspaceMembership(seed.Workspace.Id, viewer.Id, WorkspaceRole.Member), new ProjectMembership(seed.Workspace.Id, seed.Project.Id, viewer.Id, ProjectRole.Viewer));
        await db.SaveChangesAsync(Ct);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, viewer.CognitoSubject);
        using var owner = Client(factory, seed.User.CognitoSubject);
        using var outsider = Client(factory, Guid.NewGuid().ToString());
        var path = $"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}/board";
        var board = await Board(client, path);
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(client, path, task.Id, TaskStatus.Completed, null, board.Revision)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path, Ct)).StatusCode);
        seed.Project.Update(seed.Project.Name, null, true); await db.SaveChangesAsync(Ct);
        Assert.Equal(HttpStatusCode.Conflict, (await Move(owner, path, task.Id, TaskStatus.Completed, null, board.Revision)).StatusCode);
    }
}
