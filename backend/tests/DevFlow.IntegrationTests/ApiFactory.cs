using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace DevFlow.IntegrationTests;

internal sealed class ApiFactory(string connectionString, string environment = "Development") : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:DevFlow", connectionString);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        // Only the test host sees these controller endpoints; they are not in the API assembly.
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(PipelineProbeController).Assembly));
    }
}
