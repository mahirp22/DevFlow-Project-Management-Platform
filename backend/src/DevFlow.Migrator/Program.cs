using DevFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

// One-off ECS task, never part of API startup. Do not log connection strings or SQL.
try
{
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var connectionString = DatabaseConnection.Resolve(configuration);
    var applicationPassword = configuration["Database:ApplicationPassword"];
    if (string.IsNullOrWhiteSpace(applicationPassword))
        throw new InvalidOperationException("Application password is required.");

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    // Serialize bootstrap as well as EF migrations if a deployment is accidentally repeated.
    await using var deploymentLock = new NpgsqlCommand("SELECT pg_advisory_lock(81462283)", connection);
    await deploymentLock.ExecuteNonQueryAsync();
    await using var database = new DevFlowDbContext(new DbContextOptionsBuilder<DevFlowDbContext>()
        .UseNpgsql(connection).Options);
    await database.Database.MigrateAsync();

    await using var transaction = await connection.BeginTransactionAsync();
    await using (var ensureRole = new NpgsqlCommand("""
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'devflow_app') THEN
            CREATE ROLE devflow_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE;
          END IF;
        END $$;
        """, connection, transaction))
        await ensureRole.ExecuteNonQueryAsync();
    // PostgreSQL format(%L) safely quotes the password; utility DDL cannot bind it directly.
    await using (var quote = new NpgsqlCommand("SELECT format('ALTER ROLE devflow_app PASSWORD %L', @password)", connection, transaction))
    {
        quote.Parameters.AddWithValue("password", applicationPassword);
        var sql = (string)(await quote.ExecuteScalarAsync())!;
        await using var setPassword = new NpgsqlCommand(sql, connection, transaction);
        await setPassword.ExecuteNonQueryAsync();
    }
    await using (var grants = new NpgsqlCommand("""
        REVOKE CREATE ON SCHEMA public FROM PUBLIC;
        GRANT CONNECT ON DATABASE devflow TO devflow_app;
        GRANT USAGE ON SCHEMA public TO devflow_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO devflow_app;
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO devflow_app;
        REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM devflow_app;
        GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO devflow_app;
        """, connection, transaction))
        await grants.ExecuteNonQueryAsync();
    await transaction.CommitAsync();
    Console.WriteLine("Database migrations and application grants completed.");
    return 0;
}
catch (Exception exception)
{
    // Error messages from providers can contain submitted SQL or configuration.
    Console.Error.WriteLine($"Migration failed ({exception.GetType().Name}); no service rollout should proceed.");
    return 1;
}
