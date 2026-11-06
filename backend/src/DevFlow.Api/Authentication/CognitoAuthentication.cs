using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace DevFlow.Api.Authentication;

public static class CognitoAuthentication
{
    public static IServiceCollection AddCognitoAuthentication(this IServiceCollection services,
        IConfiguration configuration, bool development)
    {
        var settings = configuration.GetSection("Cognito").Get<CognitoSettings>() ?? new();
        settings.Validate(development);
        services.AddSingleton(settings);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.SaveToken = true;
            options.IncludeErrorDetails = false;
            options.RequireHttpsMetadata = true;
            if (settings.IsConfigured) options.Authority = settings.Authority.TrimEnd('/');
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.IsConfigured ? settings.Authority.TrimEnd('/') : "urn:devflow:unconfigured",
                // Cognito access tokens use client_id instead of the ID-token aud.
                // The event below enforces the exact public client and access-token type.
                ValidateAudience = false,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub"
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Path == "/api/hubs/projects"
                        && context.Request.Query.TryGetValue("access_token", out var token))
                        context.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var user = context.Principal!;
                    var subject = user.FindFirst("sub")?.Value;
                    var scopes = (user.FindFirst("scope")?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (!settings.IsConfigured || user.FindFirst("token_use")?.Value != "access"
                        || user.FindFirst("client_id")?.Value != settings.ClientId
                        || !Guid.TryParse(subject, out _) || !scopes.Contains("openid"))
                        context.Fail("Invalid access token.");
                    return Task.CompletedTask;
                }
            };
        });
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthorizationHandler, ProjectPermissionHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in new[] { "read", "write", "manage" })
                options.AddPolicy("project:" + permission, policy =>
                    policy.RequireAuthenticatedUser().AddRequirements(new ProjectPermission(permission)));
        });
        return services;
    }
}
