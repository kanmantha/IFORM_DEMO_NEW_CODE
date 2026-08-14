using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantUsers")]
public class EotController : Controller
{
    private readonly IEotService _eots;
    private readonly IDocumentService _documents;
    private readonly IProjectService _projects;
    private readonly IFileStorageService _storage;

    public EotController(IEotService eots, IDocumentService documents, IProjectService projects, IFileStorageService storage)
    {
        _eots = eots;
        _documents = documents;
        _projects = projects;
        _storage = storage;
    }

    public async Task<IActionResult> Index(Guid? projectId, EotStatus? status, CancellationToken ct)
    {
        ViewBag.Projects = await _projects.GetAllAsync(ct);
        return View(await _eots.GetAllAsync(projectId, status, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var dto = await _eots.GetByIdAsync(id, ct);
        if (dto == null) return NotFound();
        return View(dto);
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Projects = await _projects.GetAllAsync(ct);
        return View(new CreateEotRequest { FinancialYear = DateTime.UtcNow.Year.ToString(), Scenario = EotScenario.Sc3ProductionNotStarted, Category = EotCategory.DesignRevision });
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEotRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Projects = await _projects.GetAllAsync(ct);
            return View(request);
        }

        try
        {
            var id = await _eots.CreateAsync(request, ct);
            TempData["Success"] = $"EOT {request.ClientEotNumber} created.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DomainException || ex is PlanLimitExceededException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        ViewBag.Projects = await _projects.GetAllAsync(ct);
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScopeVariation(Guid id, AddScopeVariationRequest request, CancellationToken ct)
    {
        try
        {
            await _eots.AddScopeVariationAsync(id, request, ct);
            TempData["Success"] = "Scope variation added.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        try
        {
            await _eots.SubmitAsync(id, ct);
            TempData["Success"] = "EOT submitted for review.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(Guid id, EotStatus newStatus, string? remarks, CancellationToken ct)
    {
        try
        {
            await _eots.TransitionAsync(id, new EotTransitionRequest(newStatus, remarks), ct);
            TempData["Success"] = $"Status changed to {newStatus}.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocument(Guid id, DocumentCategory category, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var documentId = await _documents.UploadEotDocumentAsync(id, category, file.FileName, file.ContentType, ms.ToArray(), ct);
            TempData["Success"] = "Document uploaded.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
