using System.Security.Claims;
using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IForm.Web.Middleware;

/// <summary>
/// Resolves the authenticated tenant and exposes its display name, subscription
/// status and plan tier to the request pipeline (used by the layout and by the
/// query filter seeding). Unauthenticated requests pass through untouched.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IApplicationDbContext db)
    {
        if (currentUser.IsAuthenticated && currentUser.TenantId.HasValue)
        {
            var tenantId = currentUser.TenantId.Value;
            context.Items["TenantId"] = tenantId;

            if (tenantId != Guid.Empty)
            {
                var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant != null)
                {
                    context.Items["TenantName"] = tenant.Name;
                    context.Items["TenantSlug"] = tenant.Slug;
                }

                var subscription = await db.Subscriptions
                    .Include(s => s.Plan)
                    .AsNoTracking()
                    .OrderByDescending(s => s.StartDate)
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId);

                if (subscription != null)
                {
                    context.Items["PlanName"] = subscription.Plan?.PlanName ?? "N/A";
                    context.Items["PlanTier"] = subscription.Plan?.Tier ?? PlanTier.Free;
                    context.Items["SubscriptionStatus"] = subscription.Status;
                }
            }
        }

        await _next(context);
    }
}
