using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface ITenantService
{
    Task<Guid> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TenantListItemDto>> GetAllTenantsAsync(CancellationToken ct = default);
    Task<Tenant?> GetTenantAsync(Guid id, CancellationToken ct = default);
    Task SetTenantStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default);
    Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken ct = default);
    Task SetSettingAsync(Guid tenantId, string key, string value, CancellationToken ct = default);
    Task<string?> GetSettingAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<Guid?> GetTenantIdByDomainAsync(string host, CancellationToken ct = default);
}

public class TenantService : ITenantService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;
    private readonly ISubscriptionService _subscriptions;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public TenantService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IAuditLogger audit,
        ISubscriptionService subscriptions,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _subscriptions = subscriptions;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<Guid> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var slug = await GenerateUniqueSlugAsync(request.Name, ct);

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Email = request.Email,
            Status = TenantStatus.Trial,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName ?? "system"
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanName.ToLower() == request.PlanName.ToLower() && p.IsActive, ct)
            ?? await _db.SubscriptionPlans.OrderBy(p => p.Price).FirstOrDefaultAsync(ct)
            ?? throw new DomainException("No subscription plans are configured.");

        await _subscriptions.StartTrialAsync(tenant.Id, plan.Id, ct);

        var admin = new ApplicationUser
        {
            TenantId = tenant.Id,
            FullName = request.TenantAdminName.Trim(),
            UserName = request.TenantAdminEmail,
            Email = request.TenantAdminEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(admin, request.TenantAdminPassword);
        if (!result.Succeeded)
        {
            tenant.Status = TenantStatus.Inactive;
            await _db.SaveChangesAsync(ct);
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(admin, AppRoles.TenantAdmin);

        _db.TenantSettings.Add(new TenantSetting
        {
            TenantId = tenant.Id,
            Key = "features",
            Value = "{}"
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Tenant Created", nameof(Tenant), tenant.Id.ToString(), null, request.Name, ct);
        return tenant.Id;
    }

    public async Task<IReadOnlyList<TenantListItemDto>> GetAllTenantsAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.AsNoTracking().ToListAsync(ct);
        var subscriptions = await _db.Subscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .ToListAsync(ct);
        var userCounts = await _db.Users
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);
        var projectCounts = await _db.Projects
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, ct);

        return tenants.Select(t =>
        {
            var sub = subscriptions.FirstOrDefault(s => s.TenantId == t.Id);
            return new TenantListItemDto(t.Id, t.Name, t.Slug, t.Email ?? string.Empty, t.Status,
                sub?.Plan?.PlanName, sub?.Status ?? SubscriptionStatus.Inactive,
                userCounts.TryGetValue(t.Id, out var uc) ? uc : 0,
                projectCounts.TryGetValue(t.Id, out var pc) ? pc : 0, t.CreatedAt);
        }).ToList();
    }

    public async Task<Tenant?> GetTenantAsync(Guid id, CancellationToken ct = default) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task SetTenantStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tenant not found.");
        var old = tenant.Status;
        tenant.Status = status;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Tenant Status Changed", nameof(Tenant), id.ToString(), old.ToString(), status.ToString(), ct);
    }

    public async Task<Tenant?> GetTenantBySlugAsync(string slug, CancellationToken ct = default) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task SetSettingAsync(Guid tenantId, string key, string value, CancellationToken ct = default)
    {
        var setting = await _db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key, ct);
        if (setting == null)
        {
            _db.TenantSettings.Add(new TenantSetting { TenantId = tenantId, Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetSettingAsync(Guid tenantId, string key, CancellationToken ct = default) =>
        await _db.TenantSettings
            .Where(s => s.TenantId == tenantId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> GetTenantIdByDomainAsync(string host, CancellationToken ct = default)
    {
        var slug = host.Split('.').FirstOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug && (t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial), ct);
        return tenant?.Id;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = new string(name.ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .Select(c => c)
            .ToArray());
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "tenant";
        if (baseSlug.Length > 30) baseSlug = baseSlug[..30];

        var slug = baseSlug;
        var counter = 1;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug, ct))
        {
            slug = $"{baseSlug}{counter++}";
        }
        return slug;
    }
}
