using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public static class QueryTimelineBuilder
{
    public static IReadOnlyList<QueryTimelineEntry> Build(Query query)
    {
        var entries = new List<QueryTimelineEntry>
        {
            new("Created", query.Id.ToString(), query.RaiseDate, "Query raised")
        };

        foreach (var history in query.StatusHistory.OrderBy(h => h.ChangedAt))
        {
            var detail = $"{DisplayName(history.FromStatus)} -> {DisplayName(history.ToStatus)}";
            if (!string.IsNullOrWhiteSpace(history.Reason))
                detail += $" ({history.Reason})";

            entries.Add(new QueryTimelineEntry("StatusChanged", query.Id.ToString(), history.ChangedAt, detail));
        }

        if (query.Status == QueryStatus.Resolved && query.ResolvedDate.HasValue)
        {
            entries.Add(new QueryTimelineEntry("Resolved", query.Id.ToString(), query.ResolvedDate.Value,
                "Query resolved"));
        }

        return entries.OrderBy(e => e.At).ToList();
    }

    private static string DisplayName(QueryStatus status) => status switch
    {
        QueryStatus.Pending => "Pending",
        QueryStatus.InProgress => "In Progress",
        QueryStatus.Resolved => "Resolved",
        _ => status.ToString()
    };
}