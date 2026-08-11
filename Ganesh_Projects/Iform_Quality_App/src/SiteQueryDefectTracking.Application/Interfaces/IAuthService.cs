using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Auth;

namespace SiteQueryDefectTracking.Application.Interfaces;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);

    Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);

    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public interface IUserService
{
    Task<CurrentUserDto> GetCurrentUserAsync(string userId, CancellationToken ct = default);

    Task<PagedResult<UserDto>> SearchAsync(string? keyword, string? role, int page, int pageSize, CancellationToken ct = default);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserDto> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}