using System.Text.RegularExpressions;
using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using EvTicket.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class BookingService(
    AppDbContext db,
    IConfiguration configuration,
    IEmailService email,
    INotificationService notifications,
    IRealtimeNotifier realtime,
    ILogger<BookingService> logger)
{
    private decimal FeeRate => (decimal)configuration.GetValue("App:ServiceFeeRate", 0.07);
    private int HoldMinutes => configuration.GetValue("App:SeatHoldMinutes", 10);
    private int MaxSeats => configuration.GetValue("App:MaxSeatsPerOrder", 8);

    public async Task<HeldSeatsResponse> HoldAsync(Guid userId, HoldSeatsRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var evt = await db.Events.AsNoTracking()
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw ApiException.NotFound("Event not found.");

        if (evt.Status is EventStatus.Draft or EventStatus.Cancelled)
            throw ApiException.BadRequest("Tickets are not on sale for this event.");
        if (evt.StartsAt <= now)
            throw ApiException.BadRequest("This event has already started.");

        var expiresAt = now.AddMinutes(HoldMinutes);
        var booking = new Booking
        {
            Reference = NewReference(),
            UserId = userId,
            EventId = evt.Id,
            Status = BookingStatus.Pending,
            Currency = evt.Currency,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        var realtimeChanges = new List<SeatChangeDto>();

        if (evt.IsSeated)
        {
            var seatIds = (request.SeatIds ?? []).Distinct().ToList();
            if (seatIds.Count == 0)
                throw ApiException.BadRequest("Select at least one seat.");
            if (seatIds.Count > MaxSeats)
                throw ApiException.BadRequest($"You can select up to {MaxSeats} seats per order.");

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var seats = await db.EventSeats.AsNoTracking()
                .Where(s => s.EventId == evt.Id && seatIds.Contains(s.Id))
                .Select(s => new SeatSnapshot
                {
                    Id = s.Id,
                    Code = s.Code,
                    Row = s.RowLabel,
                    Number = s.SeatNumber,
                    Price = s.Price,
                    SectionName = s.Section.Name
                })
                .ToListAsync(ct);

            if (seats.Count != seatIds.Count)
                throw ApiException.BadRequest("One or more selected seats do not belong to this event.");

            // Single atomic statement: only free seats (or expired holds) can be claimed.
            var claimed = await db.EventSeats
                .Where(s => s.EventId == evt.Id && seatIds.Contains(s.Id) &&
                            (s.Status == SeatStatus.Available ||
                             (s.Status == SeatStatus.Held && s.HeldUntil != null && s.HeldUntil < now)))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, SeatStatus.Held)
                    .SetProperty(s => s.HeldByUserId, (Guid?)userId)
                    .SetProperty(s => s.HeldByBookingId, (Guid?)booking.Id)
                    .SetProperty(s => s.HeldUntil, (DateTime?)expiresAt), ct);

            if (claimed != seatIds.Count)
            {
                await tx.RollbackAsync(ct);
                var taken = await db.EventSeats.AsNoTracking()
                    .Where(s => s.EventId == evt.Id && seatIds.Contains(s.Id) && s.Status != SeatStatus.Available)
                    .Select(s => s.Code)
                    .ToListAsync(ct);
                throw ApiException.Conflict(
                    $"Those seats were just taken ({string.Join(", ", taken)}). Please choose different seats.",
                    "seats_taken");
            }

            foreach (var seat in seats.OrderBy(s => s.SectionName).ThenBy(s => s.Row).ThenBy(s => s.Number))
            {
                booking.Items.Add(new BookingItem
                {
                    EventSeatId = seat.Id,
                    SectionName = seat.SectionName,
                    RowLabel = seat.Row,
                    SeatNumber = seat.Number,
                    TicketCode = NewTicketCode(),
                    TicketType = string.IsNullOrWhiteSpace(request.TicketType) ? "General" : request.TicketType,
                    Price = seat.Price
                });
                realtimeChanges.Add(new SeatChangeDto { SeatId = seat.Id, Code = seat.Code, Status = "held" });
            }

            booking.Subtotal = seats.Sum(s => s.Price);
            booking.ItemCount = booking.Items.Count;
            booking.ServiceFee = RoundMoney(booking.Subtotal * FeeRate);
            booking.Total = booking.Subtotal + booking.ServiceFee;

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            var quantity = Math.Clamp(request.Quantity, 1, MaxSeats);
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Atomic inventory claim, safe against simultaneous requests.
            var claimed = await db.Events
                .Where(e => e.Id == evt.Id && e.Capacity - e.TicketsSold - e.HeldQuantity >= quantity)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.HeldQuantity, e => e.HeldQuantity + quantity), ct);

            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                throw ApiException.Conflict("Not enough tickets left for this event.", "sold_out");
            }

            for (var i = 0; i < quantity; i++)
            {
                booking.Items.Add(new BookingItem
                {
                    SectionName = "General Admission",
                    TicketCode = NewTicketCode(),
                    TicketType = string.IsNullOrWhiteSpace(request.TicketType) ? "General" : request.TicketType,
                    Price = evt.BasePrice
                });
            }
            booking.Subtotal = evt.BasePrice * quantity;
            booking.ItemCount = booking.Items.Count;
            booking.ServiceFee = RoundMoney(booking.Subtotal * FeeRate);
            booking.Total = booking.Subtotal + booking.ServiceFee;

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        if (realtimeChanges.Count > 0)
            await BroadcastAsync(evt.Id, realtimeChanges, ct);

        return new HeldSeatsResponse
        {
            BookingId = booking.Id,
            Reference = booking.Reference,
            ExpiresAt = expiresAt.AsUtc(),
            Subtotal = booking.Subtotal,
            ServiceFee = booking.ServiceFee,
            Total = booking.Total,
            Currency = booking.Currency,
            Tickets = booking.Items.Select(MapTicket).ToList()
        };
    }

    public async Task<BookingDto> CheckoutAsync(Guid userId, Guid bookingId, CheckoutRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var booking = await LoadBookingAsync(bookingId, ct);

        if (booking is null || booking.UserId != userId) throw ApiException.NotFound("Booking not found.");
        if (booking.Status == BookingStatus.Confirmed) return MapBooking(booking, false, now);
        if (booking.Status != BookingStatus.Pending)
            throw ApiException.Conflict("This booking is no longer awaiting payment.", "booking_closed");
        if (booking.ExpiresAt is not null && booking.ExpiresAt <= now)
        {
            await ExpireBookingAsync(booking, ct);
            throw ApiException.Conflict("Your seat hold expired. Please select seats again.", "hold_expired");
        }

        ValidatePayment(request);

        var itemCount = booking.Items.Count;
        var seatItems = booking.Items.Where(i => i.EventSeatId.HasValue).ToList();
        var seatCodes = await db.EventSeats.AsNoTracking()
            .Where(s => s.EventId == booking.EventId && seatItems.Select(i => i.EventSeatId!.Value).Contains(s.Id))
            .Select(s => new { s.Id, s.Code })
            .ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (booking.Event.IsSeated)
        {
            var ids = seatItems.Select(i => i.EventSeatId!.Value).ToList();
            var sold = await db.EventSeats
                .Where(s => s.EventId == booking.EventId && ids.Contains(s.Id) &&
                            s.Status == SeatStatus.Held && s.HeldByBookingId == booking.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, SeatStatus.Sold)
                    .SetProperty(s => s.SoldAt, (DateTime?)now)
                    .SetProperty(s => s.HeldUntil, (DateTime?)null), ct);

            if (sold != ids.Count)
            {
                await tx.RollbackAsync(ct);
                throw ApiException.Conflict("Your seat hold is no longer valid. Please choose seats again.", "hold_expired");
            }

            await db.Events.Where(e => e.Id == booking.EventId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.TicketsSold, e => e.TicketsSold + itemCount), ct);
        }
        else
        {
            var claimed = await db.Events
                .Where(e => e.Id == booking.EventId && e.HeldQuantity >= itemCount)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.HeldQuantity, e => e.HeldQuantity - itemCount)
                    .SetProperty(e => e.TicketsSold, e => e.TicketsSold + itemCount), ct);

            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                throw ApiException.Conflict("Your ticket hold is no longer valid.", "hold_expired");
            }
        }

        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAt = now;
        booking.ExpiresAt = null;
        var payment = new Payment
        {
            Provider = "Demo",
            ProviderReference = $"DEMO-{Guid.NewGuid():N}".ToUpperInvariant(),
            Amount = booking.Total,
            Status = PaymentStatus.Completed,
            RawPayload = "{\"mode\":\"demo\",\"result\":\"approved\"}",
            CreatedAt = now,
            CompletedAt = now
        };
        booking.Payment = payment;
        // A new Payment has a client-generated key, so tracking it through the
        // navigation alone would be interpreted as an existing (Modified) row.
        db.Payments.Add(payment);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var changes = seatCodes.Select(s => new SeatChangeDto { SeatId = s.Id, Code = s.Code, Status = "sold" }).ToList();
        if (changes.Count > 0) await BroadcastAsync(booking.EventId, changes, ct);

        try
        {
            await email.SendBookingConfirmationAsync(booking, booking.Event, booking.User, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking confirmation email failed for {Reference}", booking.Reference);
        }

        await notifications.CreateAsync(
            booking.UserId,
            "Booking confirmed",
            $"Your tickets for {booking.Event.Title} are ready. Reference {booking.Reference}.",
            "success",
            $"/bookings/{booking.Id}",
            ct);

        logger.LogInformation("Booking {Reference} confirmed for user {UserId}", booking.Reference, userId);
        return MapBooking(booking, false, now);
    }

    public async Task<BookingDto> ReleaseAsync(Guid userId, Guid bookingId, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(bookingId, ct)
            ?? throw ApiException.NotFound("Booking not found.");
        if (booking.UserId != userId) throw ApiException.Forbidden("This booking belongs to another user.");

        if (booking.Status == BookingStatus.Expired) return MapBooking(booking, false, DateTime.UtcNow);
        if (booking.Status != BookingStatus.Pending)
            throw ApiException.Conflict("Only pending bookings can be released.", "not_pending");

        await ExpireBookingAsync(booking, ct);
        return MapBooking(booking, false, DateTime.UtcNow);
    }

    public async Task<BookingDto> CancelAsync(Guid userId, Guid bookingId, CancelBookingRequest request, bool isAdmin = false, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var booking = await LoadBookingAsync(bookingId, ct)
            ?? throw ApiException.NotFound("Booking not found.");
        if (!isAdmin && booking.UserId != userId) throw ApiException.Forbidden("This booking belongs to another user.");

        if (booking.Status == BookingStatus.Cancelled) return MapBooking(booking, false, now);
        if (booking.Status != BookingStatus.Confirmed)
            throw ApiException.Conflict("Only confirmed bookings can be cancelled.", "not_confirmed");
        if (booking.Event.StartsAt <= now)
            throw ApiException.BadRequest("This event has already started.");
        if (booking.Event.StartsAt <= now.AddHours(24))
            throw ApiException.BadRequest("Cancellations close 24 hours before the event.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var seatIds = booking.Items.Where(i => i.EventSeatId.HasValue).Select(i => i.EventSeatId!.Value).ToList();
        if (seatIds.Count > 0)
        {
            await db.EventSeats
                .Where(s => s.EventId == booking.EventId && seatIds.Contains(s.Id) && s.Status == SeatStatus.Sold)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, SeatStatus.Available)
                    .SetProperty(s => s.SoldAt, (DateTime?)null)
                    .SetProperty(s => s.HeldByUserId, (Guid?)null)
                    .SetProperty(s => s.HeldByBookingId, (Guid?)null)
                    .SetProperty(s => s.HeldUntil, (DateTime?)null), ct);
        }

        await db.Events.Where(e => e.Id == booking.EventId && e.TicketsSold >= booking.Items.Count)
            .ExecuteUpdateAsync(set => set
                .SetProperty(e => e.TicketsSold, e => e.TicketsSold - booking.Items.Count), ct);

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Refunded;
            booking.Payment.CompletedAt = now;
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancellationReason = request.Reason;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var changes = await db.EventSeats.AsNoTracking()
            .Where(s => s.EventId == booking.EventId && seatIds.Contains(s.Id))
            .Select(s => new SeatChangeDto { SeatId = s.Id, Code = s.Code, Status = "available" })
            .ToListAsync(ct);
        if (changes.Count > 0) await BroadcastAsync(booking.EventId, changes, ct);

        await notifications.CreateAsync(
            booking.UserId,
            "Booking cancelled",
            $"Your booking for {booking.Event.Title} was cancelled. A refund is on the way.",
            "warning",
            $"/bookings/{booking.Id}",
            ct);

        logger.LogInformation("Booking {Reference} cancelled", booking.Reference);
        return MapBooking(booking, false, now);
    }

    public async Task<PagedResult<BookingSummaryDto>> GetMyBookingsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync(ct);
        var bookings = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new BookingSummaryDto
            {
                Id = b.Id,
                Reference = b.Reference,
                EventTitle = b.Event.Title,
                EventSlug = b.Event.Slug,
                EventStartsAt = b.Event.StartsAt,
                VenueName = b.Event.Venue.Name,
                BannerUrl = b.Event.BannerUrl,
                Status = b.Status.ToString().ToLower(),
                Total = b.Total,
                Currency = b.Currency,
                ItemCount = b.ItemCount,
                CreatedAt = b.CreatedAt
            }).ToListAsync(ct);

        foreach (var booking in bookings) booking.CreatedAt = booking.CreatedAt.AsUtc();

        return new PagedResult<BookingSummaryDto>(bookings, page, pageSize, total);
    }

    public async Task<BookingDto> GetByIdAsync(Guid userId, Guid bookingId, bool isAdmin = false, CancellationToken ct = default)
    {
        var booking = await LoadBookingAsync(bookingId, ct)
            ?? throw ApiException.NotFound("Booking not found.");
        if (!isAdmin && booking.UserId != userId) throw ApiException.Forbidden("This booking belongs to another user.");
        return MapBooking(booking, isAdmin, DateTime.UtcNow);
    }

    public async Task<PagedResult<AdminBookingDto>> GetAllForAdminAsync(int page, int pageSize, string? q, string? status, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Bookings.AsNoTracking().Include(b => b.User).Include(b => b.Event).ThenInclude(e => e.Venue).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Reference.ToLower().Contains(term) ||
                                     b.User.Email!.ToLower().Contains(term) ||
                                     b.Event.Title.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var parsed))
            query = query.Where(b => b.Status == parsed);

        var total = await query.CountAsync(ct);
        var bookings = await query.OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new AdminBookingDto
            {
                Id = b.Id,
                Reference = b.Reference,
                CustomerName = b.User.FullName,
                CustomerEmail = b.User.Email ?? string.Empty,
                EventTitle = b.Event.Title,
                EventStartsAt = b.Event.StartsAt,
                ItemCount = b.ItemCount,
                Total = b.Total,
                Status = b.Status.ToString().ToLower(),
                PaymentStatus = b.Payment != null ? b.Payment.Status.ToString().ToLower() : "none",
                CreatedAt = b.CreatedAt
            }).ToListAsync(ct);

        foreach (var booking in bookings) booking.CreatedAt = booking.CreatedAt.AsUtc();

        return new PagedResult<AdminBookingDto>(bookings, page, pageSize, total);
    }

    /// <summary>Called by the background worker. Releases inventory for unpaid holds.</summary>
    public async Task<int> ExpireStaleBookingsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var stale = await db.Bookings
            .Include(b => b.Items)
            .Where(b => b.Status == BookingStatus.Pending && b.ExpiresAt != null && b.ExpiresAt < now)
            .OrderBy(b => b.ExpiresAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var booking in stale)
            await ExpireBookingAsync(booking, ct);

        return stale.Count;
    }

    private async Task ExpireBookingAsync(Booking booking, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var seatIds = booking.Items.Where(i => i.EventSeatId.HasValue).Select(i => i.EventSeatId!.Value).ToList();
        var changes = new List<SeatChangeDto>();

        if (seatIds.Count > 0)
        {
            changes = await db.EventSeats.AsNoTracking()
                .Where(s => s.EventId == booking.EventId && seatIds.Contains(s.Id) && s.HeldByBookingId == booking.Id)
                .Select(s => new SeatChangeDto { SeatId = s.Id, Code = s.Code, Status = "available" })
                .ToListAsync(ct);

            await db.EventSeats
                .Where(s => s.EventId == booking.EventId && seatIds.Contains(s.Id) &&
                            s.Status == SeatStatus.Held && s.HeldByBookingId == booking.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, SeatStatus.Available)
                    .SetProperty(s => s.HeldByUserId, (Guid?)null)
                    .SetProperty(s => s.HeldByBookingId, (Guid?)null)
                    .SetProperty(s => s.HeldUntil, (DateTime?)null), ct);
        }
        else
        {
            await db.Events.Where(e => e.Id == booking.EventId && e.HeldQuantity >= booking.Items.Count)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(e => e.HeldQuantity, e => e.HeldQuantity - booking.Items.Count), ct);
        }

        booking.Status = BookingStatus.Expired;
        booking.CancelledAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (changes.Count > 0) await BroadcastAsync(booking.EventId, changes, ct);
    }

    public async Task<SeatStatsDto> GetSeatStatsAsync(Guid eventId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await db.EventSeats.AsNoTracking()
            .Where(s => s.EventId == eventId)
            .Select(s => new { s.Status, s.HeldUntil })
            .ToListAsync(ct);

        return new SeatStatsDto
        {
            Total = rows.Count,
            Available = rows.Count(s => s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil <= now)),
            Held = rows.Count(s => s.Status == SeatStatus.Held && s.HeldUntil > now),
            Sold = rows.Count(s => s.Status == SeatStatus.Sold)
        };
    }

    private async Task BroadcastAsync(Guid eventId, IReadOnlyList<SeatChangeDto> changes, CancellationToken ct)
    {
        await realtime.BroadcastSeatsAsync(eventId, changes, ct);
        await realtime.BroadcastStatsAsync(eventId, await GetSeatStatsAsync(eventId, ct), ct);
    }

    private async Task<Booking?> LoadBookingAsync(Guid id, CancellationToken ct) =>
        await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Items)
            .Include(b => b.Payment)
            .Include(b => b.Event).ThenInclude(e => e.Venue)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    private static BookingDto MapBooking(Booking b, bool isAdmin, DateTime now) => new()
    {
        Id = b.Id,
        Reference = b.Reference,
        EventId = b.EventId,
        EventTitle = b.Event.Title,
        EventSlug = b.Event.Slug,
        EventStartsAt = b.Event.StartsAt,
        VenueName = b.Event.Venue.Name,
        VenueImageUrl = b.Event.BannerUrl,
        City = b.Event.Venue.City,
        Status = b.Status.ToString().ToLowerInvariant(),
        PaymentStatus = b.Payment?.Status.ToString().ToLowerInvariant() ?? "none",
        Subtotal = b.Subtotal,
        ServiceFee = b.ServiceFee,
        Total = b.Total,
        Currency = b.Currency,
        ItemCount = b.ItemCount,
        CreatedAt = b.CreatedAt.AsUtc(),
        ExpiresAt = b.ExpiresAt.AsUtc(),
        ConfirmedAt = b.ConfirmedAt.AsUtc(),
        CancelledAt = b.CancelledAt.AsUtc(),
        CanCancel = b.Status == BookingStatus.Confirmed && b.Event.StartsAt > now.AddHours(24),
        Tickets = b.Items.Select(MapTicket).ToList()
    };

    private static TicketDto MapTicket(BookingItem item) => new()
    {
        Id = item.Id,
        Code = item.TicketCode,
        SectionName = item.SectionName,
        Row = item.RowLabel,
        SeatNumber = item.SeatNumber,
        TicketType = item.TicketType,
        Price = item.Price,
        IsCheckedIn = item.IsCheckedIn,
        CheckedInAt = item.CheckedInAt.AsUtc()
    };

    private static void ValidatePayment(CheckoutRequest request)
    {
        var method = (request.PaymentMethod ?? "card").Trim().ToLowerInvariant();
        if (method is "demo" or "cash" or "free") return;
        if (method != "card") throw ApiException.BadRequest("Unsupported payment method.");

        var digits = Regex.Replace(request.CardNumber ?? string.Empty, @"\s|-", "");
        if (digits.Length is < 13 or > 19 || !digits.All(char.IsDigit))
            throw ApiException.BadRequest("Enter a valid card number.");
        if (!PassesLuhn(digits))
            throw ApiException.BadRequest("Enter a valid card number.");
        if (digits.EndsWith("0002"))
            throw new ApiException(402, "Your card was declined by the issuer.", "card_declined");
        if (string.IsNullOrWhiteSpace(request.CardHolder))
            throw ApiException.BadRequest("Cardholder name is required.");

        var expiry = Regex.Match(request.Expiry ?? string.Empty, @"^(0[1-9]|1[0-2])\s*/\s*(\d{2}|\d{4})$");
        if (!expiry.Success) throw ApiException.BadRequest("Expiry must use MM/YY format.");
        var month = int.Parse(expiry.Groups[1].Value);
        var year = int.Parse(expiry.Groups[2].Value);
        if (year < 100) year += 2000;
        var endOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1);
        if (endOfMonth < DateTime.UtcNow.Date) throw ApiException.BadRequest("This card has expired.");

        if (string.IsNullOrWhiteSpace(request.Cvv) || request.Cvv.Length is < 3 or > 4 || !request.Cvv.All(char.IsDigit))
            throw ApiException.BadRequest("Enter a valid security code.");
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static string NewReference() => $"EVT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    private static string NewTicketCode() => $"TKT-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class SeatSnapshot
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public decimal Price { get; set; }
        public string SectionName { get; set; } = string.Empty;
    }
}
