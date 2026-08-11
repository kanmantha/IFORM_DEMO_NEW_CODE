using Microsoft.AspNetCore.SignalR;
using SiteQueryDefectTracking.Api.Hubs;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Services;

/// <summary>
/// SignalR-backed domain event publisher. Broadcasts query changes to all connected
/// clients. Registered after the infrastructure default so it wins DI resolution.
/// </summary>
public class SignalREventPublisher(IHubContext<QueriesHub> hub) : IDomainEventPublisher
{
    public Task PublishQueryCreatedAsync(QuerySummaryDto summary, CancellationToken ct = default)
        => BroadcastAsync(HubEventNames.QueryCreated, summary, ct);

    public Task PublishQueryUpdatedAsync(QuerySummaryDto summary, CancellationToken ct = default)
        => BroadcastAsync(HubEventNames.QueryUpdated, summary, ct);

    public Task PublishQueryStatusChangedAsync(QuerySummaryDto summary, CancellationToken ct = default)
        => BroadcastAsync(HubEventNames.QueryStatusChanged, summary, ct);

    public Task PublishQueryResolvedAsync(QuerySummaryDto summary, CancellationToken ct = default)
        => BroadcastAsync(HubEventNames.QueryResolved, summary, ct);

    public Task PublishCommentAddedAsync(Guid queryId, CommentDto comment, CancellationToken ct = default)
        => hub.Clients.All.SendAsync(HubEventNames.CommentAdded, queryId, comment, ct);

    private Task BroadcastAsync(string method, QuerySummaryDto payload, CancellationToken ct)
        => hub.Clients.All.SendAsync(method, payload, ct);
}