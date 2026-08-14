using IForm.Application.Common.Interfaces;
using IForm.Application.Services;
using IForm.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(INotificationService notifications, ICurrentUser currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!_currentUser.TenantId.HasValue || !_currentUser.UserId.HasValue)
            return RedirectToAction("Index", "Dashboard");
        var items = await _notifications.GetForCurrentUserAsync(_currentUser.TenantId.Value, _currentUser.UserId.Value, 200, ct);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        if (!_currentUser.TenantId.HasValue || !_currentUser.UserId.HasValue) return Ok();
        await _notifications.MarkReadAsync(id, _currentUser.TenantId.Value, _currentUser.UserId.Value, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!_currentUser.TenantId.HasValue || !_currentUser.UserId.HasValue) return Ok();
        await _notifications.MarkAllReadAsync(_currentUser.TenantId.Value, _currentUser.UserId.Value, ct);
        return Ok();
    }
}
