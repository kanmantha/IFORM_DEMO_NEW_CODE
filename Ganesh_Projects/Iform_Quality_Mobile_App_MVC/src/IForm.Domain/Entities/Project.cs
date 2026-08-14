using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

public class Project : TenantEntity
{
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Client { get; set; }
    public string? Location { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? PlannedCompletion { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public Guid? AssignedManagerId { get; set; }
    public ApplicationUser? AssignedManager { get; set; }

    public ICollection<SiteQuery> Queries { get; set; } = new List<SiteQuery>();
    public ICollection<Ipo> Ipos { get; set; } = new List<Ipo>();
    public ICollection<ProductProjectMapping> ProductMappings { get; set; } = new List<ProductProjectMapping>();
    public ICollection<EotRecord> Eots { get; set; } = new List<EotRecord>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public string DisplayName => string.IsNullOrWhiteSpace(ProjectName) ? ProjectCode : ProjectName;
}

public class Ipo : TenantEntity
{
    public string IpoNumber { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string? Client { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? QuantitySqm { get; set; }
    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.Pending;
    public DateTime? SlabTargetCastingDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }

    /// <summary>Computed automatically from slab target / completed dates.</summary>
    public int? SlabDelayDays => SlabTargetCastingDate.HasValue
        ? Math.Max(0, (SlabCompletedDate ?? DateTime.Today).Date.Subtract(SlabTargetCastingDate.Value.Date).Days)
        : null;

    public ICollection<SiteQuery> Queries { get; set; } = new List<SiteQuery>();
}
