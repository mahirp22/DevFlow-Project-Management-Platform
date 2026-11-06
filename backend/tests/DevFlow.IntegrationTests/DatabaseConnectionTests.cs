using DevFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DevFlow.IntegrationTests;

public sealed class DatabaseConnectionTests
{
    [Fact]
    public void Rds_configuration_requires_verified_tls_and_preserves_password_punctuation()
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:Host"] = "example.ca-central-1.rds.amazonaws.com",
            ["Database:Username"] = "devflow_app",
            ["Database:Password"] = "test;Password=not-an-injected-setting",
            ["Database:RootCertificate"] = "/app/certs/rds.pem"
        };
        var connection = new NpgsqlConnectionStringBuilder(DatabaseConnection.Resolve(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        Assert.Equal(SslMode.VerifyFull, connection.SslMode);
        Assert.Equal(GssEncryptionMode.Disable, connection.GssEncryptionMode);
        Assert.Equal(values["Database:Password"], connection.Password);
        Assert.Equal("devflow_app", connection.Username);
        Assert.Equal("/app/certs/rds.pem", connection.RootCertificate);
        Assert.False(connection.IncludeErrorDetail);
    }

    [Fact]
    public void Missing_cloud_secret_fails_without_including_other_configuration_values()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Host"] = "database", ["Database:Username"] = "devflow_app"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.Resolve(config));
    }

    [Fact]
    public void Existing_local_connection_configuration_is_preserved()
    {
        const string local = "Host=localhost;Database=local_test";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DevFlow"] = local }).Build();
        Assert.Equal(local, DatabaseConnection.Resolve(config));
    }
}
