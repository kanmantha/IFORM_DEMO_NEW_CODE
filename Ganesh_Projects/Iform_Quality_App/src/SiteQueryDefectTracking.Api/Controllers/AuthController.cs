using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Auth;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IUserService users, ICurrentUserService currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, currentUser.IpAddress, currentUser.DeviceInfo, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request, currentUser.IpAddress, currentUser.DeviceInfo, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object?>>> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(request, ct);
        return Ok(ApiResponse.Ok("Logged out."));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object?>>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(currentUser.UserId!, request, ct);
        return Ok(ApiResponse.Ok("Password changed."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken ct)
    {
        var result = await users.GetCurrentUserAsync(currentUser.UserId!, ct);
        return Ok(ApiResponse.Ok(result));
    }
}