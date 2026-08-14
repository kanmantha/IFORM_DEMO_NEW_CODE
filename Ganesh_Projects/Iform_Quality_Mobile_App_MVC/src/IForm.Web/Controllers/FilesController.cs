using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IForm.Web.Controllers;

/// <summary>
/// Securely streams files stored by the app. Access is authenticated and the
/// caller must belong to the tenant that owns the file.
/// </summary>
[Authorize(Policy = "TenantUsers")]
public class FilesController : Controller
{
    private readonly IFileStorageService _storage;
    private readonly IApplicationDbContext _db;

    public FilesController(IFileStorageService storage, IApplicationDbContext db)
    {
        _storage = storage;
        _db = db;
    }

    public async Task<IActionResult> Download(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return BadRequest();

        if (!await FileBelongsToTenantAsync(path, ct)) return NotFound();

        var stream = await _storage.OpenAsync(path, ct);
        if (stream == null) return NotFound();

        var fileName = Path.GetFileName(path);
        var contentType = GetContentType(fileName);
        return File(stream, contentType, fileName);
    }

    private async Task<bool> FileBelongsToTenantAsync(string path, CancellationToken ct)
    {
        if (await _db.QueryPhotos.AnyAsync(p => p.FilePath == path, ct)) return true;
        if (await _db.EotDocuments.AnyAsync(d => d.FilePath == path, ct)) return true;
        if (await _db.Products.AnyAsync(p => p.PhotoPath == path, ct)) return true;
        return false;
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
