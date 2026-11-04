using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace DevFlow.IntegrationTests;

[Collection("PostgreSQL")]
public sealed class ApiTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Ready_returns_200_after_migrations()
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unmigrated_database_is_not_ready_until_explicit_migration()
    {
        var connectionString = await fixture.CreateDatabaseAsync(migrate: false);
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken)).StatusCode);
        await using var db = fixture.CreateContext(connectionString);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)); // API must not auto-migrate.
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Database_outage_does_not_change_liveness_or_expose_connection_details()
    {
        var unavailable = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        { Database = "devflow_missing_" + Guid.NewGuid().ToString("N"), Timeout = 1 };
        await using var factory = new ApiFactory(unavailable.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health", TestContext.Current.CancellationToken)).StatusCode);
        var response = await client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(unavailable.Database, body);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Page=0")]
    [InlineData("PageSize=101")]
    [InlineData("PageSize=0")]
    [InlineData("Page=not-a-number")]
    public async Task Invalid_query_returns_validation_problem_details(string query)
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/test/pipeline/validation?" + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Theory]
    [InlineData("unexpected", HttpStatusCode.InternalServerError)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    public async Task Exceptions_return_safe_problem_details(string route, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/test/pipeline/" + route, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("PRIVATE_PROVIDER_DETAILS", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task Exception_fallback_preserves_problem_content_type_and_trace_id()
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        var response = await client.GetAsync("/test/pipeline/unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("PRIVATE_PROVIDER_DETAILS", body);
    }

    [Fact]
    public async Task Anonymous_unknown_route_returns_401_problem_details()
    {
        await using var factory = new ApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/not-a-route", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task OpenApi_describes_health_and_is_disabled_in_production()
    {
        await using var development = new ApiFactory(fixture.ConnectionString);
        using var devClient = development.CreateClient();
        var response = await devClient.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/api/health/ready", out _));
        Assert.True(json.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        Assert.Equal(1, json.RootElement.GetProperty("paths").GetProperty("/api/me").GetProperty("get").GetProperty("security").GetArrayLength());
        await using var production = new ApiFactory(fixture.ConnectionString, "Production");
        using var prodClient = production.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await prodClient.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode);
    }
}
