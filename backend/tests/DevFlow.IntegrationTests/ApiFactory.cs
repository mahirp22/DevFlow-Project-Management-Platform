using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DevFlow.Application.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
namespace DevFlow.IntegrationTests;

internal sealed class ApiFactory(string connectionString, string environment = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:DevFlow", connectionString);
        builder.UseSetting("Cognito:Authority", TestTokens.Issuer);
        builder.UseSetting("Cognito:ClientId", TestTokens.ClientId);
        builder.UseSetting("Cognito:Domain", "https://devflow-test.auth.us-east-1.amazoncognito.com");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        // Only the test host sees these controller endpoints; they are not in the API assembly.
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(PipelineProbeController).Assembly));
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                configuration.SigningKeys.Add(TestTokens.Key);
                options.Configuration = configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
            services.AddScoped<IIdentityDirectory, TestIdentityDirectory>();
        });
    }
}
