namespace SiteQueryDefectTracking.Application.DTOs.Auth;

public class LoginRequest
{
    public string UserNameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string TokenType = "Bearer");

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public record CurrentUserDto(
    string Id,
    string UserName,
    string Email,
    string FullName,
    string? MobileNumber,
    IReadOnlyList<string> Roles);

public record UserDto(
    string Id,
    string UserName,
    string Email,
    string FullName,
    string? MobileNumber,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class ResetPasswordRequest
{
    public string UserId { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}