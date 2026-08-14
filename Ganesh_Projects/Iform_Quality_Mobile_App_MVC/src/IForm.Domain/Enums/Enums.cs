namespace IForm.Domain.Enums;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string SiteEngineer = "SiteEngineer";

    public static readonly IReadOnlyList<string> All = new[] { SuperAdmin, TenantAdmin, Manager, SiteEngineer };

    /// <summary>Friendly labels matching the BRD role names (Site Engineer, Manager) for display in the UI.</summary>
    private static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [SuperAdmin] = "Super Admin",
        [TenantAdmin] = "Tenant Admin",
        [Manager] = "Manager",
        [SiteEngineer] = "Site Engineer"
    };

    public static string DisplayName(string role) =>
        DisplayNames.TryGetValue(role, out var name) ? name : role;
}

public enum QueryStatus
{
    Pending = 0,
    InProgress = 1,
    Resolved = 2
}

/// <summary>Issue categories carried over from the existing Excel tracker (BRD FR-1.2).</summary>
public enum IssueType
{
    Missing = 1,
    ProductionMistake = 2,
    DesignMistake = 3,
    DispatchMissing = 4
}

public enum DispatchStatus
{
    Pending = 0,
    Dispatched = 1,
    Partial = 2
}

public enum ProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ProductCategoryStatus
{
    Active = 0,
    Inactive = 1
}

public enum DocumentCategory
{
    Drawing = 1,
    RevisedDrawing = 2,
    TestReport = 3,
    InspectionReport = 4,
    ClientEmail = 5,
    Approval = 6,
    DelayReport = 7,
    ScopeVariation = 8,
    EotDocument = 9,
    Other = 10
}

public enum NotificationType
{
    QueryCreated = 1,
    QueryAssigned = 2,
    StatusChanged = 3,
    QueryResolved = 4,
    CommentAdded = 5,
    CriticalDelay = 6,
    EotSubmitted = 7,
    EotApproved = 8,
    EotRejected = 9,
    SubscriptionExpiryWarning = 10,
    SubscriptionExpired = 11,
    General = 12
}

public enum SeverityLevel
{
    Normal = 0,
    Watch = 1,
    Delayed = 2,
    Critical = 3,
    Severe = 4
}

/// <summary>EOT categories per IFAD-POL-EOT-001.</summary>
public enum EotCategory
{
    DesignRevision = 1,
    ScopeChange = 2,
    ClientInstruction = 3,
    ApprovalDelay = 4,
    SiteConstraint = 5,
    ForceMajeure = 6,
    OtherContractualEvents = 7
}

/// <summary>EOT scenarios per policy - project status at the time of design revision.</summary>
public enum EotScenario
{
    Sc1ProductionCompleted = 1,
    Sc2ProductionPartiallyCompleted = 2,
    Sc3ProductionNotStarted = 3
}

public enum EotStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    ClientSignoffPending = 3,
    ContractsReview = 4,
    Approved = 5,
    Rejected = 6,
    ReturnedForCorrection = 7,
    Cancelled = 8
}

public enum EotSubmissionStatus
{
    NotSubmitted = 0,
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    ClientSignoffPending = 4,
    ContractsReview = 5,
    Approved = 6,
    Rejected = 7
}

public enum ClientApprovalStatus
{
    NotStarted = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum SubscriptionStatus
{
    Inactive = 0,
    Trial = 1,
    Active = 2,
    GracePeriod = 3,
    Suspended = 4,
    Expired = 5,
    Cancelled = 6
}

public enum BillingCycle
{
    Monthly = 1,
    Yearly = 2
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3,
    FreeTrial = 4
}

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Overdue = 3,
    Void = 4
}

public enum SubscriptionAction
{
    Created = 1,
    Renewed = 2,
    Upgraded = 3,
    Downgraded = 4,
    Cancelled = 5,
    Expired = 6,
    Suspended = 7,
    PaymentReceived = 8,
    TrialStarted = 9
}

public enum TenantStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
    Trial = 3
}

public enum PlanTier
{
    Free = 0,
    Trial = 1,
    Starter = 2,
    Business = 3,
    Enterprise = 4
}
