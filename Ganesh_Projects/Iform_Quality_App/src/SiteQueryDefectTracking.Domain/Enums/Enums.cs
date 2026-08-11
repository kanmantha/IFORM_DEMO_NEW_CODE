namespace SiteQueryDefectTracking.Domain.Enums;

public enum QueryStatus
{
    Pending = 1,
    InProgress = 2,
    Resolved = 3
}

public enum DelaySeverity
{
    OnTime = 0,
    Minor = 1,
    Moderate = 2,
    Critical = 3
}

public enum DispatchStatus
{
    NotDispatched = 1,
    PartiallyDispatched = 2,
    Dispatched = 3
}

public enum EmailStatus
{
    Draft = 1,
    Generated = 2,
    Sent = 3,
    Failed = 4
}

public enum EmailLogStatus
{
    Draft = 1,
    Generated = 2,
    Sent = 3,
    Failed = 4
}

public enum AttachmentType
{
    Photo = 1,
    Document = 2
}

public enum NotificationType
{
    Info = 1,
    QueryCreated = 2,
    QueryStatusChanged = 3,
    CommentAdded = 4,
    QueryResolved = 5,
    CriticalDelay = 6
}