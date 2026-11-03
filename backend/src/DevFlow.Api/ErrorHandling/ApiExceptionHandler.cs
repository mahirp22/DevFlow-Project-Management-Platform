using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Api.ErrorHandling;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var conflict = exception is DbUpdateConcurrencyException;
        var status = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        // Log type and trace ID, not provider messages that might contain sensitive values.
        logger.Log(conflict ? LogLevel.Warning : LogLevel.Error,
            "Request failed with {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = conflict ? "The resource was modified by another request." : "An unexpected error occurred.",
            Detail = conflict ? "Reload the resource and try again." : null
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        var written = await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        if (!written)
            await context.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
