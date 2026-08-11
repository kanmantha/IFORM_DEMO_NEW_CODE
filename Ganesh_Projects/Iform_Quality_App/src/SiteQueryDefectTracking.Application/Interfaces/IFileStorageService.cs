namespace SiteQueryDefectTracking.Application.Interfaces;

public record StorageFileResult(string StorageKey, long Size, int Width, int Height, string ContentType);

/// <summary>
/// File storage abstraction. Supports local disk for development and Azure
/// Blob Storage for production. Physical paths/keys are never exposed to clients.
/// </summary>
public interface IFileStorageService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Saves the stream under a secure random logical key (<paramref name="container"/>&gt;/{guid}{ext}).
    /// </summary>
    Task<StorageFileResult> SaveAsync(
        Stream stream,
        string container,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    bool Exists(string key);
}