using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Auth;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AppPolicies.RequireManager)]
public class UsersController(IUserService services) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await services.SearchAsync(keyword, role, page, pageSize, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await services.CreateAsync(request, ct);
        return Ok(ApiResponse.Ok(result, "User created."));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update([FromRoute] string id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await services.UpdateAsync(id, request, ct);
        return Ok(ApiResponse.Ok(result, "User updated."));
    }

    [HttpPost("{id}/reset-password")]
    public async Task<ActionResult<ApiResponse<object?>>> ResetPassword([FromRoute] string id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (id != request.UserId)
        {
            return BadRequest(ApiResponse.Fail("Route and payload user ids must match."));
        }

        await services.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse.Ok("Password reset."));
    }
}