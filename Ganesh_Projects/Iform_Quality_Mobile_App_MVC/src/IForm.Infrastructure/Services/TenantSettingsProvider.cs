using System.Text.Json;
using IForm.Application.Common.Interfaces;
using IForm.Application.Services;
using IForm.Contracts;
using IForm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IForm.Infrastructure.Services;

/// <summary>
/// Loads per-tenant configurable feature settings. Uses defaults (escalation disabled,
/// catalogue owned by Tenant Admin, unlimited photo retention) when the tenant has not
/// customized them. All values are configurable via TenantSettings JSON.
/// </summary>
public class TenantSettingsProvider : ITenantSettingsProvider
{
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public TenantSettingsProvider(IApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public TenantFeatureConfig GetFeatures(Guid tenantId)
    {
        var defaults = new TenantFeatureConfig
        {
            EscalationEnabled = _configuration.GetValue<bool>("Features:Escalation:Enabled", false),
            EscalationDays = _configuration.GetValue<int>("Features:Escalation:Days", 10),
            EscalationRole = _configuration["Features:Escalation:Role"] ?? "TenantAdmin",
            CatalogueOwner = _configuration["Features:Catalogue:Owner"] ?? "TenantAdmin",
            PhotoRetentionMonths = _configuration.GetValue<int>("Features:Photos:RetentionMonths", 0),
            BaseUrl = _configuration["ApplicationBaseUrl"] ?? string.Empty
        };

        var row = _db.TenantSettings.AsNoTracking()
            .FirstOrDefault(s => s.TenantId == tenantId && s.Key == "features");

        if (row == null || string.IsNullOrWhiteSpace(row.Value)) return defaults;

        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(row.Value);
            if (doc.TryGetProperty("EscalationEnabled", out var esc) && esc.ValueKind == JsonValueKind.True)
                defaults = defaults with { EscalationEnabled = true };
            if (doc.TryGetProperty("EscalationDays", out var days) && days.TryGetInt32(out var d) && d > 0)
                defaults = defaults with { EscalationDays = d };
            if (doc.TryGetProperty("EscalationRole", out var role) && role.ValueKind == JsonValueKind.String)
                defaults = defaults with { EscalationRole = role.GetString() ?? defaults.EscalationRole };
            if (doc.TryGetProperty("CatalogueOwner", out var owner) && owner.ValueKind == JsonValueKind.String)
                defaults = defaults with { CatalogueOwner = owner.GetString() ?? defaults.CatalogueOwner };
            if (doc.TryGetProperty("PhotoRetentionMonths", out var ret) && ret.TryGetInt32(out var months))
                defaults = defaults with { PhotoRetentionMonths = months };
        }
        catch (JsonException)
        {
            // malformed settings fall back to defaults
        }

        return defaults;
    }

    public SeverityThresholdConfig GetSeverityThresholds(Guid tenantId)
    {
        var defaults = new SeverityThresholdConfig(
            _configuration.GetValue<int>("Features:Severity:Watch", 7),
            _configuration.GetValue<int>("Features:Severity:Delayed", 15),
            _configuration.GetValue<int>("Features:Severity:Critical", 30),
            _configuration.GetValue<int>("Features:Severity:Severe", 45));

        var row = _db.TenantSettings.AsNoTracking()
            .FirstOrDefault(s => s.TenantId == tenantId && s.Key == "severity");

        if (row == null || string.IsNullOrWhiteSpace(row.Value)) return defaults;

        try
        {
            var doc = JsonSerializer.Deserialize<JsonElement>(row.Value);
            if (doc.TryGetProperty("Watch", out var w) && w.TryGetInt32(out var watch)) defaults = defaults with { Watch = watch };
            if (doc.TryGetProperty("Delayed", out var de) && de.TryGetInt32(out var delayed)) defaults = defaults with { Delayed = delayed };
            if (doc.TryGetProperty("Critical", out var c) && c.TryGetInt32(out var critical)) defaults = defaults with { Critical = critical };
            if (doc.TryGetProperty("Severe", out var s) && s.TryGetInt32(out var severe)) defaults = defaults with { Severe = severe };
        }
        catch (JsonException) { }

        return defaults;
    }
}
