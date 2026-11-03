using DevFlow.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace DevFlow.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Live() => Ok(new HealthResponse("Healthy"));

    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Ready(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(x => x.Tags.Contains("ready"), cancellationToken);
        var healthy = report.Status == HealthStatus.Healthy;
        return StatusCode(healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new HealthResponse(healthy ? "Healthy" : "Unhealthy"));
    }
}
