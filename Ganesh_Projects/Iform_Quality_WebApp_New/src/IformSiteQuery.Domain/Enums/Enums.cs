namespace IformSiteQuery.Domain.Enums;

public enum UserRole
{
    SiteEngineer = 1,
    Manager = 2
}

public enum IssueType
{
    Missing = 1,
    ProductionMistake = 2,
    DesignMistake = 3,
    DispatchMissing = 4
}

public enum QueryStatus
{
    Pending = 1,
    InProgress = 2,
    Resolved = 3
}
