using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvTicket.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController(BookingService bookings, CurrentUser currentUser) : ControllerBase
{
    [HttpPost("hold")]
    public Task<HeldSeatsResponse> Hold(HoldSeatsRequest request, CancellationToken ct) =>
        bookings.HoldAsync(currentUser.RequireId(), request, ct);

    [HttpPost("{id:guid}/checkout")]
    public Task<BookingDto> Checkout(Guid id, CheckoutRequest request, CancellationToken ct) =>
        bookings.CheckoutAsync(currentUser.RequireId(), id, request, ct);

    [HttpPost("{id:guid}/release")]
    public Task<BookingDto> Release(Guid id, CancellationToken ct) =>
        bookings.ReleaseAsync(currentUser.RequireId(), id, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<BookingDto> Cancel(Guid id, CancelBookingRequest request, CancellationToken ct) =>
        bookings.CancelAsync(currentUser.RequireId(), id, request, currentUser.IsAdmin, ct);

    [HttpGet]
    public Task<PagedResult<BookingSummaryDto>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default) =>
        bookings.GetMyBookingsAsync(currentUser.RequireId(), page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<BookingDto> Get(Guid id, CancellationToken ct) =>
        bookings.GetByIdAsync(currentUser.RequireId(), id, currentUser.IsAdmin, ct);

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public Task<PagedResult<AdminBookingDto>> All(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        bookings.GetAllForAdminAsync(page, pageSize, q, status, ct);
}
