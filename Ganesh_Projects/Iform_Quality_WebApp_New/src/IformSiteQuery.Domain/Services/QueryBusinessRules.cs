using IformSiteQuery.Domain.Entities;
using IformSiteQuery.Domain.Enums;

namespace IformSiteQuery.Domain.Services;

/// <summary>
/// Pure business rules for the Site Query &amp; Defect Tracking workflow.
/// Kept free of EF/dependency concerns so they are directly unit-testable.
/// </summary>
public static class QueryBusinessRules
{
    public static int CalculateDelayDays(DateTime raisedAt, QueryStatus status, DateTime? resolvedAt, DateTime utcNow)
    {
        var end = status == QueryStatus.Resolved && resolvedAt.HasValue ? resolvedAt.Value : utcNow;
        var start = raisedAt.Date;
        return Math.Max(0, (int)(end.Date - start).TotalDays);
    }

    public static int CalculateSlabDelayDays(DateTime? target, DateTime? completed)
    {
        if (!target.HasValue || !completed.HasValue)
            return 0;
        return Math.Max(0, (int)(completed.Value.Date - target.Value.Date).TotalDays);
    }

    /// <summary>Manager may resolve a query; Site Engineers cannot (FR-2.3).</summary>
    public static bool CanResolve(UserRole role) => role == UserRole.Manager;

    /// <summary>Validate that resolving advances only an open query to Resolved.</summary>
    public static bool CanTransitionToResolved(QueryStatus current) =>
        current is QueryStatus.Pending or QueryStatus.InProgress;

    /// <summary>Only a Manager may trigger/send the auto-email (Module 5 ownership).</summary>
    public static bool CanSendEmail(UserRole role) => role == UserRole.Manager;

    /// <summary>Generate the next sequential query number for a raise date.</summary>
    public static string NextQueryNumber(int year, int sequence)
        => $"QRY-{year}-{sequence:D4}";
}
