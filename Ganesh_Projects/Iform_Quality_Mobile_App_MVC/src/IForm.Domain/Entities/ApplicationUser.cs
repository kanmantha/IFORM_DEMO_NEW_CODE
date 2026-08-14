using IForm.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace IForm.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? Designation { get; set; }
    public string? MobileNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? AvatarPath { get; set; }

    public ICollection<SiteQuery> RaisedQueries { get; set; } = new List<SiteQuery>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
