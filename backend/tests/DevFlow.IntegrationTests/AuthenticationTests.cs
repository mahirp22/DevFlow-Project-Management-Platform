using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevFlow.Application.Identity;
using DevFlow.Domain.Entities;
using DevFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class AuthenticationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Anonymous_requests_are_denied_and_health_stays_public()
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("client")]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("scope")]
    [InlineData("subject")]
    [InlineData("malformed")]
    public async Task Invalid_tokens_cannot_access_profiles(string failure)
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var token = failure == "malformed" ? "not-a-jwt" : TestTokens.Create(
            failure == "subject" ? "invalid-subject" : Guid.NewGuid().ToString(), failure);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Profile_initialization_is_idempotent_and_grants_no_memberships()
    {
        var subject = Guid.NewGuid().ToString();
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/me", TestContext.Current.CancellationToken)).StatusCode);
        using var first = await client.PostAsJsonAsync("/api/me", new { subject = "attacker-selected", email = "unverified@example.invalid", role = "Owner" }, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync("/api/me", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<ProfileResponse>(TestContext.Current.CancellationToken);
        var b = await second.Content.ReadFromJsonAsync<ProfileResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(a, b);
        Assert.Equal("verified@example.invalid", a!.Email);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(x => x.CognitoSubject == subject, TestContext.Current.CancellationToken));
        Assert.False(await db.WorkspaceMemberships.AnyAsync(x => x.UserId == a!.Id, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner, null, true, true)]
    [InlineData(WorkspaceRole.Admin, null, true, true)]
    [InlineData(WorkspaceRole.Member, ProjectRole.Manager, true, true)]
    [InlineData(WorkspaceRole.Member, ProjectRole.Member, true, false)]
    [InlineData(WorkspaceRole.Member, ProjectRole.Viewer, false, false)]
    public async Task Project_permissions_come_from_database_roles(WorkspaceRole workspaceRole, ProjectRole? projectRole, bool write, bool manage)
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var user = new UserProfile(Guid.NewGuid().ToString(), "Member", "member@example.invalid");
        db.AddRange(user, new WorkspaceMembership(seed.Workspace.Id, user.Id, workspaceRole));
        if (projectRole is not null) db.Add(new ProjectMembership(seed.Workspace.Id, seed.Project.Id, user.Id, projectRole.Value));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(user.CognitoSubject));
        var response = await client.GetAsync($"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}/access", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ResourceAccess(true, write, manage), await response.Content.ReadFromJsonAsync<ResourceAccess>(TestContext.Current.CancellationToken));
        var probe = $"/test/authorization/{seed.Workspace.Id}/projects/{seed.Project.Id}/";
        Assert.Equal(write ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden,
            (await client.PostAsync(probe + "write", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(manage ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden,
            (await client.PostAsync(probe + "manage", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Foreign_tenants_and_mismatched_project_paths_are_hidden()
    {
        await using var db = fixture.CreateContext();
        var own = await TestData.AddProjectAsync(db);
        var other = await TestData.AddProjectAsync(db);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(own.User.CognitoSubject));
        foreach (var path in new[] {
            $"/api/workspaces/{other.Workspace.Id}/access",
            $"/api/workspaces/{own.Workspace.Id}/projects/{other.Project.Id}/access",
            $"/api/workspaces/{other.Workspace.Id}/projects/{other.Project.Id}/access" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Workspace_members_need_project_membership_and_revocation_is_immediate()
    {
        await using var db = fixture.CreateContext();
        var seed = await TestData.AddProjectAsync(db);
        var user = new UserProfile(Guid.NewGuid().ToString(), "Viewer", "viewer@example.invalid");
        db.AddRange(user, new WorkspaceMembership(seed.Workspace.Id, user.Id, WorkspaceRole.Member));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(user.CognitoSubject));
        var path = $"/api/workspaces/{seed.Workspace.Id}/projects/{seed.Project.Id}/access";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        var membership = new ProjectMembership(seed.Workspace.Id, seed.Project.Id, user.Id, ProjectRole.Viewer);
        db.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        db.Remove(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
    }
}

internal sealed class TestIdentityDirectory : IIdentityDirectory
{
    public Task<VerifiedIdentity?> GetVerifiedAsync(string accessToken, string subject, CancellationToken cancellationToken) =>
        Task.FromResult<VerifiedIdentity?>(new(subject, "verified@example.invalid", "Verified user"));
}
