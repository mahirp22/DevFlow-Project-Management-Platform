using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class RealtimeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Websocket_subscription_receives_committed_changes_and_loses_access_after_removal()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var viewer = new UserProfile(Guid.NewGuid().ToString(), "Viewer", "viewer@example.invalid");
        db.AddRange(viewer, new WorkspaceMembership(seed.Workspace.Id, viewer.Id, WorkspaceRole.Member),
            new ProjectMembership(seed.Workspace.Id, seed.Project.Id, viewer.Id, ProjectRole.Viewer));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(seed.User.CognitoSubject));
        using var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/hubs/projects?access_token=" + TestTokens.Create(viewer.CognitoSubject)), TestContext.Current.CancellationToken);
        var reader = new HubReader(socket);
        await reader.Send(new { protocol = "json", version = 1 });
        Assert.Equal("{}", (await reader.Read()).GetRawText());
        await reader.Send(new { type = 1, invocationId = "1", target = "Subscribe", arguments = new[] { seed.Workspace.Id, seed.Project.Id } });
        Assert.False((await reader.Read()).TryGetProperty("error", out _));
        var path = $"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}";
        var created = await owner.PostAsJsonAsync(path + "/tasks", new { title = "Live task" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var changed = await reader.Read();
        Assert.Equal("ProjectChanged", changed.GetProperty("target").GetString());
        Assert.Empty(changed.GetProperty("arguments").EnumerateArray());
        Assert.True(await db.Tasks.AnyAsync(x => x.ProjectId == seed.Project.Id && x.Title == "Live task", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path + "/members/" + viewer.Id, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal("AccessRevoked", (await reader.Read()).GetProperty("target").GetString());
        await reader.Send(new { type = 1, invocationId = "2", target = "Subscribe", arguments = new[] { seed.Workspace.Id, seed.Project.Id } });
        Assert.True((await reader.Read()).TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Anonymous_negotiation_and_query_tokens_on_rest_are_rejected_and_foreign_subscription_is_denied()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var other = await TestData.AddProjectAsync(db);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/hubs/projects/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me?access_token=" + TestTokens.Create(seed.User.CognitoSubject), TestContext.Current.CancellationToken)).StatusCode);
        using var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/api/hubs/projects?access_token=" + TestTokens.Create(seed.User.CognitoSubject)), TestContext.Current.CancellationToken);
        var reader = new HubReader(socket);
        await reader.Send(new { protocol = "json", version = 1 }); await reader.Read();
        await reader.Send(new { type = 1, invocationId = "1", target = "Subscribe", arguments = new[] { other.Workspace.Id, other.Project.Id } });
        Assert.True((await reader.Read()).TryGetProperty("error", out _));
    }

    private sealed class HubReader(WebSocket socket)
    {
        private string buffered = "";
        public Task Send(object message) => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + '\u001e')), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);
        public async Task<JsonElement> Read()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (true)
            {
                var separator = buffered.IndexOf('\u001e');
                if (separator >= 0)
                {
                    var json = buffered[..separator]; buffered = buffered[(separator + 1)..];
                    using var document = JsonDocument.Parse(json);
                    var value = document.RootElement.Clone();
                    if (value.TryGetProperty("type", out var type) && type.GetInt32() == 6) continue;
                    return value;
                }
                var bytes = new byte[4096];
                var received = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
                Assert.NotEqual(WebSocketMessageType.Close, received.MessageType);
                buffered += Encoding.UTF8.GetString(bytes, 0, received.Count);
            }
        }
    }
}
