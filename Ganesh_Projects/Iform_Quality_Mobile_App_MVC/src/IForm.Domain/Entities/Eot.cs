using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

/// <summary>
/// EOT record per IFAD-POL-EOT-001. Sequential numbering EOT-01, EOT-02...
/// is concurrency-safe (unique index on TenantId + EotNumber).
/// </summary>
public class EotRecord : TenantEntity
{
    public string EotNumber { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string ClientEotNumber { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public EotScenario Scenario { get; set; }
    public DateTime? SpaDate { get; set; }
    public DateTime? DesignRevisionDate { get; set; }
    public EotCategory Category { get; set; }
    public decimal? DelayDays { get; set; }
    public decimal? CostEscalation { get; set; }
    public EotSubmissionStatus SubmissionStatus { get; set; } = EotSubmissionStatus.Draft;
    public ClientApprovalStatus ClientApproval { get; set; } = ClientApprovalStatus.NotStarted;
    public EotStatus Status { get; set; } = EotStatus.Draft;
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public string? ChangeProposedBy { get; set; }
    public decimal? EstimatedTimeImpactDays { get; set; }
    public string? EstimatedCostImpact { get; set; }
    public string? Remarks { get; set; }
    public Guid CreatedByUserId { get; set; }

    public ICollection<ScopeVariation> ScopeVariations { get; set; } = new List<ScopeVariation>();
    public ICollection<EotDocument> EotDocuments { get; set; } = new List<EotDocument>();
    public ICollection<EotStatusHistory> StatusHistory { get; set; } = new List<EotStatusHistory>();
    public ICollection<ClientApproval> ClientApprovals { get; set; } = new List<ClientApproval>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

public class ScopeVariation : TenantEntity
{
    public Guid EotId { get; set; }
    public EotRecord? Eot { get; set; }
    public string? OriginalApprovedScope { get; set; }
    public string? RevisedScope { get; set; }
    public decimal ScopeAddition { get; set; }
    public decimal ScopeReduction { get; set; }
    public string? RevisionReference { get; set; }
    public string? Unit { get; set; } = "nos";

    /// <summary>Net Scope Variation = Scope Addition - Scope Reduction.</summary>
    public decimal NetScopeVariation => ScopeAddition - ScopeReduction;
}

public class EotDocument : TenantEntity
{
    public Guid EotId { get; set; }
    public EotRecord? Eot { get; set; }
    public DocumentCategory Category { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
}

public class EotStatusHistory : TenantEntity
{
    public Guid EotId { get; set; }
    public EotRecord? Eot { get; set; }
    public EotStatus OldStatus { get; set; }
    public EotStatus NewStatus { get; set; }
    public Guid ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
    public DateTime ChangedDateTime { get; set; } = DateTime.UtcNow;
    public string? Remarks { get; set; }
}

public class ClientApproval : TenantEntity
{
    public Guid EotId { get; set; }
    public EotRecord? Eot { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverEmail { get; set; }
    public ClientApprovalStatus Status { get; set; }
    public string? Response { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? SupportingReference { get; set; }
}
