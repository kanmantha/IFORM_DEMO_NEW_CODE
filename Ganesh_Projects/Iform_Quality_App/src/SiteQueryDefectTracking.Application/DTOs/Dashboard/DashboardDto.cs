namespace SiteQueryDefectTracking.Application.DTOs.Dashboard;

using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.DTOs.Shared;

public record DashboardSummaryDto(
    int TotalOpenQueries,
    int Pending,
    int InProgress,
    int ResolvedTotal,
    int ResolvedToday,
    int CriticalDelays,
    double AverageDelay,
    int MaxDelay,
    int TotalQueries);

public record IssueBreakdownDto(Guid IssueTypeId, string IssueTypeName, int OpenCount, int? TotalDelayDays);

public record ProjectBreakdownDto(Guid ProjectId, string ProjectName, int OpenCount, double AverageDelay, int TotalOpenDelayDays);

public record StatusBreakdownDto(string Status, int Count);

public record DelayBucketDto(string Range, int Count);

public record OpenQueryRowDto(QuerySummaryDto Query);

public record DashboardSnapshotDto(
    DashboardSummaryDto Summary,
    IReadOnlyList<IssueBreakdownDto> Issues,
    IReadOnlyList<ProjectBreakdownDto> Projects,
    IReadOnlyList<StatusBreakdownDto> StatusDistribution,
    IReadOnlyList<DelayBucketDto> DelayDistribution,
    IReadOnlyList<QuerySummaryDto> OpenQueries);