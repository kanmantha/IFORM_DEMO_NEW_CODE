using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantUsers")]
public class SubscriptionController : Controller
{
    private readonly ISubscriptionService _subscriptions;

    public SubscriptionController(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _subscriptions.GetTenantViewAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(Guid planId, CancellationToken ct)
    {
        try
        {
            await _subscriptions.ChangePlanAsync(planId, ct);
            TempData["Success"] = "Plan updated.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
