using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public class SuperAdminController : Controller
{
    private readonly ITenantService _tenants;
    private readonly ISubscriptionService _subscriptions;
    private readonly IDashboardService _dashboard;

    public SuperAdminController(ITenantService tenants, ISubscriptionService subscriptions, IDashboardService dashboard)
    {
        _tenants = tenants;
        _subscriptions = subscriptions;
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Tenants(CancellationToken ct)
    {
        return View(await _tenants.GetAllTenantsAsync(ct));
    }

    [HttpGet]
    public IActionResult CreateTenant() => View(new CreateTenantRequest(string.Empty, string.Empty, "TRIAL", string.Empty, string.Empty, string.Empty));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(request);
        try
        {
            var id = await _tenants.CreateTenantAsync(request, ct);
            TempData["Success"] = "Tenant created.";
            return RedirectToAction(nameof(Tenants));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        return View(request);
    }

    public async Task<IActionResult> Plans(CancellationToken ct)
    {
        return View(await _subscriptions.GetAllPlansAsync(ct));
    }

    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        return View(await _dashboard.GetSuperAdminDashboardAsync(ct));
    }
}
