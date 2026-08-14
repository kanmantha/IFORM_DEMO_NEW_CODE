using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantUsers")]
public class ProjectsController : Controller
{
    private readonly IProjectService _projects;
    private readonly IIpoService _ipos;
    private readonly IUserManagementService _users;

    public ProjectsController(IProjectService projects, IIpoService ipos, IUserManagementService users)
    {
        _projects = projects;
        _ipos = ipos;
        _users = users;
    }

    public async Task<IActionResult> Index(string? term, ProjectStatus? status, int page = 1, CancellationToken ct = default)
    {
        return View(await _projects.SearchAsync(term, status, page, 20, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(id, ct);
        if (project == null) return NotFound();
        ViewBag.Ipos = await _ipos.GetAllAsync(id, ct);
        return View(project);
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Managers = await _users.GetManagersAsync(ct);
        return View(new CreateProjectRequest());
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Managers = await _users.GetManagersAsync(ct);
            return View(request);
        }

        try
        {
            var id = await _projects.CreateAsync(request, ct);
            TempData["Success"] = "Project created.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DomainException || ex is PlanLimitExceededException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        ViewBag.Managers = await _users.GetManagersAsync(ct);
        return View(request);
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIpo(CreateIpoRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "IPO number and project are required.";
            return RedirectToAction(nameof(Details), new { id = request.ProjectId });
        }

        try
        {
            var id = await _ipos.CreateAsync(request, ct);
            TempData["Success"] = "IPO added.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = request.ProjectId });
    }

    public async Task<IActionResult> Ipos(Guid? projectId, string? term, CancellationToken ct)
    {
        ViewBag.Projects = await _projects.GetAllAsync(ct);
        var ipos = string.IsNullOrWhiteSpace(term) ? await _ipos.GetAllAsync(projectId, ct) : await _ipos.SearchAsync(term, ct);
        return View(ipos);
    }
}
