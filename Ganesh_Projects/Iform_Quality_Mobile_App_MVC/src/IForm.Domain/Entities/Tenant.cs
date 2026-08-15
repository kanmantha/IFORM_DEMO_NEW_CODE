using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public string? TimeZone { get; set; } = "India Standard Time";
    public string? LogoPath { get; set; }
    public string? PrimaryColor { get; set; } = "#0d6efd";
    public TenantStatus Status { get; set; } = TenantStatus.Trial;

    public ICollection<TenantSetting> Settings { get; set; } = new List<TenantSetting>();
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

public class TenantSetting : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>JSON object storing the configurable feature settings of the tenant.</summary>
    public string? SettingsJson { get; set; }
}
