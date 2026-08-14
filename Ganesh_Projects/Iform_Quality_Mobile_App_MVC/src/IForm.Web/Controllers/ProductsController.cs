using IForm.Application.Common;
using IForm.Application.DTOs;
using IForm.Application.Services;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace IForm.Web.Controllers;

[Authorize(Policy = "TenantUsers")]
public class ProductsController : Controller
{
    private readonly IProductService _products;
    private readonly ISubscriptionService _subscriptions;

    public ProductsController(IProductService products, ISubscriptionService subscriptions)
    {
        _products = products;
        _subscriptions = subscriptions;
    }

    public async Task<IActionResult> Index(string? term, Guid? categoryId, int page = 1, CancellationToken ct = default)
    {
        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        return View(await _products.SearchAsync(term, categoryId, page, 24, ct));
    }

    public async Task<IActionResult> Lookup(string? term, CancellationToken ct)
    {
        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        var products = string.IsNullOrWhiteSpace(term)
            ? await _products.GetAllAsync(ct)
            : (await _products.SearchAsync(term, null, 1, 100, ct)).Items;
        return View("Index", new PagedResult<ProductListItemDto>(products, products.Count, 1, 24, (int)Math.Ceiling(products.Count / 24.0)));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product == null) return NotFound();
        return View(product);
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        return View(new CreateProductRequest());
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _products.GetCategoriesAsync(ct);
            return View(request);
        }

        try
        {
            var id = await _products.CreateAsync(request, ct);
            TempData["Success"] = "Product created.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DomainException || ex is PlanLimitExceededException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        return View(request);
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpGet]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        ViewBag.Products = await _products.GetAllAsync(ct);
        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        return View();
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose a file to import.";
            ViewBag.Categories = await _products.GetCategoriesAsync(ct);
            return View();
        }

        try
        {
            var rows = await ReadImportFileAsync(file, ct);
            var result = await _products.ImportCatalogueAsync(rows, ct);
            TempData["Success"] = $"Import complete: {result.Imported} imported, {result.Duplicates} duplicates skipped, {result.Invalid} invalid.";
            if (result.Errors > 0)
                TempData["Error"] = $"Import completed with {result.Errors} errors. Download the error report from the import page.";
            ViewBag.Result = result;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        ViewBag.Categories = await _products.GetCategoriesAsync(ct);
        ViewBag.Products = await _products.GetAllAsync(ct);
        return View();
    }

    [Authorize(Policy = "TenantAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedDefaultCatalogue(CancellationToken ct)
    {
        var count = await _products.SeedDefaultCatalogueAsync(ct);
        TempData["Success"] = $"{count} products seeded from the I-FORM accessories catalogue.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Category";
        sheet.Cell(1, 4).Value = "Specification";
        sheet.Cell(1, 5).Value = "Material";
        sheet.Cell(1, 6).Value = "Unit";
        sheet.Cell(1, 7).Value = "Description";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ProductImportTemplate.xlsx");
    }

    private static async Task<List<ProductImportRow>> ReadImportFileAsync(IFormFile file, CancellationToken ct)
    {
        var rows = new List<ProductImportRow>();
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension == ".xlsx" || extension == ".xls")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            using var workbook = new XLWorkbook(ms);
            var sheet = workbook.Worksheets.First();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            for (int r = 2; r <= lastRow; r++)
            {
                var code = sheet.Cell(r, 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;
                rows.Add(new ProductImportRow(
                    code,
                    sheet.Cell(r, 2).GetString().Trim(),
                    sheet.Cell(r, 3).GetString().Trim(),
                    sheet.Cell(r, 4).GetString().Trim(),
                    sheet.Cell(r, 5).GetString().Trim(),
                    sheet.Cell(r, 6).GetString().Trim(),
                    sheet.Cell(r, 7).GetString().Trim()));
            }
        }
        else if (extension == ".csv")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var header = await reader.ReadLineAsync(ct);
            while (!reader.EndOfStream)
            {
                var line = (await reader.ReadLineAsync(ct))?.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    rows.Add(new ProductImportRow(
                        parts[0].Trim(),
                        parts[1].Trim(),
                        parts.Length > 2 ? parts[2].Trim() : null,
                        parts.Length > 3 ? parts[3].Trim() : null,
                        parts.Length > 4 ? parts[4].Trim() : null,
                        parts.Length > 5 ? parts[5].Trim() : null,
                        parts.Length > 6 ? parts[6].Trim() : null));
                }
            }
        }
        else
        {
            throw new DomainException("Unsupported file type. Use .xlsx, .xls or .csv.");
        }

        return rows;
    }
}
