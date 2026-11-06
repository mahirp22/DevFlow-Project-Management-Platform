using DevFlow.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(IUserProfiles profiles, IIdentityDirectory directory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileResponse>(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(User.FindFirst("sub")!.Value, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    [ProducesResponseType<ProfileResponse>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProfileResponse>> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RenameAsync(User.FindFirst("sub")!.Value, request.DisplayName, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType<ProfileResponse>(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<ProfileResponse>> Initialize(CancellationToken cancellationToken)
    {
        var token = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(token)) return Unauthorized();
        var identity = await directory.GetVerifiedAsync(token, User.FindFirst("sub")!.Value, cancellationToken);
        if (identity is null) return Forbid();
        return Ok(await profiles.EnsureAsync(identity, cancellationToken));
    }
}
