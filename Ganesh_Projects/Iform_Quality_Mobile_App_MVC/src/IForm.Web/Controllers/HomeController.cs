using IForm.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ICurrentUser currentUser, ILogger<HomeController> logger)
    {
        _currentUser = currentUser;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult WorkPermit()
    {
        return UnderConstruction("Work Permit");
    }

    public IActionResult Quality()
    {
        return UnderConstruction("Quality");
    }

    public IActionResult Safety()
    {
        return UnderConstruction("Safety");
    }

    public IActionResult UnderConstruction(string feature)
    {
        ViewData["Feature"] = feature;
        ViewData["Title"] = $"{feature} - Under Construction";
        return View("UnderConstruction");
    }

    [AllowAnonymous]
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue)
            ViewData["StatusCode"] = statusCode.Value;
        return View();
    }
}
