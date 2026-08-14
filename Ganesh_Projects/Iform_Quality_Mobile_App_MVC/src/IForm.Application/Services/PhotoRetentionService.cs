using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IForm.Application.Services;

public interface IPhotoRetentionService
{
    /// <summary>
    /// Deletes query photos older than each tenant's configured retention period
    /// (Features:Photos:RetentionMonths; 0 = keep forever). Removes the stored file
    /// and the database record.
    /// </summary>
    Task<int> PurgeExpiredAsync(CancellationToken ct = default);
}

public class PhotoRetentionService : IPhotoRetentionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantSettingsProvider _settings;
    private readonly IFileStorageService _storage;
    private readonly ILogger<PhotoRetentionService> _logger;

    public PhotoRetentionService(
        IApplicationDbContext db,
        ITenantSettingsProvider settings,
        IFileStorageService storage,
        ILogger<PhotoRetentionService> logger)
    {
        _db = db;
        _settings = settings;
        _storage = storage;
        _logger = logger;
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct = default)
    {
        var removed = 0;
        var tenants = await _db.Tenants.Select(t => t.Id).ToListAsync(ct);

        foreach (var tenantId in tenants)
        {
            var features = _settings.GetFeatures(tenantId);
            if (features.PhotoRetentionMonths <= 0) continue;

            var cutoff = DateTime.UtcNow.AddMonths(-features.PhotoRetentionMonths);
            var expired = await _db.QueryPhotos.IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId && p.UploadedAt < cutoff)
                .ToListAsync(ct);

            foreach (var photo in expired)
            {
                if (!string.IsNullOrWhiteSpace(photo.FilePath))
                {
                    try
                    {
                        await _storage.DeleteAsync(photo.FilePath, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete photo file {Path} during retention purge.", photo.FilePath);
                    }
                }

                _db.QueryPhotos.Remove(photo);
                removed++;
            }
        }

        if (removed > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Photo retention purge removed {Count} photos.", removed);
        }

        return removed;
    }
}
