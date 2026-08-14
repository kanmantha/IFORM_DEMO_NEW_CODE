using System.ComponentModel.DataAnnotations;
using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public class CreateEotRequest
{
    [Required] public Guid ProjectId { get; set; }
    public string ClientEotNumber { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public EotScenario Scenario { get; set; }
    public EotCategory Category { get; set; }
    public DateTime? SpaDate { get; set; }
    public DateTime? DesignRevisionDate { get; set; }
    public decimal? DelayDays { get; set; }
    public decimal? CostEscalation { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public string? ChangeProposedBy { get; set; }
    public decimal? EstimatedTimeImpactDays { get; set; }
    public string? EstimatedCostImpact { get; set; }
    public string? Remarks { get; set; }
}

public record EotListItemDto(
    Guid Id, string EotNumber, string ProjectName, string ClientEotNumber, string FinancialYear,
    int RevisionNumber, EotScenario Scenario, EotCategory Category, decimal? DelayDays, decimal? CostEscalation,
    EotStatus Status, EotSubmissionStatus SubmissionStatus, ClientApprovalStatus ClientApproval, DateTime CreatedAt);

public record EotDetailDto(
    Guid Id, string EotNumber, Guid ProjectId, string ProjectName, string ClientEotNumber, string FinancialYear,
    int RevisionNumber, EotScenario Scenario, EotCategory Category, DateTime? SpaDate, DateTime? DesignRevisionDate,
    decimal? DelayDays, decimal? CostEscalation, EotStatus Status, EotSubmissionStatus SubmissionStatus,
    ClientApprovalStatus ClientApproval, string? Reason, string? Reference, string? ChangeProposedBy,
    decimal? EstimatedTimeImpactDays, string? EstimatedCostImpact, string? Remarks,
    IReadOnlyList<ScopeVariationDto> ScopeVariations, IReadOnlyList<EotDocumentDto> Documents,
    IReadOnlyList<EotStatusHistoryDto> StatusHistory, bool HasRequiredDocuments);

public record ScopeVariationDto(Guid Id, string? OriginalApprovedScope, string? RevisedScope, decimal ScopeAddition, decimal ScopeReduction, string? RevisionReference, string? Unit, decimal NetScopeVariation);

public record EotDocumentDto(Guid Id, DocumentCategory Category, string FileName, string FilePath, string ContentType, long SizeBytes, DateTime UploadedAt);

public record EotStatusHistoryDto(EotStatus OldStatus, EotStatus NewStatus, string ChangedByName, DateTime ChangedDateTime, string? Remarks);

public class AddScopeVariationRequest
{
    public string? OriginalApprovedScope { get; set; }
    public string? RevisedScope { get; set; }
    public decimal ScopeAddition { get; set; }
    public decimal ScopeReduction { get; set; }
    public string? RevisionReference { get; set; }
    public string? Unit { get; set; } = "nos";
}

public class UpdateEotRequest
{
    public string ClientEotNumber { get; set; } = string.Empty;
    public string FinancialYear { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public EotScenario Scenario { get; set; }
    public EotCategory Category { get; set; }
    public DateTime? SpaDate { get; set; }
    public DateTime? DesignRevisionDate { get; set; }
    public decimal? DelayDays { get; set; }
    public decimal? CostEscalation { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public string? ChangeProposedBy { get; set; }
    public decimal? EstimatedTimeImpactDays { get; set; }
    public string? EstimatedCostImpact { get; set; }
    public string? Remarks { get; set; }
}

public record EotTransitionRequest(EotStatus NewStatus, string? Remarks = null);
