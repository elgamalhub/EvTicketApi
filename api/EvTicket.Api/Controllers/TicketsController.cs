using System.Text;
using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Roles = "Organizer,Admin")]
public sealed class TicketsController(TicketService tickets, CurrentUser currentUser) : ControllerBase
{
    [HttpPost("check-in")]
    public Task<CheckInResultDto> CheckIn(CheckInRequest request, CancellationToken ct) =>
        tickets.CheckInAsync(currentUser.RequireId(), currentUser.IsAdmin, request, ct);

    [HttpGet("event/{eventId:guid}")]
    public Task<PagedResult<AttendeeDto>> Attendees(
        Guid eventId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? q = null,
        CancellationToken ct = default) =>
        tickets.GetAttendeesAsync(eventId, currentUser.RequireId(), currentUser.IsAdmin, page, pageSize, q, ct);

    [HttpGet("event/{eventId:guid}/export")]
    public async Task<IActionResult> Export(Guid eventId, CancellationToken ct)
    {
        var csv = await tickets.ExportCsvAsync(eventId, currentUser.RequireId(), currentUser.IsAdmin, ct);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"attendees-{eventId:N}.csv");
    }
}
