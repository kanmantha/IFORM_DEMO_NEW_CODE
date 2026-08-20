using System.Text.Json;
using DailyPosterGenerator.Models;
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace DailyPosterGenerator.Services;

public interface ITemplateImportService
{
    Task<TemplateImportResult> ImportAsync(
        int tenantId,
        string name,
        string? description,
        string sector,
        IFormFile file,
        IReadOnlyList<ImportBox>? boxes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Heuristically locates likely text blocks in an uploaded poster and returns them
    /// as normalized erase boxes, so users get a starting point for the layout editor.
    /// Colourful areas (typically logos) are skipped.
    /// </summary>
    Task<IReadOnlyList<ImportBox>> DetectTextRegionsAsync(IFormFile file, CancellationToken ct = default);
}

public record TemplateImportResult(bool Success, PosterTemplate? Template, string? Error);

/// <summary>
/// Turns an uploaded poster image into a reusable PosterTemplate: the image becomes
/// the background layout and the dominant colours are extracted so daily text can be
/// rendered on top with the same look and feel. The user draws "erase" boxes over the
/// old text (which is removed by re-blending the surrounding background) and "keep"
/// boxes over logos (which stay untouched).
/// </summary>
public class TemplateImportService : ITemplateImportService
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TemplateImportService> _logger;

