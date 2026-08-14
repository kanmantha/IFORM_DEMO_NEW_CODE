using IForm.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace IForm.Infrastructure.Services;

/// <summary>
/// File storage backed by the local filesystem. A file-safe container path is generated
/// and stored in SQL Server; only authenticated controller actions can stream it back,
/// so files are never exposed as predictable public URLs.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _baseUrl;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _rootPath = Path.GetFullPath(configuration["Storage:Local:RootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "storage"));
        Directory.CreateDirectory(_rootPath);
        _baseUrl = configuration["ApplicationBaseUrl"] ?? string.Empty;
    }

    public Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "files" : Sanitize(folder);
        var safeName = Sanitize(Path.GetFileNameWithoutExtension(fileName));
        var extension = Sanitize(Path.GetExtension(fileName));
        if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";

        var id = Guid.NewGuid().ToString("N");
        var relativePath = Path.Combine(safeFolder, $"{id}_{safeName}{extension}").Replace('\\', '/');
        var fullPath = Path.Combine(_rootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using (var file = File.Create(fullPath))
        {
            content.CopyTo(file);
        }

        var size = new FileInfo(fullPath).Length;
        return Task.FromResult(new StoredFile(relativePath, $"{safeName}{extension}", contentType, size, BuildUrl(relativePath)));
    }

    public async Task<StoredFile> SaveBytesAsync(byte[] content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        return await SaveAsync(stream, fileName, contentType, folder, ct);
    }

    public Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        var safePath = ResolvePath(path);
        if (!File.Exists(safePath)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(safePath));
    }

    public Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var safePath = ResolvePath(path);
        if (!File.Exists(safePath)) return Task.FromResult(false);
        File.Delete(safePath);
        return Task.FromResult(true);
    }

    public Task<long> GetStorageUsedBytesAsync(CancellationToken ct = default) =>
        Task.FromResult(Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length));

    public async Task<byte[]> NormalizeImageAsync(Stream content, int maxWidth = 1600, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(content, ct);
        if (image.Width > maxWidth)
        {
            var ratio = (double)maxWidth / image.Width;
            var height = (int)(image.Height * ratio);
            image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(maxWidth, height), Mode = ResizeMode.Max }));
        }
        using var output = new MemoryStream();
        await image.SaveAsync(output, new JpegEncoder { Quality = 82 }, ct);
        return output.ToArray();
    }

    private string ResolvePath(string path)
    {
        var safe = path.Replace('\\', '/');
        var full = Path.GetFullPath(Path.Combine(_rootPath, safe));
        var root = Path.GetFullPath(_rootPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid storage path.");
        return full;
    }

    private string BuildUrl(string relativePath) =>
        string.IsNullOrWhiteSpace(_baseUrl) ? $"/files/{relativePath}" : $"{_baseUrl}/files/{relativePath}";

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }
}

/// <summary>Azure Blob Storage implementation used in production. Requires ConnectionString in configuration.</summary>
public class AzureBlobFileStorageService : IFileStorageService
{
    private readonly ILogger<AzureBlobFileStorageService> _logger;
    private readonly string _connectionString;
    private readonly string _containerName;
    private Azure.Storage.Blobs.BlobContainerClient? _container;

    public AzureBlobFileStorageService(IConfiguration configuration, ILogger<AzureBlobFileStorageService> logger)
    {
        _logger = logger;
        _connectionString = configuration["Storage:Azure:ConnectionString"] ?? string.Empty;
        _containerName = configuration["Storage:Azure:ContainerName"] ?? "iform";
    }

    private Azure.Storage.Blobs.BlobContainerClient Container
    {
        get
        {
            if (_container != null) return _container;
            var service = new Azure.Storage.Blobs.BlobServiceClient(_connectionString);
            _container = service.GetBlobContainerClient(_containerName);
            _container.CreateIfNotExists();
            return _container;
        }
    }

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "files" : new string(folder.Where(c => char.IsLetterOrDigit(c) || c == '/' || c == '-').ToArray());
        var blobName = $"{safeFolder}/{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var client = Container.GetBlobClient(blobName);
        await client.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        var props = await client.GetPropertiesAsync(cancellationToken: ct);
        return new StoredFile(blobName, Path.GetFileName(fileName), contentType, props.Value.ContentLength, client.Uri.ToString());
    }

    public async Task<StoredFile> SaveBytesAsync(byte[] content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        return await SaveAsync(stream, fileName, contentType, folder, ct);
    }

    public async Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        var client = Container.GetBlobClient(path);
        if (!await client.ExistsAsync(ct)) return null;
        var response = await client.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await Container.GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: ct);
        return response.Value;
    }

    public Task<long> GetStorageUsedBytesAsync(CancellationToken ct = default)
    {
        long total = 0;
        var blobs = Container.GetBlobs(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, string.Empty, ct);
        foreach (var blob in blobs) total += blob.Properties.ContentLength ?? 0;
        return Task.FromResult(total);
    }

    public async Task<byte[]> NormalizeImageAsync(Stream content, int maxWidth = 1600, CancellationToken ct = default)
    {
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(content, ct);
        if (image.Width > maxWidth)
        {
            var ratio = (double)maxWidth / image.Width;
            image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(maxWidth, (int)(image.Height * ratio)),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
            }));
        }
        using var output = new MemoryStream();
        await image.SaveAsync(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 82 }, ct);
        return output.ToArray();
    }
}
