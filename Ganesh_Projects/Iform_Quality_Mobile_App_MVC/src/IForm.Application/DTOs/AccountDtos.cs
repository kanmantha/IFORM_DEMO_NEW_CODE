using System.ComponentModel.DataAnnotations;

namespace IForm.Application.DTOs;

public record LoginRequest(string UserName, string Password, bool RememberMe = false);

public record UserDto(Guid Id, string UserName, string Email, string FullName, string? MobileNumber, string? Designation, string? EmployeeCode, bool IsActive, IReadOnlyList<string> Roles);

public class CreateUserRequest
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string UserName { get; set; } = string.Empty;
    [Required] [MinLength(8)] public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "SiteEngineer";
    public string? MobileNumber { get; set; }
    public string? Designation { get; set; }
    public string? EmployeeCode { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? MobileNumber { get; set; }
    public string? Designation { get; set; }
    public string? EmployeeCode { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ProfileUpdateRequest(string FullName, string? MobileNumber, string? Designation, string? EmployeeCode);
