using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevFlow.Infrastructure.Persistence;

public static class DatabaseConnection
{
    // Keep local User Secrets compatible; ECS uses separately injected secret fields.
    public static string Resolve(IConfiguration configuration)
    {
        var local = configuration.GetConnectionString("DevFlow");
        if (!string.IsNullOrWhiteSpace(local)) return local;
        string Required(string key) => !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : throw new InvalidOperationException($"Configure {key} or ConnectionStrings:DevFlow.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = Required("Database:Host"),
            Port = 5432,
            Database = "devflow",
            Username = Required("Database:Username"),
            Password = Required("Database:Password"),
            SslMode = SslMode.VerifyFull,
            GssEncryptionMode = GssEncryptionMode.Disable,
            RootCertificate = Required("Database:RootCertificate"),
            IncludeErrorDetail = false
        }.ConnectionString;
    }
}
