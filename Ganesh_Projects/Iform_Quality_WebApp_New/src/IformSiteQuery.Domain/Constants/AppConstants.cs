namespace IformSiteQuery.Domain.Constants;

public static class Roles
{
    public const string SiteEngineer = "SiteEngineer";
    public const string Manager = "Manager";

    public static string GetName(IformSiteQuery.Domain.Enums.UserRole role) => role switch
    {
        IformSiteQuery.Domain.Enums.UserRole.SiteEngineer => SiteEngineer,
        IformSiteQuery.Domain.Enums.UserRole.Manager => Manager,
        _ => SiteEngineer
    };
}

public static class IssueTypeDisplay
{
    public static string GetName(IformSiteQuery.Domain.Enums.IssueType type) => type switch
    {
        IformSiteQuery.Domain.Enums.IssueType.Missing => "Missing",
        IformSiteQuery.Domain.Enums.IssueType.ProductionMistake => "Production Mistake",
        IformSiteQuery.Domain.Enums.IssueType.DesignMistake => "Design Mistake",
        IformSiteQuery.Domain.Enums.IssueType.DispatchMissing => "Dispatch Missing",
        _ => "Unknown"
    };
}

public static class StatusDisplay
{
    public static string GetName(IformSiteQuery.Domain.Enums.QueryStatus status) => status switch
    {
        IformSiteQuery.Domain.Enums.QueryStatus.Pending => "Pending",
        IformSiteQuery.Domain.Enums.QueryStatus.InProgress => "In Progress",
        IformSiteQuery.Domain.Enums.QueryStatus.Resolved => "Resolved",
        _ => "Unknown"
    };
}
