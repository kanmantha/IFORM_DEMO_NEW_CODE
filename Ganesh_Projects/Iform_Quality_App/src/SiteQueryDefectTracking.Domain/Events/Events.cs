using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Domain.Events;

public abstract record DomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public record QueryCreatedEvent(Guid QueryId, Guid ProjectId, Guid IssueTypeId, Guid RaisedByUserId) : DomainEvent;

public record QueryUpdatedEvent(Guid QueryId, Guid UpdatedByUserId) : DomainEvent;

public record QueryStatusChangedEvent(Guid QueryId, QueryStatus From, QueryStatus To, Guid ChangedByUserId) : DomainEvent;

public record QueryResolvedEvent(Guid QueryId, Guid ResolvedByUserId) : DomainEvent;

public record CommentAddedEvent(Guid QueryId, Guid CommentId, Guid UserId) : DomainEvent;