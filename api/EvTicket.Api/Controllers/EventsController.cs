using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(EventService events, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<EventSummaryDto>> Search([FromQuery] EventSearchQuery query, CancellationToken ct) =>
        events.SearchAsync(query, publicOnly: true, ct: ct);

    [HttpGet("featured")]
    [AllowAnonymous]
    public Task<IReadOnlyList<EventSummaryDto>> Featured([FromQuery] int take, CancellationToken ct) =>
        events.GetFeaturedAsync(take <= 0 ? 6 : take, ct);

    [HttpGet("{idOrSlug}")]
    [AllowAnonymous]
    public Task<EventDetailDto> Get(string idOrSlug, CancellationToken ct)
    {
        var userId = currentUser.IsAuthenticated ? currentUser.Id : null;
        return events.GetDetailAsync(idOrSlug, userId, currentUser.IsAdmin, ct);
    }

    [HttpGet("{idOrSlug}/seat-map")]
    [AllowAnonymous]
    public Task<SeatMapDto> SeatMap(string idOrSlug, CancellationToken ct)
    {
        var userId = currentUser.IsAuthenticated ? currentUser.Id : null;
        return events.GetSeatMapAsync(idOrSlug, userId, currentUser.IsAdmin, ct);
    }

    [HttpGet("organizer/mine")]
    [Authorize(Roles = "Organizer,Admin")]
    public Task<PagedResult<EventSummaryDto>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        events.GetOrganizerEventsAsync(currentUser.RequireId(), page, pageSize, status, ct);

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public Task<PagedResult<EventSummaryDto>> All(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        events.GetAdminEventsAsync(page, pageSize, q, status, ct);

    [HttpPost]
    [Authorize(Roles = "Organizer,Admin")]
    public async Task<ActionResult<EventDetailDto>> Create(CreateEventRequest request, CancellationToken ct)
    {
        var eventDto = await events.CreateAsync(currentUser.RequireId(), request, currentUser.IsAdmin, ct);
        return CreatedAtAction(nameof(Get), new { idOrSlug = eventDto.Slug }, eventDto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Organizer,Admin")]
    public Task<EventDetailDto> Update(Guid id, UpdateEventRequest request, CancellationToken ct) =>
        events.UpdateAsync(id, currentUser.RequireId(), request, currentUser.IsAdmin, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Organizer,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await events.DeleteAsync(id, currentUser.RequireId(), currentUser.IsAdmin, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(EventService events) : ControllerBase
{
    [HttpGet("categories")]
    [AllowAnonymous]
    public Task<IReadOnlyList<EventCategoryDto>> Categories(CancellationToken ct) =>
        events.GetCategoriesAsync(ct);

    [HttpGet("venues")]
    [AllowAnonymous]
    public Task<IReadOnlyList<VenueSummaryDto>> Venues(CancellationToken ct) =>
        events.GetVenuesAsync(ct);
}
