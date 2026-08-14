using IForm.Application.Services;
using IForm.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUser _currentUser;

    public DashboardController(IDashboardService dashboard, ICurrentUser currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (_currentUser.IsInRole("SuperAdmin"))
            return View("SuperAdmin", await _dashboard.GetSuperAdminDashboardAsync(ct));

        if (_currentUser.IsInRole("Manager") || _currentUser.IsInRole("TenantAdmin"))
            return View("Manager", await _dashboard.GetManagerDashboardAsync(ct));

        return View("Engineer", await _dashboard.GetManagerDashboardAsync(ct));
    }
}
