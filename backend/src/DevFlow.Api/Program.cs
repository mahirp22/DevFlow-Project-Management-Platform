using DevFlow.Api.Collaboration;
using DevFlow.Api.ErrorHandling;
using DevFlow.Api.Health;
using DevFlow.Api.Authentication;
using DevFlow.Application.Identity;
using DevFlow.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
// Browser WebSocket authentication uses a query token; suppress request URL logging.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddSingleton<ProjectConnections>();
builder.Services.AddScoped<ProjectNotifications>();
builder.Services.AddScoped<ProjectNotificationFilter>();
builder.Services.AddSignalR(options => { options.MaximumReceiveMessageSize = 4096; options.EnableDetailedErrors = false; });
builder.Services.AddControllers(options => options.Filters.AddService<ProjectNotificationFilter>()).AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityDocument>());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCognitoAuthentication(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddHttpClient<IIdentityDirectory, CognitoIdentityDirectory>(client =>
    client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<DevFlow.Api.Attachments.AttachmentCleanup>();
builder.Services.AddHealthChecks().AddCheck<PostgreSqlReadinessCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapControllers();
app.MapHub<ProjectHub>("/api/hubs/projects", options => options.CloseOnAuthenticationExpiration = true);
app.Run();

public partial class Program { }
