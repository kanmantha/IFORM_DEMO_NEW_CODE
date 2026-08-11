namespace SiteQueryDefectTracking.Application.DTOs.Queries;

using SiteQueryDefectTracking.Domain.Enums;

public class CreateQueryRequest
{
    public Guid ProjectId { get; set; }
    public Guid IssueTypeId { get; set; }

    public string IPO { get; set; } = string.Empty;

    public int? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }

    public Guid? VerifiedProductCodeId { get; set; }
    public string? ProductCodeText { get; set; }
    public string? DispatchStatus { get; set; }
    public string? Description { get; set; }

    public Guid? SlabId { get; set; }
    public string? SlabTarget { get; set; }
    public string? SlabCompleted { get; set; }
    public int? SlabDelayDays { get; set; }

    /// <summary>Optional: raise date override (used for legacy import). Defaults to now.</summary>
    public DateTimeOffset? RaiseDate { get; set; }
}

public class UpdateQueryRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public string? IPO { get; set; }
    public int? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }
    public Guid? VerifiedProductCodeId { get; set; }
    public string? ProductCodeText { get; set; }
    public string? DispatchStatus { get; set; }
    public string? Description { get; set; }
    public Guid? SlabId { get; set; }
    public string? SlabTarget { get; set; }
    public string? SlabCompleted { get; set; }
    public int? SlabDelayDays { get; set; }
}

public class ChangeQueryStatusRequest
{
    public QueryStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class ResolveQueryRequest
{
    public string? ResolutionNote { get; set; }
}