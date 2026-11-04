using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DevFlow.Api.Authentication;

public sealed class BearerSecurityDocument : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Cognito access token from the configured public app client; ID tokens are rejected."
        };
        foreach (var (path, item) in document.Paths)
        {
            if (item.Operations is null) continue;
            foreach (var operation in item.Operations.Values)
                operation.Security = path.StartsWith("/api/health", StringComparison.Ordinal) ? []
                    : [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
        }
        return Task.CompletedTask;
    }
}
