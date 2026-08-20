using DailyPosterGenerator.Data;
using DailyPosterGenerator.Models;
using DailyPosterGenerator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyPosterGenerator.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly IDbContextFactory<DailyPosterDbContext> _dbFactory;
    private readonly ISettingsService _settings;
    private readonly ITextGenerationService _text;

    public SettingsController(
        IDbContextFactory<DailyPosterDbContext> dbFactory,
        ISettingsService settings,
        ITextGenerationService text)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _text = text;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var vm = new SettingsViewModel
        {
            AiEnabled = bool.Parse(await _settings.GetAsync("ai.enabled", "true") ?? "true"),
            AiEndpoint = await _settings.GetAsync("ai.endpoint", "https://api.openai.com/v1"),
            AiApiKey = await _settings.GetAsync("ai.apiKey", ""),
            AiChatModel = await _settings.GetAsync("ai.chatModel", "gpt-4o-mini"),
            AiImageModel = await _settings.GetAsync("ai.imageModel", "dall-e-3"),
            AiGenerateImages = bool.Parse(await _settings.GetAsync("ai.generateImages", "false") ?? "false"),
            AiTimeoutSeconds = int.Parse(await _settings.GetAsync("ai.timeoutSeconds", "90") ?? "90"),
            SchedulerEnabled = bool.Parse(await _settings.GetAsync("scheduler.enabled", "true") ?? "true"),
            SchedulerTime = await _settings.GetAsync("scheduler.time", "06:00"),
            PosterTheme = await GetOrgAsync("theme", "auto", "auto"),
            OrganizationName = await GetOrgAsync("name", ""),
            OrganizationCity = await GetOrgAsync("city", ""),
            OrganizationTagline = await GetOrgAsync("tagline", ""),
            OrganizationFacebook = await GetOrgAsync("facebook", ""),
            OrganizationInstagram = await GetOrgAsync("instagram", ""),
            OrganizationPhones = await GetOrgAsync("phones", ""),
            OrganizationShowValues = bool.Parse(await GetOrgAsync("showValues", "true") ?? "true"),
            OrganizationValues = await GetOrgAsync("values", "Quality,Service,Trust,Excellence,Community"),
            AiActuallyConfigured = await _text.IsConfiguredAsync(),
            Platforms = await db.Platforms.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SettingsViewModel vm, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            vm.Platforms = await db.Platforms.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
            vm.AiActuallyConfigured = await _text.IsConfiguredAsync();
            return View("Index", vm);
        }

        await _settings.SetAsync("ai.enabled", vm.AiEnabled.ToString());
        await _settings.SetAsync("ai.endpoint", vm.AiEndpoint ?? string.Empty);
        await _settings.SetAsync("ai.apiKey", vm.AiApiKey ?? string.Empty);
        await _settings.SetAsync("ai.chatModel", vm.AiChatModel ?? "gpt-4o-mini");
        await _settings.SetAsync("ai.imageModel", vm.AiImageModel ?? "dall-e-3");
        await _settings.SetAsync("ai.generateImages", vm.AiGenerateImages.ToString());
        await _settings.SetAsync("ai.timeoutSeconds", vm.AiTimeoutSeconds.ToString());
        await _settings.SetAsync("scheduler.enabled", vm.SchedulerEnabled.ToString());
        await _settings.SetAsync("scheduler.time", vm.SchedulerTime ?? "06:00");
        await SetOrgAsync("theme", vm.PosterTheme ?? "auto");
        await SetOrgAsync("name", vm.OrganizationName ?? string.Empty);
        await SetOrgAsync("city", vm.OrganizationCity ?? string.Empty);
        await SetOrgAsync("tagline", vm.OrganizationTagline ?? string.Empty);
        await SetOrgAsync("facebook", vm.OrganizationFacebook ?? string.Empty);
        await SetOrgAsync("instagram", vm.OrganizationInstagram ?? string.Empty);
        await SetOrgAsync("phones", vm.OrganizationPhones ?? string.Empty);
        await SetOrgAsync("showValues", vm.OrganizationShowValues.ToString());
        await SetOrgAsync("values", vm.OrganizationValues ?? string.Empty);

        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> GetOrgAsync(string key, string? defaultValue, string? legacyDefault = null)
    {
        var value = await _settings.GetAsync($"org.{key}", null);
        if (value is not null)
        {
            return value;
        }

        return await _settings.GetAsync($"school.{key}", defaultValue ?? legacyDefault);
    }

    private Task SetOrgAsync(string key, string value) => _settings.SetAsync($"org.{key}", value);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlatform(Platform platform, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.Platforms.FirstOrDefaultAsync(p => p.Name == platform.Name, ct);
        if (existing is null)
        {
            db.Platforms.Add(platform);
        }
        else
        {
            existing.Enabled = platform.Enabled;
            existing.WebhookUrl = platform.WebhookUrl;
            existing.AccountHandle = platform.AccountHandle;
        }

        await db.SaveChangesAsync(ct);
        TempData["Success"] = $"Platform '{platform.Name}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlatform(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var platform = await db.Platforms.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (platform is not null)
        {
            db.Platforms.Remove(platform);
            await db.SaveChangesAsync(ct);
        }

        return RedirectToAction(nameof(Index));
    }
}
