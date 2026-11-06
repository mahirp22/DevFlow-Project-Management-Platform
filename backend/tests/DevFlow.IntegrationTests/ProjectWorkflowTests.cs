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
public sealed class ProjectWorkflowTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static HttpClient Client(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject));
        return client;
    }
    private static async Task<T> Body<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken))!;
    }
    private static Task<HttpResponseMessage> Send(HttpClient client, string path, object body, bool put = false)
    {
        var request = new HttpRequestMessage(put ? HttpMethod.Put : HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) };
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private static string Path(Guid workspaceId, Guid projectId) => $"/api/workspaces/{workspaceId}/projects/{projectId}";
    private async Task<(UserProfile User, Workspace Workspace, Project Project)> Seed()
    {
        await using var db = fixture.CreateContext();
        return await TestData.AddProjectAsync(db);
    }

    [Fact]
    public async Task Display_name_updates_only_the_authenticated_profile_and_rejects_invalid_names()
    {
        var seed = await Seed();
        var other = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var response = await Send(client, "/api/me", new { displayName = "  Alex Chen  ", id = other.User.Id, email = "changed@example.invalid" }, put: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateContext();
        var saved = await db.Users.SingleAsync(x => x.Id == seed.User.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Alex Chen", saved.DisplayName);
        Assert.Equal(seed.User.Email, saved.Email);
        Assert.Equal(other.User.DisplayName, (await db.Users.SingleAsync(x => x.Id == other.User.Id, TestContext.Current.CancellationToken)).DisplayName);
        foreach (var invalid in new[] { "   ", new string('a', 101) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, "/api/me", new { displayName = invalid }, put: true)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, "/api/me", new { displayName = "Nobody" }, put: true)).StatusCode);
        var members = await Body<MemberDto[]>(await client.GetAsync(Path(seed.Workspace.Id, seed.Project.Id) + "/members", TestContext.Current.CancellationToken));
        Assert.Contains(members, m => m.UserId == seed.User.Id && m.DisplayName == "Alex Chen");
    }

    [Fact]
    public async Task Full_workflow_persists_tasks_comments_history_filters_and_analytics()
    {
        var seed = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var workspace = await Body<WorkspaceDto>(await Send(client, "/api/workspaces", new WorkspaceRequest("Launch team")), HttpStatusCode.Created);
        Assert.Equal(WorkspaceRole.Owner, workspace.Role);
        var project = await Body<ProjectDto>(await Send(client, $"/api/workspaces/{workspace.Id}/projects",
            new ProjectRequest("APP", "First release", "Ship a useful feature")), HttpStatusCode.Created);
        var path = Path(workspace.Id, project.Id);
        var request = new TaskRequest("Build search", "Support title queries", TaskPriority.High, TaskStatus.InProgress,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), seed.User.Id);
        var task = await Body<TaskDto>(await Send(client, path + "/tasks", request), HttpStatusCode.Created);
        Assert.Equal(1, task.Number);
        Assert.True(task.Version > 0);
        var changed = await Body<TaskDto>(await Send(client, path + "/tasks/" + task.Id,
            request with { Title = "Build task search", Version = task.Version }, put: true));
        Assert.NotEqual(task.Version, changed.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, path + "/tasks/" + task.Id,
            request with { Version = task.Version }, put: true)).StatusCode);
        await Body<CommentDto>(await Send(client, path + "/tasks/" + task.Id + "/comments", new CommentRequest("Ready for review")), HttpStatusCode.Created);
        var comments = await Body<PageResult<CommentDto>>(await client.GetAsync(path + "/tasks/" + task.Id + "/comments", TestContext.Current.CancellationToken));
        Assert.Single(comments.Items);
        var activity = await Body<PageResult<ActivityDto>>(await client.GetAsync(path + "/tasks/" + task.Id + "/activity", TestContext.Current.CancellationToken));
        Assert.Equal(3, activity.Total);
        var list = await Body<PageResult<TaskDto>>(await client.GetAsync(path + "/tasks?search=TASK&priority=High&status=InProgress&overdue=true&pageSize=1", TestContext.Current.CancellationToken));
        Assert.Single(list.Items);
        var mine = await Body<PageResult<TaskDto>>(await client.GetAsync("/api/me/tasks", TestContext.Current.CancellationToken));
        Assert.Contains(mine.Items, x => x.Id == task.Id);
        var analytics = await Body<AnalyticsDto>(await client.GetAsync(path + "/analytics", TestContext.Current.CancellationToken));
        Assert.Equal(1, analytics.Overdue);
        Assert.Equal(1, analytics.Total);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/tasks/" + task.Id + "?version=" + changed.Version, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/tasks/" + task.Id, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(0, (await Body<AnalyticsDto>(await client.GetAsync(path + "/analytics", TestContext.Current.CancellationToken))).Total);
        await using var db = fixture.CreateContext();
        Assert.Equal(4, await db.Activities.CountAsync(x => x.TaskId == task.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Viewers_and_outsiders_cannot_mutate_or_read_other_tenants()
    {
        var seed = await Seed();
        var other = await Seed();
        await using var db = fixture.CreateContext();
        var viewer = new UserProfile(Guid.NewGuid().ToString(), "Viewer", "viewer@example.invalid");
        db.AddRange(viewer, new WorkspaceMembership(seed.Workspace.Id, viewer.Id, WorkspaceRole.Member),
            new ProjectMembership(seed.Workspace.Id, seed.Project.Id, viewer.Id, ProjectRole.Viewer));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, viewer.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, path + "/tasks", new TaskRequest("Denied", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, path + "/invitations", new InvitationRequest("x@example.invalid", ProjectRole.Manager))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Path(other.Workspace.Id, other.Project.Id) + "/tasks", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Path(seed.Workspace.Id, other.Project.Id) + "/analytics", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Invitation_is_bound_to_verified_email_single_use_and_never_stores_raw_token()
    {
        var seed = await Seed();
        await using var db = fixture.CreateContext();
        var recipient = new UserProfile(Guid.NewGuid().ToString(), "Recipient", "stale-profile@example.invalid");
        db.Add(recipient); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var owner = Client(factory, seed.User.CognitoSubject);
        using var invited = Client(factory, recipient.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        var wrong = await Body<InvitationCreated>(await Send(owner, path + "/invitations", new InvitationRequest("different@example.invalid", ProjectRole.Manager)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(invited, "/api/invitations/accept", new AcceptInvitationRequest(wrong.Token))).StatusCode);
        var invite = await Body<InvitationCreated>(await Send(owner, path + "/invitations", new InvitationRequest("verified@example.invalid", ProjectRole.Viewer)), HttpStatusCode.Created);
        var stored = await db.Invitations.SingleAsync(x => x.Id == invite.Invitation.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(invite.Token, stored.TokenHash);
        await Body<ProjectDto>(await Send(invited, "/api/invitations/accept", new AcceptInvitationRequest(invite.Token)));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(invited, "/api/invitations/accept", new AcceptInvitationRequest(invite.Token))).StatusCode);
        Assert.Equal(WorkspaceRole.Member, (await db.WorkspaceMemberships.SingleAsync(x => x.WorkspaceId == seed.Workspace.Id && x.UserId == recipient.Id, TestContext.Current.CancellationToken)).Role);
        Assert.Equal(ProjectRole.Viewer, (await db.ProjectMemberships.SingleAsync(x => x.ProjectId == seed.Project.Id && x.UserId == recipient.Id, TestContext.Current.CancellationToken)).Role);
        var elevation = await Body<InvitationCreated>(await Send(owner, path + "/invitations", new InvitationRequest("verified@example.invalid", ProjectRole.Manager)), HttpStatusCode.Created);
        await Body<ProjectDto>(await Send(invited, "/api/invitations/accept", new AcceptInvitationRequest(elevation.Token)));
        db.ChangeTracker.Clear();
        Assert.Equal(ProjectRole.Viewer, (await db.ProjectMemberships.SingleAsync(x => x.ProjectId == seed.Project.Id && x.UserId == recipient.Id, TestContext.Current.CancellationToken)).Role);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Revoked_and_expired_invites_are_rejected(bool revoke)
    {
        var seed = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        var invitation = await Body<InvitationCreated>(await Send(client, path + "/invitations", new InvitationRequest("verified@example.invalid", ProjectRole.Member)), HttpStatusCode.Created);
        if (revoke) Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/invitations/" + invitation.Invitation.Id, TestContext.Current.CancellationToken)).StatusCode);
        else
        {
            await using var db = fixture.CreateContext();
            await db.Invitations.Where(x => x.Id == invitation.Invitation.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddDays(-1)), TestContext.Current.CancellationToken);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, "/api/invitations/accept", new AcceptInvitationRequest(invitation.Token))).StatusCode);
    }

    [Fact]
    public async Task Last_owner_invalid_assignee_and_invalid_inputs_are_rejected()
    {
        var seed = await Seed();
        var foreign = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, $"/api/workspaces/{seed.Workspace.Id}/members/{seed.User.Id}",
            new WorkspaceRoleRequest(WorkspaceRole.Member), put: true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, path + "/tasks", new TaskRequest("Foreign assignee", null, AssigneeUserId: foreign.User.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, path + "/tasks", new { title = " ", priority = "Medium", status = "ToDo" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, path + "/tasks", new { title = "Invalid", priority = "Unknown", status = "ToDo" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/tasks?pageSize=1000", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, $"/api/workspaces/{seed.Workspace.Id}/projects", new ProjectRequest("TEST", "Duplicate", null))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_task_creation_allocates_unique_numbers()
    {
        var seed = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        var responses = await Task.WhenAll(Enumerable.Range(1, 6).Select(i => Send(client, path + "/tasks", new TaskRequest("Parallel " + i, null))));
        var tasks = await Task.WhenAll(responses.Select(r => Body<TaskDto>(r, HttpStatusCode.Created)));
        Assert.Equal(Enumerable.Range(1, 6), tasks.Select(x => x.Number).Order());
    }

    [Fact]
    public async Task Archive_and_soft_delete_remove_write_access_and_hide_deleted_projects()
    {
        var seed = await Seed();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = Client(factory, seed.User.CognitoSubject);
        var path = Path(seed.Workspace.Id, seed.Project.Id);
        await Body<ProjectDto>(await Send(client, path, new ProjectRequest("TEST", "Archived", null, true), put: true));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, path + "/tasks", new TaskRequest("Blocked", null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/access", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/analytics", TestContext.Current.CancellationToken)).StatusCode);
    }
}
