using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class TicketService(AppDbContext db)
{
    public async Task<CheckInResultDto> CheckInAsync(Guid userId, bool isAdmin, CheckInRequest request, CancellationToken ct = default)
    {
        var code = request.TicketCode.Trim().ToUpperInvariant();
        var item = await db.BookingItems
            .Include(i => i.Booking).ThenInclude(b => b.Event)
            .Include(i => i.Booking).ThenInclude(b => b.User)
            .FirstOrDefaultAsync(i => i.TicketCode == code, ct);

        if (item is null)
            return new CheckInResultDto { Success = false, Message = "Ticket not found." };

        if (!isAdmin && item.Booking.Event.OrganizerId != userId)
            throw ApiException.Forbidden("This ticket belongs to another organizer's event.");

        if (item.Booking.Status != BookingStatus.Confirmed)
            return new CheckInResultDto { Success = false, Message = "This booking is not confirmed." };

        if (item.IsCheckedIn)
            return new CheckInResultDto
            {
                Success = false,
                Message = $"Already checked in at {item.CheckedInAt:HH:mm}.",
                AttendeeName = item.Booking.User.FullName,
                TicketCode = item.TicketCode,
                EventTitle = item.Booking.Event.Title,
                Seat = FormatSeat(item),
                CheckedInAt = item.CheckedInAt.AsUtc()
            };

        item.IsCheckedIn = true;
        item.CheckedInAt = DateTime.UtcNow;
        item.CheckedInByUserId = userId;
        await db.SaveChangesAsync(ct);

        return new CheckInResultDto
        {
            Success = true,
            Message = "Welcome in!",
            AttendeeName = item.Booking.User.FullName,
            TicketCode = item.TicketCode,
            EventTitle = item.Booking.Event.Title,
            Seat = FormatSeat(item),
            CheckedInAt = item.CheckedInAt.AsUtc()
        };
    }

    public async Task<PagedResult<AttendeeDto>> GetAttendeesAsync(Guid eventId, Guid userId, bool isAdmin, int page, int pageSize, string? q, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(eventId, userId, isAdmin, ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.BookingItems.AsNoTracking()
            .Where(i => i.Booking.EventId == eventId && i.Booking.Status == BookingStatus.Confirmed);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(i => i.TicketCode.ToLower().Contains(term) ||
                                     i.Booking.User.FullName.ToLower().Contains(term) ||
                                     i.Booking.User.Email!.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(i => i.Booking.CreatedAt).ThenBy(i => i.TicketCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new AttendeeDto
            {
                TicketCode = i.TicketCode,
                AttendeeName = i.Booking.User.FullName,
                Email = i.Booking.User.Email ?? string.Empty,
                Section = i.SectionName,
                Row = i.RowLabel,
                Seat = i.SeatNumber,
                BookingReference = i.Booking.Reference,
                IsCheckedIn = i.IsCheckedIn,
                CheckedInAt = i.CheckedInAt,
                BookedAt = i.Booking.CreatedAt
            }).ToListAsync(ct);

        foreach (var item in items)
        {
            item.CheckedInAt = item.CheckedInAt.AsUtc();
            item.BookedAt = item.BookedAt.AsUtc();
        }

        return new PagedResult<AttendeeDto>(items, page, pageSize, total);
    }

    public async Task<string> ExportCsvAsync(Guid eventId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var evt = await EnsureOwnerAsync(eventId, userId, isAdmin, ct);
        var rows = await db.BookingItems.AsNoTracking()
            .Where(i => i.Booking.EventId == eventId && i.Booking.Status == BookingStatus.Confirmed)
            .OrderBy(i => i.Booking.CreatedAt)
            .Select(i => new
            {
                i.TicketCode,
                Name = i.Booking.User.FullName,
                Email = i.Booking.User.Email,
                i.SectionName,
                i.RowLabel,
                i.SeatNumber,
                Reference = i.Booking.Reference,
                i.IsCheckedIn,
                i.CheckedInAt
            }).ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ticket_code,attendee,email,section,row,seat,booking_ref,checked_in,checked_in_at");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(r.TicketCode), Csv(r.Name), Csv(r.Email), Csv(r.SectionName),
                Csv(r.RowLabel), r.SeatNumber?.ToString() ?? string.Empty, Csv(r.Reference),
                r.IsCheckedIn ? "yes" : "no", r.CheckedInAt?.AsUtc().ToString("O") ?? string.Empty));
        }
        _ = evt;
        return sb.ToString();
    }

    private async Task<Event> EnsureOwnerAsync(Guid eventId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var evt = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ApiException.NotFound("Event not found.");
        if (!isAdmin && evt.OrganizerId != userId) throw ApiException.Forbidden("You do not own this event.");
        return evt;
    }

    private static string FormatSeat(BookingItem item) =>
        string.IsNullOrEmpty(item.RowLabel) ? "General admission" : $"{item.SectionName} · {item.RowLabel}{item.SeatNumber}";

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
