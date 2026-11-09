using System.Security.Claims;
using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Domain;
using EvTicket.Api.Hubs;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace EvTicket.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public bool IsAdmin => Principal?.IsInRole(AppConstants.AdminRole) == true;
    public bool IsOrganizer => Principal?.IsInRole(AppConstants.OrganizerRole) == true || IsAdmin;

    public Guid RequireId() => Id ?? throw ApiException.Unauthorized();
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink, CancellationToken ct = default);
    Task SendBookingConfirmationAsync(Booking booking, Event evt, AppUser user, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(Guid userId, string title, string body, string type = "info", string? link = null, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> GetAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid userId, Guid? id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

public interface IRealtimeNotifier
{
    Task BroadcastSeatsAsync(Guid eventId, IReadOnlyList<SeatChangeDto> changes, CancellationToken ct = default);
    Task BroadcastStatsAsync(Guid eventId, SeatStatsDto stats, CancellationToken ct = default);
    Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default);
}

public sealed class SeatChangeDto
{
    public Guid SeatId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class RealtimeNotifier(IHubContext<SeatHub> hub) : IRealtimeNotifier
{
    public Task BroadcastSeatsAsync(Guid eventId, IReadOnlyList<SeatChangeDto> changes, CancellationToken ct = default)
        => hub.Clients.Group(SeatHub.EventGroup(eventId))
            .SendAsync("seatStatusChanged", new { eventId, seats = changes }, ct);

    public Task BroadcastStatsAsync(Guid eventId, SeatStatsDto stats, CancellationToken ct = default)
        => hub.Clients.Group(SeatHub.EventGroup(eventId)).SendAsync("seatStatsChanged", new { eventId, stats }, ct);

    public Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
        => hub.Clients.Group(SeatHub.UserGroup(userId)).SendAsync("notification", notification, ct);
}
