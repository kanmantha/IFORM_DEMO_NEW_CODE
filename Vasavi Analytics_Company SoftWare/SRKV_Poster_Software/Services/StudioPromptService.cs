using System.Text;
using System.Text.RegularExpressions;
using DailyPosterGenerator.Data;
using DailyPosterGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyPosterGenerator.Services;

public sealed record StudioPlan(string Title, string Sector, string OccasionKey, string Prompt);

public interface IStudioPromptService
{
    /// <summary>Turns a free-text prompt into a poster title plus the best-match business sector.</summary>
    StudioPlan Parse(string prompt, string tenantSector);

    /// <summary>Creates (and persists) a fresh style variant of a template for the given prompt.</summary>
    Task<PosterTemplate> CreateVariantAsync(StudioPlan plan, int round, int tenantId, CancellationToken ct = default);
}

public class StudioPromptService : IStudioPromptService
{
    private readonly IDbContextFactory<DailyPosterDbContext> _dbFactory;
    private readonly ILogger<StudioPromptService> _logger;

    public StudioPromptService(
        IDbContextFactory<DailyPosterDbContext> dbFactory,
        ILogger<StudioPromptService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ------------------------------------------------------------- prompt parsing

    private static readonly (string Key, string Title, string[] Words)[] Occasions =
    {
        ("womens-day", "International Women's Day", new[] { "women", "woman", "womens", "female", "girl" }),
        ("mothers-day", "Happy Mother's Day", new[] { "mother", "mom", "moms", "amma" }),
        ("fathers-day", "Happy Father's Day", new[] { "father", "dad", "dads", "appa" }),
        ("teachers-day", "Happy Teachers' Day", new[] { "teacher", "guru", "faculty" }),
        ("childrens-day", "Happy Children's Day", new[] { "children", "child", "kids", "bal" }),
        ("diwali", "Happy Diwali", new[] { "diwali", "deepavali", "festival of lights" }),
        ("holi", "Happy Holi", new[] { "holi", "colours festival" }),
        ("pongal", "Happy Pongal", new[] { "pongal", "sankranti", "lohri" }),
        ("ganesh", "Ganesh Chaturthi", new[] { "ganesh", "vinayaka", "ganesha" }),
        ("navratri", "Navratri Celebrations", new[] { "navratri", "dussehra", "durga pooja", "vijayadashami" }),
        ("eid", "Eid Mubarak", new[] { "eid", "ramadan", "ramzan" }),
        ("christmas", "Merry Christmas", new[] { "christmas", "xmas" }),
        ("newyear", "Happy New Year", new[] { "newyear", "new year eve" }),
        ("independence", "Independence Day", new[] { "independence day", "15 august", "tiranga" }),
        ("republic", "Republic Day", new[] { "republic day", "26 january" }),
        ("yoga", "International Yoga Day", new[] { "yoga" }),
        ("environment", "World Environment Day", new[] { "environment", "go green", "earth day", "plantation" }),
        ("water", "Save Water Awareness", new[] { "save water", "water conservation", "jal" }),
        ("health", "Health & Wellness Camp", new[] { "health", "medical", "doctor", "wellness", "blood donation" }),
        ("admission", "Admissions Open", new[] { "admission", "enrollment", "enrolment", "join now", "registrations open" }),
        ("exam", "Exam Announcements", new[] { "exam", "result", "timetable", "hall ticket" }),
        ("offer", "Special Offer", new[] { "offer", "sale", "discount", "deal", "combo", "promo" }),
        ("menu", "Today's Special Menu", new[] { "menu", "special thali", "today special", "chef special", "breakfast", "lunch", "dinner" }),
        ("grand-opening", "Grand Opening", new[] { "grand opening", "inauguration", "now open", "launch" }),
        ("election", "Election Campaign", new[] { "election", "vote", "campaign", "rally", "manifesto", "candidate" }),
        ("meeting", "Public Meeting", new[] { "meeting", "sabha", "conference" }),
        ("match", "Match Day", new[] { "match", "tournament", "league", "final", "championship", "fixture" }),
        ("victory", "Victory Celebration", new[] { "victory", "won", "winner", "champion", "trophy" }),
        ("birthday", "Birthday Wishes", new[] { "birthday", "janmadin" }),
        ("anniversary", "Anniversary Wishes", new[] { "anniversary", "wedding day" }),
        ("thankyou", "Thank You", new[] { "thank you", "thanks", "gratitude" }),
        ("welcome", "Warm Welcome", new[] { "welcome", "farewell", "send off" })
    };

    private static readonly (string Sector, string[] Words)[] SectorHints =
    {
        (SectorCatalog.Education, new[] { "school", "college", "student", "education", "academy", "institution", "class", "campus", "study" }),
        (SectorCatalog.Restaurant, new[] { "restaurant", "cafe", "café", "food", "dine", "dining", "menu", "coffee", "bakery", "tiffin", "hotel" }),
        (SectorCatalog.Politics, new[] { "election", "vote", "politic", "campaign", "party", "candidate", "rally", "janata", "minister", "ward" }),
        (SectorCatalog.Sports, new[] { "sports", "sport", "cricket", "football", "match", "tournament", "club", "gym", "fitness", "kabaddi" }),
        (SectorCatalog.Retail, new[] { "shop", "store", "retail", "supermarket", "sale", "offer", "discount", "boutique", "mall", "showroom" })
    };

    public StudioPlan Parse(string? prompt, string tenantSector)
    {
        var raw = (prompt ?? string.Empty).Trim();
        var lowered = Regex.Replace(raw.ToLowerInvariant(), @"\s+", " ");

        if (lowered.Length == 0)
        {
            return new StudioPlan("Today's Special Highlight", SectorCatalog.Normalize(tenantSector), "generic", raw);
        }

        var occasionKey = "generic";
        var title = string.Empty;
        foreach (var (key, occasionTitle, words) in Occasions)
        {
            if (words.Any(w => lowered.Contains(w)))
            {
                occasionKey = key;
                title = occasionTitle;
                break;
            }
        }

        if (title.Length == 0)
        {
            title = ToTitle(raw);
        }

        var sector = SectorHints.Where(h => h.Words.Any(lowered.Contains))
            .Select(h => h.Sector)
            .FirstOrDefault()
            ?? SectorCatalog.Normalize(tenantSector);

        return new StudioPlan(title, sector, occasionKey, raw);
    }

    private static string ToTitle(string text)
    {
        var clean = Regex.Replace(text, @"[\r\n]+", " ").Trim();
        if (clean.Length > 80)
        {
            clean = clean[..77].TrimEnd() + "…";
        }

        var sb = new StringBuilder(clean.Length);
        foreach (var word in clean.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
        }

        return sb.Length > 0 ? sb.ToString() : "Today's Special Highlight";
    }

    // ------------------------------------------------------------ style variants

    private static readonly (string Theme, string Accent)[] Variants =
    {
        ("colorful", "#E91E63"),
        ("dark", "#FFC107"),
        ("light", "#7B1FA2"),
        ("colorful", "#00897B"),
        ("dark", "#FF7043"),
        ("light", "#1565C0"),
        ("colorful", "#F4511E"),
        ("dark", "#26A69A"),
        ("light", "#C62828"),
        ("colorful", "#5C6BC0"),
        ("dark", "#EC407A"),
        ("light", "#2E7D32")
    };

    public async Task<PosterTemplate> CreateVariantAsync(StudioPlan plan, int round, int tenantId, CancellationToken ct = default)
    {
        var safeRound = Math.Max(1, round);
        var (theme, accent) = Variants[(safeRound - 1) % Variants.Length];

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existingCount = await db.PosterTemplates
            .CountAsync(t => t.TenantId == tenantId && !t.IsSystem &&
                             t.Name!.StartsWith(plan.Title), ct);

        var template = new PosterTemplate
        {
            TenantId = tenantId,
            IsSystem = false,
            IsActive = true,
            Name = existingCount > 0 ? $"{plan.Title} – Style {existingCount + 1}" : plan.Title,
            Description = $"Studio design {safeRound} · {SectorCatalog.Label(plan.Sector)} · {theme} theme",
            Sector = plan.Sector,
            Theme = theme,
            AccentColor = accent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.PosterTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Studio created template {TemplateId} '{Name}' ({Theme}/{Accent}) from prompt: {Prompt}",
            template.Id, template.Name, theme, accent, plan.Prompt);
        return template;
    }
}
