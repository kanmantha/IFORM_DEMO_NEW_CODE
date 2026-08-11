using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Infrastructure.Services;

public class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string? RootPath { get; set; }
    public string? ConnectionString { get; set; }
    public string? ContainerName { get; set; }
}

/// <summary>
/// Local-disk storage provider used for development.
/// </summary>
public class LocalFileStorageService(
    IOptions<StorageOptions> options,
    ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    public bool IsConfigured => true;

    private string Root => options.Value.RootPath ?? Path.Combine(AppContext.BaseDirectory, "uploads");

    public async Task<StorageFileResult> SaveAsync(
        Stream stream, string container, string originalFileName, string contentType,
        CancellationToken cancellationToken = default)
    {
        var containerPath = Path.Combine(Root, Sanitize(container));
        Directory.CreateDirectory(containerPath);

        var extension = Path.GetExtension(originalFileName);
        var key = $"{container}/{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(Root, Sanitize(key));

        await using (var file = File.Create(fullPath))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        var size = new FileInfo(fullPath).Length;
        logger.LogInformation("Stored file {Key} ({Size} bytes).", key, size);

        return new StorageFileResult(key, size, 0, 0, contentType);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(Root, Sanitize(key));
        if (!File.Exists(fullPath)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(Root, Sanitize(key));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public bool Exists(string key) => File.Exists(Path.Combine(Root, Sanitize(key)));

    private static string Sanitize(string path) =>
        path.Replace("..", string.Empty).Replace("/", Path.DirectorySeparatorChar.ToString());
}

/// <summary>
/// Azure Blob Storage backend for production.
/// </summary>
public class AzureBlobStorageService(
    IOptions<StorageOptions> options,
    ILogger<AzureBlobStorageService> logger) : IFileStorageService
{
    private BlobContainerClient? _container;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ConnectionString);

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct = default)
    {
        if (_container is not null) return _container;

        var client = new BlobServiceClient(options.Value.ConnectionString);
        var container = client.GetBlobContainerClient(options.Value.ContainerName ?? "site-query");
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        _container = container;
        return container;
    }

    public async Task<StorageFileResult> SaveAsync(
        Stream stream, string containerKey, string originalFilename, string contentType,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        var blobName = $"{containerKey}/{Guid.NewGuid():N}{Path.GetExtension(originalFilename)}";
        var blob = container.GetBlobClient(blobName);

        stream.Position = 0;
        await blob.UploadAsync(stream, overwrite: true, cancellationToken);

        logger.LogInformation("Uploaded blob {Blob}.", blobName);

        return new StorageFileResult(blobName, stream.Length, 0, 0, contentType);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(key);
        var exists = await blob.ExistsAsync(cancellationToken);
        if (!exists.Value) return null;

        var stream = new MemoryStream();
        await blob.DownloadToAsync(stream, cancellationToken);
        stream.Position = 0;
        return stream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.DeleteBlobIfExistsAsync(key, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);
        return (await container.GetBlobClient(key).ExistsAsync(cancellationToken)).Value;
    }

    public bool Exists(string key) => ExistsAsync(key).GetAwaiter().GetResult();
}