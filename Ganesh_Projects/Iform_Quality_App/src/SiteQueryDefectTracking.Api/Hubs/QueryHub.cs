using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SiteQueryDefectTracking.Api.Hubs;

public static class HubEventNames
{
    public const string QueryCreated = "QueryCreated";
    public const string QueryUpdated = "QueryUpdated";
    public const string QueryStatusChanged = "QueryStatusChanged";
    public const string QueryResolved = "QueryResolved";
    public const string CommentAdded = "CommentAdded";
}

[Authorize]
public class QueriesHub : Hub
{
    public const string HubPath = "/hubs/queries";

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}