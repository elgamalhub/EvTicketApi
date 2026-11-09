using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NotificationDto>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default) =>
        notifications.GetAsync(currentUser.RequireId(), page, pageSize, unreadOnly, ct);

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) =>
        new { count = await notifications.GetUnreadCountAsync(currentUser.RequireId(), ct) };

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(currentUser.RequireId(), id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(currentUser.RequireId(), ct);
        return NoContent();
    }
}
