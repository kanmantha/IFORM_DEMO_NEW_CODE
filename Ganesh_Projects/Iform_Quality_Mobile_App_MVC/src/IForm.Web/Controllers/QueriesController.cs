using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantUsers")]
public class QueriesController : Controller
{
    private readonly IQueryService _queries;
    private readonly IProjectService _projects;
    private readonly IIpoService _ipos;
    private readonly IProductService _products;

    public QueriesController(IQueryService queries, IProjectService projects, IIpoService ipos, IProductService products)
    {
        _queries = queries;
        _projects = projects;
        _ipos = ipos;
        _products = products;
    }

    public async Task<IActionResult> Index(
        string? searchTerm, Guid? projectId, Guid? ipoId, Guid? productId, IssueType? issueType,
        QueryStatus? status, Guid? raisedById, int? minDelay, int? maxDelay, DateTime? dateFrom, DateTime? dateTo,
        bool myQueries, string sortBy = "delay", bool sortDescending = true, int page = 1, CancellationToken ct = default)
    {
        var request = new QuerySearchRequest(
            SearchTerm: searchTerm, ProjectId: projectId, IpoId: ipoId, ProductId: productId,
            IssueType: issueType, Status: status, RaisedById: raisedById,
            MinDelayDays: minDelay, MaxDelayDays: maxDelay, DateFrom: dateFrom, DateTo: dateTo,
            MyQueries: myQueries, SortBy: sortBy, SortDescending: sortDescending, Page: page, PageSize: 20);

        await PopulateFilterDataAsync(projectId, ct);

        return View(await _queries.SearchAsync(request, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateFormDataAsync(ct);
        return View(new CreateQueryRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateQueryRequest request, List<IFormFile>? photos, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateFormDataAsync(ct);
            return View(request);
        }

        try
        {
            var photoTuples = await ReadPhotosAsync(photos, ct);
            var id = await _queries.CreateAsync(request, photoTuples, ct);
            TempData["Success"] = "Query registered successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is PlanLimitExceededException || ex is DomainException || ex is NotFoundException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await PopulateFormDataAsync(ct);
        return View(request);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var dto = await _queries.GetByIdAsync(id, ct);
        if (dto == null) return NotFound();
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid id, string body, CancellationToken ct)
    {
        try
        {
            await _queries.AddCommentAsync(id, body, ct);
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(Guid id, QueryStatus newStatus, string? comment, CancellationToken ct)
    {
        try
        {
            await _queries.ChangeStatusAsync(id, newStatus, comment, ct);
            TempData["Success"] = $"Status changed to {newStatus}.";
        }
        catch (Exception ex) when (ex is DomainException || ex is AuthorizationException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateFilterDataAsync(Guid? projectId, CancellationToken ct)
    {
        ViewBag.Projects = await _projects.GetAllAsync(ct);
        ViewBag.Ipos = await _ipos.GetAllAsync(projectId, ct);
        ViewBag.Products = await _products.GetAllAsync(ct);
    }

    private async Task PopulateFormDataAsync(CancellationToken ct)
    {
        ViewBag.Projects = await _projects.GetAllAsync(ct);
        ViewBag.Ipos = await _ipos.GetAllAsync(null, ct);
        ViewBag.Products = await _products.GetAllAsync(ct);
    }

    private static async Task<List<(byte[] Content, string FileName, string ContentType)>?> ReadPhotosAsync(List<IFormFile>? photos, CancellationToken ct)
    {
        if (photos == null || photos.Count == 0) return null;
        var result = new List<(byte[], string, string)>();
        foreach (var photo in photos)
        {
            if (photo.Length == 0) continue;
            using var ms = new MemoryStream();
            await photo.CopyToAsync(ms, ct);
            result.Add((ms.ToArray(), Path.GetFileName(photo.FileName), photo.ContentType));
        }
        return result.Count > 0 ? result : null;
    }
}
