using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IUserManagementService
{
    Task<Guid> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetTenantUsersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetManagersAsync(CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(Guid userId, ProfileUpdateRequest request, CancellationToken ct = default);
    Task RecordLoginAsync(Guid userId, CancellationToken ct = default);
}

public class UserManagementService : IUserManagementService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly ISubscriptionService _subscriptions;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserManagementService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IAuditLogger audit,
        ISubscriptionService subscriptions,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _subscriptions = subscriptions;
        _userManager = userManager;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<Guid> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await _subscriptions.AssertCanAddUsersAsync(request.Role, ct);

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            throw new DomainException("A user with this email already exists.");

        var userName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName;
        existing = await _userManager.FindByNameAsync(userName);
        if (existing != null)
            throw new DomainException("A user with this user name already exists.");

        var user = new ApplicationUser
        {
            TenantId = Tenant,
            FullName = request.FullName.Trim(),
            UserName = userName,
            Email = request.Email,
            MobileNumber = request.MobileNumber,
            Designation = request.Designation,
            EmployeeCode = request.EmployeeCode,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

        result = await _userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _audit.LogAsync("User Created", nameof(ApplicationUser), user.Id.ToString(), null, $"{request.FullName} ({request.Role})", ct);
        return user.Id;
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
        if (request.MobileNumber != null) user.MobileNumber = request.MobileNumber;
        if (request.Designation != null) user.Designation = request.Designation;
        if (request.EmployeeCode != null) user.EmployeeCode = request.EmployeeCode;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(request.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, request.Role);
            }
        }

        await _userManager.UpdateAsync(user);
        await _audit.LogAsync("User Updated", nameof(ApplicationUser), id.ToString(), null, user.FullName, ct);
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct)
            ?? throw new NotFoundException("User not found.");
        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync("User Deactivated", nameof(ApplicationUser), id.ToString(), null, user.FullName, ct);
    }

    public async Task<IReadOnlyList<UserDto>> GetTenantUsersAsync(CancellationToken ct = default)
    {
        var users = await _userManager.Users
            .Where(x => x.TenantId == Tenant)
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.FullName,
                user.MobileNumber, user.Designation, user.EmployeeCode, user.IsActive, roles.ToArray()));
        }
        return result;
    }

    public async Task<IReadOnlyList<UserDto>> GetManagersAsync(CancellationToken ct = default)
    {
        var users = await _userManager.Users
            .Where(x => x.TenantId == Tenant && x.IsActive)
            .ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Manager))
                result.Add(new UserDto(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.FullName,
                    user.MobileNumber, user.Designation, user.EmployeeCode, user.IsActive, roles.ToArray()));
        }
        return result;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == Tenant, ct);
        if (user == null) return null;
        var roles = await _userManager.GetRolesAsync(user);
        return new UserDto(user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.FullName,
            user.MobileNumber, user.Designation, user.EmployeeCode, user.IsActive, roles.ToArray());
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId && x.TenantId == Tenant, ct);
        if (user == null) return Array.Empty<string>();
        return (await _userManager.GetRolesAsync(user)).ToArray();
    }

    public async Task UpdateProfileAsync(Guid userId, ProfileUpdateRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");
        user.FullName = request.FullName?.Trim() ?? user.FullName;
        user.MobileNumber = request.MobileNumber;
        user.Designation = request.Designation;
        user.EmployeeCode = request.EmployeeCode;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync("Profile Updated", nameof(ApplicationUser), userId.ToString(), null, null, ct);
    }

    public async Task RecordLoginAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user == null) return;
        user.LastLoginAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync("Login", nameof(ApplicationUser), userId.ToString(), null, null, ct);
    }
}
