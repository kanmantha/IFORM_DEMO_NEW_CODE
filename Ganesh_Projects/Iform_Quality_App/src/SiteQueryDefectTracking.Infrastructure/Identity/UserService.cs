using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Auth;
using SiteQueryDefectTracking.Application.Exceptions;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Infrastructure.Identity;

public class UserService(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IAuditLogService auditLog,
    ICurrentUserService currentUser) : IUserService
{
    public async Task<CurrentUserDto> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);
        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        return new CurrentUserDto(
            user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty,
            user.FullName, user.PhoneNumber, roles);
    }

    public async Task<PagedResult<UserDto>> SearchAsync(string? keyword, string? role, int page, int pageSize, CancellationToken ct = default)
    {
        var users = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            users = users.Where(u =>
                (u.UserName != null && u.UserName.Contains(term))
                || (u.Email != null && u.Email.Contains(term))
                || (u.FirstName != null && u.FirstName.Contains(term))
                || (u.LastName != null && u.LastName.Contains(term)));
        }

        var total = await users.CountAsync(ct);
        var items = await users
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = new List<UserDto>();
        foreach (var user in items)
        {
            var roles = (await userManager.GetRolesAsync(user)).ToArray();
            dtos.Add(ToDto(user, roles));
        }

        return PagedResult<UserDto>.Create(dtos, total, page, pageSize);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await userManager.FindByNameAsync(request.UserName.Trim()) is not null)
            throw new BusinessException($"Username '{request.UserName}' is already taken.");
        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
            throw new BusinessException($"Email '{request.Email}' is already registered.");

        var nameParts = SplitFullName(request.FullName);
        var user = new User
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FirstName = nameParts.First,
            LastName = nameParts.Last,
            PhoneNumber = request.MobileNumber,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new BusinessException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        foreach (var role in request.Roles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, Domain.Constants.AuditActions.UserCreated, nameof(User), user.Id,
            null, $"{user.UserName} ({string.Join(",", request.Roles)})",
            currentUser.IpAddress, currentUser.DeviceInfo), ct);

        return ToDto(user, request.Roles);
    }

    public async Task<UserDto> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id)
            ?? throw new NotFoundException("User", id);

        var nameParts = SplitFullName(request.FullName);
        user.FirstName = nameParts.First;
        user.LastName = nameParts.Last;
        user.PhoneNumber = request.MobileNumber;
        user.IsActive = request.IsActive;

        await userManager.UpdateAsync(user);

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToAdd = request.Roles.Where(r => !currentRoles.Contains(r)).ToList();
        var rolesToRemove = currentRoles.Where(r => !request.Roles.Contains(r)).ToList();
        foreach (var role in rolesToRemove) await userManager.RemoveFromRoleAsync(user, role);
        foreach (var role in rolesToAdd) await userManager.AddToRoleAsync(user, role);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, Domain.Constants.AuditActions.UserUpdated, nameof(User), user.Id,
            string.Join(",", currentRoles), string.Join(",", request.Roles),
            currentUser.IpAddress, currentUser.DeviceInfo), ct);

        return ToDto(user, request.Roles);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("User", request.UserId);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new BusinessException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static UserDto ToDto(User user, IReadOnlyList<string> roles) => new(
        user.Id, user.UserName ?? string.Empty, user.Email ?? string.Empty,
        user.FullName, user.PhoneNumber, user.IsActive, roles, user.CreatedAt);

    private static (string? First, string? Last) SplitFullName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], string.Join(' ', parts.Skip(1)))
        };
    }
}