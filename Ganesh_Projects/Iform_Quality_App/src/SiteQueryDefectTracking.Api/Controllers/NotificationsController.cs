using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Notifications;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> Mine(CancellationToken ct)
    {
        var result = await notifications.GetMineAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("read")]
    public async Task<ActionResult<ApiResponse<object?>>> MarkRead([FromBody] MarkNotificationsReadRequest request, CancellationToken ct)
    {
        await notifications.MarkReadAsync(request, ct);
        return Ok(ApiResponse.Ok("Marked as read."));
    }
}