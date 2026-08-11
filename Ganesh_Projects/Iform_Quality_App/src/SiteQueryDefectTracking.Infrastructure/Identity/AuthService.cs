using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Auth;
using SiteQueryDefectTracking.Application.Exceptions;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Infrastructure.Authentication;

namespace SiteQueryDefectTracking.Infrastructure.Authentication;

public class AuthService(
    UserManager<User> userManager,
    TokenService tokenService,
    IApplicationDbContext context,
    IAuditLogService auditLog) : IAuthService
{
    public async Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var user = await FindUserAsync(request.UserNameOrEmail);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            await auditLog.RecordAsync(new AuditLogEntry(
                null, AuditActions.LoginFailed, nameof(User), null,
                null, request.UserNameOrEmail, ipAddress, deviceInfo), ct);
            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("This account is disabled. Contact your administrator.");
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        var pair = await IssueTokenPairAsync(user, roles, ipAddress, ct);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        await auditLog.RecordAsync(new AuditLogEntry(
            user.Id, AuditActions.Login, nameof(User), user.Id,
            null, null, ipAddress, deviceInfo), ct);

        return pair;
    }

    public async Task<TokenResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var hash = TokenService.Hash(request.RefreshToken);
        var stored = await context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive || stored.User is null)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var user = stored.User;
        if (!user.IsActive)
            throw new UnauthorizedException("This account is disabled.");

        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        var pair = await IssueTokenPairAsync(user, roles, ipAddress, ct, revoke: stored);

        await auditLog.RecordAsync(new AuditLogEntry(
            user.Id, AuditActions.RefreshTokenUsed, nameof(RefreshToken), stored.Id.ToString(),
            null, null, ipAddress, deviceInfo), ct);

        return pair;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var hash = TokenService.Hash(request.RefreshToken);
        var stored = await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new BusinessException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task<TokenResponse> IssueTokenPairAsync(
        User user, IReadOnlyList<string> roles, string? ip, CancellationToken ct,
        RefreshToken? revoke = null)
    {
        var (accessToken, expiresIn) = tokenService.CreateAccessToken(user, roles);

        var refreshValue = tokenService.CreateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenService.Hash(refreshValue),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(tokenService.RefreshTokenDays),
            IpAddress = ip
        };
        if (revoke is not null)
        {
            revoke.RevokedAt = DateTimeOffset.UtcNow;
            revoke.ReplacedByTokenHash = entity.TokenHash;
        }

        context.RefreshTokens.Add(entity);
        await context.SaveChangesAsync(ct);

        return new TokenResponse(accessToken, refreshValue, expiresIn);
    }

    private async Task<User?> FindUserAsync(string userNameOrEmail)
    {
        var byName = await userManager.FindByNameAsync(userNameOrEmail);
        if (byName is not null) return byName;

        return await userManager.FindByEmailAsync(userNameOrEmail);
    }
}