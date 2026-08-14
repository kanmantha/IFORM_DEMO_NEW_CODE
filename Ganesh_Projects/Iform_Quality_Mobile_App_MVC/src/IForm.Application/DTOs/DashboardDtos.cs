using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public record DashboardDto(
    int TotalQueries, int OpenQueries, int ResolvedQueries, int CriticalQueries,
    double AverageDelayDays, int ProjectsAffected,
    IReadOnlyList<ProjectDelayDto> TopDelayedProjects,
    IReadOnlyList<IssueTypeCountDto> QueriesByIssueType,
    IReadOnlyList<StatusCountDto> QueriesByStatus,
    IReadOnlyList<MonthlyCountDto> QueriesByMonth,
    IReadOnlyList<EngineerCountDto> EngineerWise,
    double AverageResolutionDays,
    IReadOnlyList<AgingBucketDto> AgingAnalysis);

public record ProjectDelayDto(Guid ProjectId, string ProjectName, int DelayDays, int OpenQueries, int MaxDelayDays);

public record IssueTypeCountDto(IssueType IssueType, int Count);

public record StatusCountDto(QueryStatus Status, int Count);

public record MonthlyCountDto(string Month, int Count, int Resolved);

public record EngineerCountDto(string EngineerName, int OpenQueries, int ResolvedQueries, int TotalQueries, double AvgDelay);

public record AgingBucketDto(string Bucket, int Count);

public record TenantDashboardDto(
    int TotalQueries, int OpenQueries, int ResolvedQueries, int CriticalQueries,
    int ProjectsAffected, double AverageDelayDays, double AverageResolutionDays,
    int UsersUsed, int ProjectsUsed, int QueriesUsed, int ProductsUsed, long StorageUsedBytes);

public record SuperAdminDashboardDto(
    int TotalTenants, int ActiveTenants, int TrialTenants, int ExpiredTenants,
    int TotalUsers, int TotalProjects, int TotalQueries, int OpenQueries, int CriticalQueries,
    long TotalStorageBytes, IReadOnlyList<PlanDistributionDto> SubscriptionDistribution);

public record PlanDistributionDto(string PlanName, int Count);