    public TemplateImportService(IWebHostEnvironment env, ILogger<TemplateImportService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<TemplateImportResult> ImportAsync(
        int tenantId,
        string name,
        string? description,
        string sector,
        IFormFile file,
        IReadOnlyList<ImportBox>? boxes = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new TemplateImportResult(false, null, "Give the template a name.");
        }

        if (file is null || file.Length == 0)
        {
            return new TemplateImportResult(false, null, "Choose a poster image to upload.");
        }

        if (file.Length > MaxFileBytes)
        {
            return new TemplateImportResult(false, null, "Image must be 10 MB or smaller.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return new TemplateImportResult(false, null, "Only PNG, JPG, JPEG and WEBP images are supported.");
        }

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        SKImage? image = null;
        try
        {
            image = SKImage.FromEncodedData(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode uploaded poster for template import.");
            return new TemplateImportResult(false, null, "That image could not be read. Try a PNG or JPG.");
        }

        using (image)
        {
            SKImage layout;
            var validBoxes = NormalizeBoxes(boxes);
            try
            {
                layout = validBoxes.Count > 0
                    ? ApplyBoxEdits(image, validBoxes)
                    : image;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Box editing failed; importing the image as-is.");
                layout = image;
            }

            using (layout)
            {
                var (accent, textColor) = AnalyzeColors(layout);
                var savedPath = await SaveBackgroundAsync(tenantId, layout, ext, ct);

                var template = new PosterTemplate
                {
                    TenantId = tenantId,
                    Name = name.Trim(),
                    Description = description?.Trim(),
                    Sector = SectorCatalog.Normalize(sector),
                    IsSystem = false,
                    IsImported = true,
                    IsActive = true,
                    Theme = "template",
                    AccentColor = accent,
                    TextColor = textColor,
                    BackgroundImagePath = savedPath,
                    BackgroundDim = 30,
                    TextRegionsJson = BuildRegionsJson(validBoxes),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                return new TemplateImportResult(true, template, null);
            }
        }
    }

    public async Task<IReadOnlyList<ImportBox>> DetectTextRegionsAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return Array.Empty<ImportBox>();
        }

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        try
        {
            using var image = SKImage.FromEncodedData(bytes);
            return DetectTextBlocks(image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto text detection failed.");
            return Array.Empty<ImportBox>();
        }
    }

    // --------------------------------------------------------------- box editing

    private static List<ImportBox> NormalizeBoxes(IReadOnlyList<ImportBox>? boxes)
    {
        if (boxes is null)
        {
            return new List<ImportBox>();
        }

        var result = new List<ImportBox>();
        foreach (var b in boxes)
        {
            var x = Math.Clamp(b.X, 0f, 0.99f);
            var y = Math.Clamp(b.Y, 0f, 0.99f);
            var w = Math.Clamp(b.W, 0.02f, 1f - x);
            var h = Math.Clamp(b.H, 0.02f, 1f - y);
            result.Add(new ImportBox { Type = b.IsKeep ? "keep" : "erase", X = x, Y = y, W = w, H = h });
        }

        return result;
    }

    /// <summary>
    /// Removes the content inside "erase" boxes by re-blending the blurred background
    /// there, then restores "keep" boxes (logos) from the original so they survive.
    /// </summary>
    private static SKImage ApplyBoxEdits(SKImage image, IReadOnlyList<ImportBox> boxes)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.DrawImage(image, 0, 0);

        var erase = boxes.Where(b => !b.IsKeep).ToList();
        if (erase.Count > 0)
        {
            using var blurred = CreateBlurred(image);
            foreach (var box in erase)
            {
                var rect = BoxRect(box, image.Width, image.Height);
                canvas.Save();
                canvas.ClipRect(rect);
                canvas.DrawImage(blurred, rect, rect, SKSamplingOptions.Default, null);
                canvas.Restore();
            }
        }

        foreach (var box in boxes.Where(b => b.IsKeep))
        {
            var rect = BoxRect(box, image.Width, image.Height);
            canvas.DrawImage(image, rect, rect, SKSamplingOptions.Default, null);
        }

        return surface.Snapshot();
    }

    private static SKImage CreateBlurred(SKImage image)
    {
        var sigma = Math.Max(24f, Math.Max(image.Width, image.Height) / 18f);
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        using var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma) };
        surface.Canvas.DrawImage(image, 0, 0, paint);
        return surface.Snapshot();
    }

    private static SKRect BoxRect(ImportBox box, int width, int height) =>
        SKRect.Create(box.X * width, box.Y * height, box.W * width, box.H * height);

    private static string BuildRegionsJson(IReadOnlyList<ImportBox> boxes)
    {
        var erase = boxes.Where(b => !b.IsKeep).ToList();
        if (erase.Count == 0)
        {
            return DefaultRegionsJson();
        }

        var keys = new[] { "header", "date", "title", "caption", "values", "footer" };
        var regions = new List<object>();
        var ordered = erase.OrderBy(b => b.Y).ThenBy(b => b.X).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            var key = i < keys.Length ? keys[i] : "caption";
            var fontSize = Math.Clamp(b.H * 0.4f, 0.014f, 0.055f);
            regions.Add(new
            {
                Key = key,
                X = Round3(b.X),
                Y = Round3(b.Y),
                W = Round3(b.W),
                H = Round3(b.H),
                FontSize = Round3(fontSize),
                Align = "center",
                Bold = key is "header" or "title" or "values"
            });
        }

        return JsonSerializer.Serialize(regions);
    }

    private static float Round3(float v) => MathF.Round(v, 3);

    // ------------------------------------------------------------- auto detection

    /// <summary>
    /// Simple heuristic: downscale, split into a coarse grid, mark cells with high
    /// luminance variance and low colour saturation as "text", group adjacent cells,
    /// and return the groups as normalized erase boxes. Colourful regions (logos) are
    /// left out so they can be kept.
    /// </summary>
    private static IReadOnlyList<ImportBox> DetectTextBlocks(SKImage image)
    {
        const int targetWidth = 96;
        var w = Math.Min(targetWidth, image.Width);
        var h = Math.Max(1, (int)Math.Round(image.Height * (w / (float)image.Width)));
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var small = SKSurface.Create(info);
        small.Canvas.DrawImage(image, new SKRect(0, 0, w, h), SKSamplingOptions.Default, null);
        using var pm = small.PeekPixels();
        var span = pm.GetPixelSpan();

        var lum = new float[w * h];
        var sat = new float[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var r = span[i];
                var g = span[i + 1];
                var b = span[i + 2];
                lum[y * w + x] = 0.299f * r + 0.587f * g + 0.114f * b;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                sat[y * w + x] = max - min;
            }
        }

        const int cell = 5;
        var cols = w / cell;
        var rows = h / cell;
        if (cols < 2 || rows < 2)
        {
            return Array.Empty<ImportBox>();
        }

        var vars = new float[cols * rows];
        var satAvgs = new float[cols * rows];
        for (var cy = 0; cy < rows; cy++)
        {
            for (var cx = 0; cx < cols; cx++)
            {
                float mean = 0, m2 = 0, s = 0;
                var n = 0;
                for (var dy = 0; dy < cell; dy++)
                {
                    for (var dx = 0; dx < cell; dx++)
                    {
                        var px = cx * cell + dx;
                        var py = cy * cell + dy;
                        if (px >= w || py >= h)
                        {
                            continue;
                        }

                        var v = lum[py * w + px];
                        mean += v;
                        m2 += v * v;
                        s += sat[py * w + px];
                        n++;
                    }
                }

                if (n == 0)
                {
                    continue;
                }

                var idx = cy * cols + cx;
                mean /= n;
                m2 /= n;
                vars[idx] = m2 - mean * mean;
                satAvgs[idx] = s / n;
            }
        }

        var meanVar = vars.Average();
        var stdVar = (float)Math.Sqrt(vars.Average(v => (v - meanVar) * (v - meanVar)));
        var threshold = meanVar + Math.Max(120f, stdVar * 1.1f);

        var marked = new bool[cols * rows];
        for (var i = 0; i < cols * rows; i++)
        {
            if (vars[i] > threshold && satAvgs[i] < 105)
            {
                marked[i] = true;
            }
        }

        // Connected components -> bounding boxes (cell coordinates).
        var boxes = new List<(int MinX, int MinY, int MaxX, int MaxY)>();
        var visited = new bool[cols * rows];
        var stack = new Stack<int>();
        for (var start = 0; start < cols * rows; start++)
        {
            if (!marked[start] || visited[start])
            {
                continue;
            }

            stack.Push(start);
            visited[start] = true;
            var minX = cols;
            var minY = rows;
            var maxX = -1;
            var maxY = -1;
            var cellCount = 0;
            while (stack.Count > 0)
            {
                var idx = stack.Pop();
                var cy = idx / cols;
                var cx = idx % cols;
                minX = Math.Min(minX, cx);
                maxX = Math.Max(maxX, cx);
                minY = Math.Min(minY, cy);
                maxY = Math.Max(maxY, cy);
                cellCount++;

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = cx + dx;
                        var ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= cols || ny >= rows)
                        {
                            continue;
                        }

                        var ni = ny * cols + nx;
                        if (marked[ni] && !visited[ni])
                        {
                            visited[ni] = true;
                            stack.Push(ni);
                        }
                    }
                }
            }

            if (cellCount >= 2)
            {
                boxes.Add((minX, minY, maxX, maxY));
            }
        }

        var result = new List<ImportBox>();
        foreach (var b in boxes)
        {
            var x0 = b.MinX * cell / (float)w;
            var y0 = b.MinY * cell / (float)h;
            var x1 = (b.MaxX + 1) * cell / (float)w;
            var y1 = (b.MaxY + 1) * cell / (float)h;
            var bw = x1 - x0;
            var bh = y1 - y0;
            if (bw * bh < 0.006f)
            {
                continue;
            }

            result.Add(new ImportBox { Type = "erase", X = Round3(Math.Clamp(x0, 0, 0.99f)), Y = Round3(Math.Clamp(y0, 0, 0.99f)), W = Round3(Math.Clamp(bw, 0.02f, 1)), H = Round3(Math.Clamp(bh, 0.02f, 1)) });
        }

        return result;
    }

    // --------------------------------------------------------------- persistence

    private async Task<string?> SaveBackgroundAsync(int tenantId, SKImage image, string originalExt, CancellationToken ct)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return null;
        }

        var dir = Path.Combine(webRoot, "templates", "imports", tenantId.ToString());
        Directory.CreateDirectory(dir);

        // Downscale wide uploads so the stored layout stays reasonable.
        var maxWidth = 1080;
        SKImage toSave = image;
        if (image.Width > maxWidth)
        {
            var scale = maxWidth / (float)image.Width;
            var info = new SKImageInfo(maxWidth, (int)(image.Height * scale), SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            surface.Canvas.DrawImage(image, new SKRect(0, 0, info.Width, info.Height), SKSamplingOptions.Default, null);
            toSave = surface.Snapshot();
        }

        try
        {
            using var data = toSave.Encode(SKEncodedImageFormat.Png, 90);
            if (data is null || data.Size == 0)
            {
                return null;
            }

            var fileName = $"bg_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}.png";
            var fullPath = Path.Combine(dir, fileName);
            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            data.SaveTo(fs);
            return $"/templates/imports/{tenantId}/{fileName}";
        }
        finally
        {
            if (!ReferenceEquals(toSave, image))
            {
                toSave.Dispose();
            }
        }
    }

    /// <summary>Samples the image and returns an accent hex plus a readable text hex.</summary>
    private static (string Accent, string TextColor) AnalyzeColors(SKImage image)
    {
        const int sampleSize = 24;
        using var small = SKSurface.Create(new SKImageInfo(sampleSize, sampleSize, SKColorType.Rgba8888, SKAlphaType.Premul));
        small.Canvas.DrawImage(image, new SKRect(0, 0, sampleSize, sampleSize), SKSamplingOptions.Default, null);
        using var pm = small.PeekPixels();
        var span = pm.GetPixelSpan();

        // Quantise each channel to 2 bits (64 buckets) and tally.
        var buckets = new Dictionary<int, (int R, int G, int B, int Count)>();
        long sumR = 0, sumG = 0, sumB = 0;
        var n = 0;
        for (var i = 0; i + 3 < span.Length; i += 4)
        {
            var r = span[i];
            var g = span[i + 1];
            var b = span[i + 2];
            sumR += r;
            sumG += g;
            sumB += b;
            n++;

            var key = (r >> 6) << 4 | (g >> 6) << 2 | (b >> 6);
            buckets.TryGetValue(key, out var cur);
            buckets[key] = (cur.R + r, cur.G + g, cur.B + b, cur.Count + 1);
        }

        // Prefer the most frequent colourful bucket for the accent.
        SKColor accent = new(255, 102, 0);
        var bestScore = -1;
        foreach (var kv in buckets)
        {
            if (kv.Value.Count < 2)
            {
                continue;
            }

            var r = kv.Value.R / kv.Value.Count;
            var g = kv.Value.G / kv.Value.Count;
            var b = kv.Value.B / kv.Value.Count;
            var saturation = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            var score = kv.Value.Count * 10 + (saturation >= 40 ? 1 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                accent = new SKColor((byte)r, (byte)g, (byte)b);
            }
        }

        if (HexColor.Luminance(accent) < 120)
        {
            // Dark accent: brighten it a little so it reads on dark backgrounds.
            accent = new SKColor(
                (byte)Math.Min(255, accent.Red + 70),
                (byte)Math.Min(255, accent.Green + 70),
                (byte)Math.Min(255, accent.Blue + 70));
        }

        var avgBg = new SKColor((byte)(sumR / Math.Max(1, n)), (byte)(sumG / Math.Max(1, n)), (byte)(sumB / Math.Max(1, n)));
        return (HexColor.ToHex(accent), HexColor.ToHex(HexColor.AutoTextColor(avgBg)));
    }

    private static string DefaultRegionsJson() => JsonSerializer.Serialize(new[]
    {
        new { Key = "header", X = 0.05, Y = 0.05, W = 0.9, H = 0.07, FontSize = 0.028, Align = "center", Bold = true },
        new { Key = "date", X = 0.05, Y = 0.14, W = 0.9, H = 0.045, FontSize = 0.019, Align = "center", Bold = false },
        new { Key = "title", X = 0.08, Y = 0.22, W = 0.84, H = 0.2, FontSize = 0.048, Align = "center", Bold = true },
        new { Key = "caption", X = 0.12, Y = 0.46, W = 0.76, H = 0.16, FontSize = 0.026, Align = "center", Bold = false },
        new { Key = "values", X = 0.06, Y = 0.88, W = 0.88, H = 0.06, FontSize = 0.021, Align = "center", Bold = true },
        new { Key = "footer", X = 0.06, Y = 0.945, W = 0.88, H = 0.04, FontSize = 0.016, Align = "center", Bold = false }
    });
}