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
    /// Heuristically locates likely text blocks and logo areas in an uploaded poster so
    /// the import editor can offer one-click erasing. Text detection skips colourful
    /// regions (logos) and logo detection skips low-saturation text.
    /// </summary>
    Task<DetectionResult> DetectRegionsAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Same detection as <see cref="DetectRegionsAsync"/> but on a stored image under wwwroot.</summary>
    Task<DetectionResult> DetectRegionsFromFileAsync(string? relativePath, CancellationToken ct = default);

    /// <summary>
    /// Re-applies erase / erase-logo / keep boxes to a template's untouched original
    /// upload and replaces its processed background, text regions and box history.
    /// </summary>
    Task<TemplateImportResult> ReprocessAsync(PosterTemplate template, IReadOnlyList<ImportBox>? boxes, CancellationToken ct = default);
}

public record TemplateImportResult(bool Success, PosterTemplate? Template, string? Error);

/// <summary>Auto-detected regions of an uploaded poster, normalized 0..1.</summary>
public record DetectionResult(IReadOnlyList<ImportBox> TextBoxes, IReadOnlyList<ImportBox> LogoBoxes);

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
                _logger.LogError(ex, "Box editing failed; importing the image as-is.");
                layout = image;
            }

            using (layout)
            {
                var (accent, textColor) = AnalyzeColors(layout);
                var savedPath = await SaveBackgroundAsync(tenantId, layout, ext, ct);
                var originalPath = await SaveOriginalAsync(tenantId, bytes, ext, ct);

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
                    OriginalBackgroundPath = originalPath,
                    ImportBoxesJson = validBoxes.Count > 0 ? JsonSerializer.Serialize(validBoxes) : null,
                    BackgroundDim = 30,
                    TextRegionsJson = BuildRegionsJson(validBoxes),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                return new TemplateImportResult(true, template, null);
            }
        }
    }

    public async Task<DetectionResult> DetectRegionsAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>());
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
            return DetectCore(image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto detection failed.");
            return new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>());
        }
    }

    public Task<DetectionResult> DetectRegionsFromFileAsync(string? relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Task.FromResult(new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>()));
        }

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return Task.FromResult(new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>()));
        }

        var fullPath = Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>()));
        }

        try
        {
            using var image = SKImage.FromEncodedData(fullPath);
            return Task.FromResult(DetectCore(image));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto detection failed for {Path}.", relativePath);
            return Task.FromResult(new DetectionResult(Array.Empty<ImportBox>(), Array.Empty<ImportBox>()));
        }
    }

    private static DetectionResult DetectCore(SKImage image)
    {
        var logos = DetectLogoBlocks(image);
        var text = DetectTextBlocks(image, logos);
        return new DetectionResult(text, logos);
    }

    public async Task<TemplateImportResult> ReprocessAsync(
        PosterTemplate template, IReadOnlyList<ImportBox>? boxes, CancellationToken ct = default)
    {
        // Templates imported before re-editing existed have no stored original; fall
        // back to their processed background and promote it to the new "original".
        var sourceRelative = string.IsNullOrWhiteSpace(template?.OriginalBackgroundPath)
            ? template?.BackgroundImagePath
            : template.OriginalBackgroundPath;
        if (template is null || string.IsNullOrWhiteSpace(sourceRelative))
        {
            return new TemplateImportResult(false, null, "This template has no poster image to re-edit.");
        }

        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return new TemplateImportResult(false, null, "Storage is not available.");
        }

        var fullPath = Path.Combine(
            webRoot, sourceRelative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return new TemplateImportResult(false, null, "The poster image file is missing on disk.");
        }

        SKImage? image;
        try
        {
            image = SKImage.FromEncodedData(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode the original upload of template {TemplateId}.", template.Id);
            return new TemplateImportResult(false, null, "The original image could not be read.");
        }

        using (image)
        {
            var validBoxes = NormalizeBoxes(boxes);
            SKImage layout;
            try
            {
                layout = validBoxes.Count > 0 ? ApplyBoxEdits(image, validBoxes) : image;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reprocessing box editing failed for template {TemplateId}; using the raw original.", template.Id);
                layout = image;
            }

            using (layout)
            {
                var savedPath = await SaveBackgroundAsync(template.TenantId, layout, ".png", ct);
                if (savedPath is null)
                {
                    return new TemplateImportResult(false, null, "Could not save the updated background.");
                }

                var previousBg = template.BackgroundImagePath;
                if (string.IsNullOrWhiteSpace(template.OriginalBackgroundPath))
                {
                    // Legacy template: the pre-edit image becomes the new editing source.
                    template.OriginalBackgroundPath = sourceRelative;
                }

                // Never delete the file that just became (or already was) the original.
                if (!string.Equals(previousBg, template.OriginalBackgroundPath, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteImportedFile(previousBg, savedPath);
                }

                // Accent/text colours are left untouched so user customizations survive.
                template.BackgroundImagePath = savedPath;
                template.TextRegionsJson = BuildRegionsJson(validBoxes);
                template.ImportBoxesJson = validBoxes.Count > 0 ? JsonSerializer.Serialize(validBoxes) : null;
                template.UpdatedAt = DateTime.UtcNow;
                return new TemplateImportResult(true, template, null);
            }
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
            var type = b.IsKeep ? "keep" : b.IsLogoErase ? "erase-logo" : "erase";
            result.Add(new ImportBox { Type = type, X = x, Y = y, W = w, H = h });
        }

        return result;
    }

    /// <summary>
    /// Removes the content inside "erase" boxes and restores "keep" boxes (logos).
    ///
    /// Each erase box is seeded in a working copy with the colour of the band of pixels
    /// just outside it (trimmed-mean of the border ring, so stray glyph pixels do not
    /// bleed in). The seed region extends beyond the box by a blur-radius margin, so
    /// when the seeded copy is blurred, box-edge pixels average seed fill on both sides
    /// instead of picking up bright text that sits just outside the box. Seeding runs
    /// twice: the second pass re-samples rings from the already-seeded image so adjacent
    /// boxes do not contaminate each other's fill colour. The blurred area is then copied
    /// back over each box and "keep" boxes are restored pixel-perfect from the original.
    /// </summary>
    private static SKImage ApplyBoxEdits(SKImage image, IReadOnlyList<ImportBox> boxes)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var erase = boxes.Where(b => !b.IsKeep).ToList();
        if (erase.Count == 0)
        {
            return image;
        }

        var pad = (int)Math.Clamp(BlurSigma(image) * 1.5f, 12f, 120f);

        using var sample = SKBitmap.FromImage(image);
        using var seed1 = BuildSeed(image, sample, erase, pad, info);
        using var seedBmp = SKBitmap.FromImage(seed1);
        using var seed2 = BuildSeed(seed1, seedBmp, erase, pad, info);

        using var blurred = CreateBlurred(seed2);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Failed to allocate the edit surface.");
        var canvas = surface.Canvas;
        canvas.DrawImage(image, 0, 0);

        foreach (var box in erase)
        {
            var rect = BoxRect(box, image.Width, image.Height);
            canvas.Save();
            canvas.ClipRect(rect);
            canvas.DrawImage(blurred, rect, rect, SKSamplingOptions.Default, null);
            canvas.Restore();
        }

        foreach (var box in boxes.Where(b => b.IsKeep))
        {
            var rect = BoxRect(box, image.Width, image.Height);
            canvas.DrawImage(image, rect, rect, SKSamplingOptions.Default, null);
        }

        return surface.Snapshot();
    }

    /// <summary>Draws a copy of <paramref name="source"/> with every erase box's padded
    /// area replaced by a gradient derived from that area's border ring.</summary>
    private static SKImage BuildSeed(
        SKImage source, SKBitmap ringSource, IReadOnlyList<ImportBox> erase, int pad, SKImageInfo info)
    {
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Failed to allocate the seed surface.");
        var canvas = surface.Canvas;
        canvas.DrawImage(source, 0, 0);
        foreach (var box in erase)
        {
            var rect = PaddedRect(BoxRect(box, info.Width, info.Height), pad, info.Width, info.Height);
            using var shader = MakeFillShader(rect, SampleBorder(ringSource, rect));
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect(rect, paint);
        }

        return surface.Snapshot();
    }

    private static SKRect PaddedRect(SKRect rect, int pad, int width, int height) =>
        SKRect.Create(
            Math.Max(0f, rect.Left - pad),
            Math.Max(0f, rect.Top - pad),
            Math.Min(width, rect.Right + pad) - Math.Max(0f, rect.Left - pad),
            Math.Min(height, rect.Bottom + pad) - Math.Max(0f, rect.Top - pad));

    private static float BlurSigma(SKImage image) =>
        Math.Min(160f, Math.Max(24f, Math.Max(image.Width, image.Height) / 18f));

    private static SKImage CreateBlurred(SKImage image)
    {
        var sigma = BlurSigma(image);
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Failed to allocate the blur surface.");
        using var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(sigma, sigma) };
        surface.Canvas.DrawImage(image, 0, 0, paint);
        return surface.Snapshot();
    }

    private static SKRect BoxRect(ImportBox box, int width, int height) =>
        SKRect.Create(box.X * width, box.Y * height, box.W * width, box.H * height);

    /// <summary>Robust average of a pixel set: drops the extreme 12.5% on each side so
    /// a few glyph pixels touching the ring do not skew the background colour.</summary>
    private static SKColor TrimmedMean(IReadOnlyList<SKColor> pixels)
    {
        if (pixels.Count == 0)
        {
            return new SKColor(0, 0, 0);
        }

        static int Center(List<int> l)
        {
            var skip = l.Count / 8;
            if (l.Count - 2 * skip < 2)
            {
                return l[l.Count / 2];
            }

            return (int)Math.Round(l.Skip(skip).Take(l.Count - 2 * skip).Average());
        }

        return new SKColor(
            (byte)Center(pixels.Select(p => (int)p.Red).OrderBy(v => v).ToList()),
            (byte)Center(pixels.Select(p => (int)p.Green).OrderBy(v => v).ToList()),
            (byte)Center(pixels.Select(p => (int)p.Blue).OrderBy(v => v).ToList()));
    }

    /// <summary>Samples the band of pixels just outside each edge of <paramref name="rect"/>.</summary>
    private static (SKColor Top, SKColor Bottom, SKColor Left, SKColor Right) SampleBorder(SKBitmap bmp, SKRect rect)
    {
        var thick = Math.Max(4, (int)Math.Min(24, Math.Min(rect.Width, rect.Height) * 0.05));
        var x0 = Math.Max(0, (int)rect.Left);
        var x1 = Math.Min(bmp.Width - 1, (int)rect.Right);
        var y0 = Math.Max(0, (int)rect.Top);
        var y1 = Math.Min(bmp.Height - 1, (int)rect.Bottom);

        var top = new List<SKColor>();
        var bottom = new List<SKColor>();
        var left = new List<SKColor>();
        var right = new List<SKColor>();
        for (var x = x0; x <= x1; x++)
        {
            for (var k = 1; k <= thick; k++)
            {
                if (y0 - k >= 0)
                {
                    top.Add(bmp.GetPixel(x, y0 - k));
                }

                if (y1 + k < bmp.Height)
                {
                    bottom.Add(bmp.GetPixel(x, y1 + k));
                }
            }
        }

        for (var y = y0; y <= y1; y++)
        {
            for (var k = 1; k <= thick; k++)
            {
                if (x0 - k >= 0)
                {
                    left.Add(bmp.GetPixel(x0 - k, y));
                }

                if (x1 + k < bmp.Width)
                {
                    right.Add(bmp.GetPixel(x1 + k, y));
                }
            }
        }

        // Sides that fall outside the image (box flush to an edge) fall back to the
        // overall ring colour instead of an arbitrary black.
        var overall = TrimmedMean(top.Concat(bottom).Concat(left).Concat(right).ToList());
        return (
            top.Count > 0 ? TrimmedMean(top) : overall,
            bottom.Count > 0 ? TrimmedMean(bottom) : overall,
            left.Count > 0 ? TrimmedMean(left) : overall,
            right.Count > 0 ? TrimmedMean(right) : overall);
    }

    /// <summary>Builds a fill for an erase box: a vertical or horizontal gradient from the
    /// ring colours (whichever axis varies more), or a solid colour when the ring is flat.</summary>
    private static SKShader MakeFillShader(SKRect rect, (SKColor Top, SKColor Bottom, SKColor Left, SKColor Right) border)
    {
        var vertical = ColorDist(border.Top, border.Bottom) >= ColorDist(border.Left, border.Right);
        var c0 = vertical ? border.Top : border.Left;
        var c1 = vertical ? border.Bottom : border.Right;
        if (ColorDist(c0, c1) < 10)
        {
            return SKShader.CreateColor(c0);
        }

        var p0 = vertical ? new SKPoint(rect.MidX, rect.Top) : new SKPoint(rect.Left, rect.MidY);
        var p1 = vertical ? new SKPoint(rect.MidX, rect.Bottom) : new SKPoint(rect.Right, rect.MidY);
        return SKShader.CreateLinearGradient(p0, p1, new[] { c0, c1 }, null, SKShaderTileMode.Clamp);
    }

    private static float ColorDist(SKColor a, SKColor b) =>
        MathF.Sqrt(MathF.Pow(a.Red - b.Red, 2) + MathF.Pow(a.Green - b.Green, 2) + MathF.Pow(a.Blue - b.Blue, 2));

    private static string BuildRegionsJson(IReadOnlyList<ImportBox> boxes)
    {
        // Logo erases are removed from the background but do not become text zones.
        var erase = boxes.Where(b => !b.IsKeep && !b.IsLogoErase).ToList();
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

    private sealed class CellStats
    {
        public int Width;
        public int Height;
        public int Cols;
        public int Rows;
        public float[] Variance = Array.Empty<float>();
        public float[] Saturation = Array.Empty<float>();
    }

    /// <summary>Downscales the image onto a coarse grid and computes per-cell luminance
    /// variance and mean colour saturation - shared by the text and logo detectors.</summary>
    private static CellStats ComputeCellStats(SKImage image, int targetWidth)
    {
        const int cell = 5;
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

        var cols = w / cell;
        var rows = h / cell;
        var stats = new CellStats { Width = w, Height = h, Cols = cols, Rows = rows };
        if (cols < 2 || rows < 2)
        {
            return stats;
        }

        stats.Variance = new float[cols * rows];
        stats.Saturation = new float[cols * rows];
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
                stats.Variance[idx] = m2 - mean * mean;
                stats.Saturation[idx] = s / n;
            }
        }

        return stats;
    }

    /// <summary>
    /// Text heuristic run at two scales (coarse catches large headings, fine catches
    /// small lines): mark cells with high luminance variance and low colour saturation,
    /// group adjacent cells into blocks, drop blocks overlapping a detected logo, then
    /// merge overlapping boxes from both scales.
    /// </summary>
    private static IReadOnlyList<ImportBox> DetectTextBlocks(SKImage image, IReadOnlyList<ImportBox> logoBoxes)
    {
        var candidates = new List<ImportBox>();
        candidates.AddRange(DetectTextBlocksAtScale(image, 96, logoBoxes));
        candidates.AddRange(DetectTextBlocksAtScale(image, 192, logoBoxes));
        return MergeBoxes(candidates);
    }

    private static IReadOnlyList<ImportBox> DetectTextBlocksAtScale(
        SKImage image, int targetWidth, IReadOnlyList<ImportBox> logoBoxes)
    {
        const int cell = 5;
        var s = ComputeCellStats(image, targetWidth);
        if (s.Cols < 2 || s.Rows < 2)
        {
            return Array.Empty<ImportBox>();
        }

        var vars = s.Variance;
        var meanVar = vars.Average();
        var stdVar = (float)Math.Sqrt(vars.Average(v => (v - meanVar) * (v - meanVar)));
        var threshold = meanVar + Math.Max(70f, stdVar * 1.1f);

        var marked = new bool[s.Cols * s.Rows];
        for (var i = 0; i < marked.Length; i++)
        {
            if (vars[i] > threshold && s.Saturation[i] < 125)
            {
                marked[i] = true;
            }
        }

        var result = new List<ImportBox>();
        foreach (var group in GroupCells(marked, s.Cols, s.Rows))
        {
            var box = ToImportBox(group, cell, s.Width, s.Height, minArea: 0.006f, type: "erase");
            if (box is not null && !logoBoxes.Any(l => OverlapRatio(box, l) > 0.4f))
            {
                result.Add(box);
            }
        }

        return result;
    }

    /// <summary>Greedy union of overlapping boxes (largest first) so multi-scale
    /// detections of the same text block collapse into one region.</summary>
    private static List<ImportBox> MergeBoxes(List<ImportBox> boxes)
    {
        var result = new List<ImportBox>();
        foreach (var b in boxes.OrderByDescending(b => b.W * b.H))
        {
            ImportBox? hit = null;
            foreach (var r in result)
            {
                if (OverlapRatio(r, b) > 0.3f || OverlapRatio(b, r) > 0.3f)
                {
                    hit = r;
                    break;
                }
            }

            if (hit is null)
            {
                result.Add(new ImportBox { Type = b.Type, X = b.X, Y = b.Y, W = b.W, H = b.H });
            }
            else
            {
                var x1 = MathF.Min(hit.X, b.X);
                var y1 = MathF.Min(hit.Y, b.Y);
                var x2 = MathF.Max(hit.X + hit.W, b.X + b.W);
                var y2 = MathF.Max(hit.Y + hit.H, b.Y + b.H);
                hit.X = Round3(x1);
                hit.Y = Round3(y1);
                hit.W = Round3(x2 - x1);
                hit.H = Round3(y2 - y1);
            }
        }

        return result;
    }

    /// <summary>
    /// Logo heuristic: logos are compact colourful artwork, so mark cells with high
    /// colour saturation, group them, and keep only reasonably sized groups (a full-bleed
    /// coloured background or a tiny speck is not a logo).
    /// </summary>
    private static IReadOnlyList<ImportBox> DetectLogoBlocks(SKImage image)
    {
        const int cell = 5;
        const float saturationThreshold = 90f;
        var s = ComputeCellStats(image, 96);
        if (s.Cols < 2 || s.Rows < 2)
        {
            return Array.Empty<ImportBox>();
        }

        var marked = new bool[s.Cols * s.Rows];
        for (var i = 0; i < marked.Length; i++)
        {
            if (s.Saturation[i] >= saturationThreshold)
            {
                marked[i] = true;
            }
        }

        var result = new List<ImportBox>();
        foreach (var group in GroupCells(marked, s.Cols, s.Rows))
        {
            var box = ToImportBox(group, cell, s.Width, s.Height, minArea: 0.004f, type: "erase-logo");
            if (box is not null && box.W * box.H <= 0.30f)
            {
                result.Add(box);
            }
        }

        return result;
    }

    private static ImportBox? ToImportBox(
        (int MinX, int MinY, int MaxX, int MaxY) group, int cell, int w, int h, float minArea, string type)
    {
        var x0 = group.MinX * cell / (float)w;
        var y0 = group.MinY * cell / (float)h;
        var x1 = (group.MaxX + 1) * cell / (float)w;
        var y1 = (group.MaxY + 1) * cell / (float)h;
        var bw = x1 - x0;
        var bh = y1 - y0;
        if (bw * bh < minArea)
        {
            return null;
        }

        return new ImportBox
        {
            Type = type,
            X = Round3(Math.Clamp(x0, 0, 0.99f)),
            Y = Round3(Math.Clamp(y0, 0, 0.99f)),
            W = Round3(Math.Clamp(bw, 0.02f, 1)),
            H = Round3(Math.Clamp(bh, 0.02f, 1))
        };
    }

    /// <summary>Flood-fills adjacent marked cells and returns their bounding boxes.</summary>
    private static List<(int MinX, int MinY, int MaxX, int MaxY)> GroupCells(bool[] marked, int cols, int rows)
    {
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

        return boxes;
    }

    /// <summary>Intersection area of two boxes divided by the first box's area.</summary>
    private static float OverlapRatio(ImportBox a, ImportBox b)
    {
        var x1 = MathF.Max(a.X, b.X);
        var y1 = MathF.Max(a.Y, b.Y);
        var x2 = MathF.Min(a.X + a.W, b.X + b.W);
        var y2 = MathF.Min(a.Y + a.H, b.Y + b.H);
        var inter = MathF.Max(0f, x2 - x1) * MathF.Max(0f, y2 - y1);
        return inter / MathF.Max(0.0001f, a.W * a.H);
    }

    // --------------------------------------------------------------- persistence

    /// <summary>Stores the untouched upload so the layout can be re-edited later.</summary>
    private async Task<string?> SaveOriginalAsync(int tenantId, byte[] bytes, string ext, CancellationToken ct)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return null;
        }

        var dir = Path.Combine(webRoot, "templates", "imports", tenantId.ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var fileName = $"orig_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
            var fullPath = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes, ct);
            return $"/templates/imports/{tenantId}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store the original upload; re-editing stays unavailable for this template.");
            return null;
        }
    }

    /// <summary>Deletes a replaced file under /templates/imports (never touches other paths).</summary>
    private void DeleteImportedFile(string? relativePath, string? keepPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            string.Equals(relativePath, keepPath, StringComparison.OrdinalIgnoreCase) ||
            !relativePath.StartsWith("/templates/imports/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                return;
            }

            var full = Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete the replaced import file {Path}.", relativePath);
        }
    }

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