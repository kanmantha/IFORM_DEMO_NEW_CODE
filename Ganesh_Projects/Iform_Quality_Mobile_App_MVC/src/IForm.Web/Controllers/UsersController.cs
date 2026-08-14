using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantAdmin")]
public class UsersController : Controller
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users) => _users = users;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _users.GetTenantUsersAsync(ct));
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserRequest { Role = AppRoles.SiteEngineer });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(request);

        try
        {
            await _users.CreateUserAsync(request, ct);
            TempData["Success"] = "User created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DomainException || ex is PlanLimitExceededException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user != null)
            await _users.UpdateUserAsync(id, new UpdateUserRequest { IsActive = !user.IsActive }, ct);
        TempData["Success"] = "User status updated.";
        return RedirectToAction(nameof(Index));
    }
}
