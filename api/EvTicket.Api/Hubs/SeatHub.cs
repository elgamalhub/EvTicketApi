using EvTicket.Api.Domain;
using EvTicket.Api.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EvTicket.Api.Hubs;

[AllowAnonymous]
public class SeatHub : Hub
{
    public static string EventGroup(Guid eventId) => $"event:{eventId}";
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public async Task JoinEvent(Guid eventId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, EventGroup(eventId));
    }

    public Task LeaveEvent(Guid eventId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, EventGroup(eventId));

    [Authorize]
    public async Task JoinMyNotifications()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(raw, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
    }
}
