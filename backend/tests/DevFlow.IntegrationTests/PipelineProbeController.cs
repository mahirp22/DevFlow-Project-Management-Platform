using DevFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.IntegrationTests;

[ApiController]
[AllowAnonymous]
[Route("test/pipeline")]
public sealed class PipelineProbeController : ControllerBase
{
    [HttpGet("validation")]
    public IActionResult Validate([FromQuery] PageRequest request) => Ok(request);

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException("PRIVATE_PROVIDER_DETAILS");

    [HttpGet("conflict")]
    public IActionResult ConflictProbe() => throw new DbUpdateConcurrencyException("PRIVATE_PROVIDER_DETAILS");
}
