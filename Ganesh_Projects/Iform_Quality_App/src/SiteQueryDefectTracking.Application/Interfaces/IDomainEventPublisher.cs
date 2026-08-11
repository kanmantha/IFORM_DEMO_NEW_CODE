namespace SiteQueryDefectTracking.Application.Interfaces;

using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.DTOs.Shared;

/// <summary>Real-time events raised after query mutations. Consumed by SignalR.</summary>
public interface IDomainEventPublisher
{
    Task PublishQueryCreatedAsync(QuerySummaryDto summary, CancellationToken ct = default);
    Task PublishQueryUpdatedAsync(QuerySummaryDto summary, CancellationToken ct = default);
    Task PublishQueryStatusChangedAsync(QuerySummaryDto summary, CancellationToken ct = default);
    Task PublishQueryResolvedAsync(QuerySummaryDto summary, CancellationToken ct = default);
    Task PublishCommentAddedAsync(Guid queryId, CommentDto comment, CancellationToken ct = default);
}