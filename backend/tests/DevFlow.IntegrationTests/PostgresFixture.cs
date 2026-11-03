using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
namespace DevFlow.IntegrationTests;

// External mode requires a disposable PostgreSQL server with CREATEDB permission.
// Every run creates randomly named databases; it never migrates/deletes a supplied database.
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string adminConnectionString = string.Empty;
    private readonly List<string> databases = [];
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("DEVFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(external))
        {
            container = new PostgreSqlBuilder("postgres:18.6")
                .WithPassword(Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))
                .Build();
            await container.StartAsync();
            external = container.GetConnectionString();
        }
        var admin = new NpgsqlConnectionStringBuilder(external) { Database = "postgres", Pooling = false };
        adminConnectionString = admin.ConnectionString;
        ConnectionString = await CreateDatabaseAsync(migrate: true);
    }

    public DevFlowDbContext CreateContext(string? connectionString = null) => new(
        new DbContextOptionsBuilder<DevFlowDbContext>().UseNpgsql(connectionString ?? ConnectionString).Options);

    public async Task<string> CreateDatabaseAsync(bool migrate)
    {
        // Identifier comes exclusively from a generated GUID, not user input.
        var name = "devflow_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        databases.Add(name);
        var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name }.ConnectionString;
        if (migrate)
        {
            await using var db = CreateContext(connectionString);
            await db.Database.MigrateAsync();
        }
        return connectionString;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var name in databases)
            {
                await using var connection = new NpgsqlConnection(adminConnectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (container is not null) await container.DisposeAsync();
        }
    }
}

[CollectionDefinition("PostgreSQL")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }
