namespace SiteQueryDefectTracking.Application.Common;

public record SortDescriptor(string? SortBy, string? SortDirection)
{
    public string AppliedSortBy => string.IsNullOrWhiteSpace(SortBy) ? "DelayDays" : SortBy;
    public bool IsDescending => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}