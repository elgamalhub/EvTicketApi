using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class AnalyticsService(AppDbContext db, UserManager<AppUser> userManager)
{
    public async Task<OrganizerDashboardDto> GetOrganizerDashboardAsync(Guid organizerId, CancellationToken ct = default)
    {
        var events = await db.Events.AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.StartsAt)
            .ToListAsync(ct);
        var eventIds = events.Select(e => e.Id).ToList();

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => eventIds.Contains(b.EventId))
            .ToListAsync(ct);
        var confirmed = bookings.Where(b => b.Status == BookingStatus.Confirmed).ToList();

        var checkedIn = confirmed.Count == 0
            ? 0
            : await db.BookingItems.AsNoTracking().CountAsync(i => i.IsCheckedIn && eventIds.Contains(i.Booking.EventId), ct);

        var revenueByEvent = confirmed.GroupBy(b => b.EventId)
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(b => b.Total), Tickets: g.Sum(b => b.ItemCount)));
        var checkedByEvent = await db.BookingItems.AsNoTracking()
            .Where(i => i.IsCheckedIn && eventIds.Contains(i.Booking.EventId))
            .GroupBy(i => i.Booking.EventId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var performance = events.Select(e =>
        {
            var perf = revenueByEvent.GetValueOrDefault(e.Id);
            return new EventPerformanceDto
            {
                Id = e.Id,
                Title = e.Title,
                StartsAt = e.StartsAt,
                Status = EventService.StatusText(e, Math.Max(0, e.Capacity - e.TicketsSold)),
                Capacity = e.Capacity,
                TicketsSold = perf.Tickets,
                SellThrough = e.Capacity == 0 ? 0 : Math.Round(perf.Tickets * 100.0 / e.Capacity, 1),
                Revenue = perf.Revenue,
                CheckedIn = checkedByEvent.GetValueOrDefault(e.Id)
            };
        }).ToList();

        var recent = await db.Bookings.AsNoTracking()
            .Where(b => eventIds.Contains(b.EventId))
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
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

        foreach (var booking in recent) booking.CreatedAt = booking.CreatedAt.AsUtc();

        return new OrganizerDashboardDto
        {
            TotalEvents = events.Count,
            LiveEvents = events.Count(e => e.Status == EventStatus.Published && e.StartsAt > DateTime.UtcNow),
            TicketsSold = confirmed.Sum(b => b.ItemCount),
            GrossRevenue = confirmed.Sum(b => b.Total),
            TotalAttendees = checkedIn,
            AverageSellThrough = performance.Count == 0 ? 0 : Math.Round(performance.Average(p => p.SellThrough), 1),
            Events = performance,
            SalesByDay = await BuildSalesSeriesAsync(eventIds, confirmed, 14, ct),
            RecentBookings = recent
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().CountAsync(ct);
        var organizers = await db.Users.AsNoTracking().CountAsync(u => u.IsOrganizer, ct);
        var events = await db.Events.AsNoTracking().ToListAsync(ct);
        var bookings = await db.Bookings.AsNoTracking().ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking().ToListAsync(ct);

        var confirmed = bookings.Where(b => b.Status == BookingStatus.Confirmed).ToList();
        var gross = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var refunded = payments.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.Amount);

        var revenueByEvent = confirmed.GroupBy(b => b.EventId)
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(b => b.Total), Tickets: g.Sum(b => b.ItemCount)));

        var topEvents = events.Select(e =>
        {
            var perf = revenueByEvent.GetValueOrDefault(e.Id);
            return new EventPerformanceDto
            {
                Id = e.Id,
                Title = e.Title,
                StartsAt = e.StartsAt,
                Status = EventService.StatusText(e, Math.Max(0, e.Capacity - e.TicketsSold)),
                Capacity = e.Capacity,
                TicketsSold = perf.Tickets,
                SellThrough = e.Capacity == 0 ? 0 : Math.Round(perf.Tickets * 100.0 / e.Capacity, 1),
                Revenue = perf.Revenue
            };
        }).OrderByDescending(e => e.TicketsSold).Take(6).ToList();

        var categoryRows = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Select(b => new { b.Total, Category = b.Event.Category.Name })
            .ToListAsync(ct);

        var recentUsers = await db.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(8)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                IsOrganizer = u.IsOrganizer,
                CreatedAt = u.CreatedAt,
                BookingCount = u.Bookings.Count
            }).ToListAsync(ct);
        var recentEntities = await db.Users.AsNoTracking()
            .Where(u => recentUsers.Select(x => x.Id).Contains(u.Id))
            .ToListAsync(ct);
        foreach (var user in recentUsers)
        {
            var entity = recentEntities.First(u => u.Id == user.Id);
            user.Roles = (await userManager.GetRolesAsync(entity)).ToList();
            user.CreatedAt = user.CreatedAt.AsUtc();
        }

        return new AdminDashboardDto
        {
            TotalUsers = users,
            Organizers = organizers,
            TotalEvents = events.Count,
            PublishedEvents = events.Count(e => e.Status == EventStatus.Published),
            TotalBookings = bookings.Count,
            GrossRevenue = gross,
            Refunded = refunded,
            AverageOrderValue = confirmed.Count == 0 ? 0 : Math.Round((double)(confirmed.Sum(b => b.Total) / confirmed.Count), 2),
            RevenueByDay = await BuildSalesSeriesAsync(events.Select(e => e.Id).ToList(), confirmed, 30, ct),
            BookingsByCategory = categoryRows.GroupBy(c => c.Category)
                .Select(g => new ChartPointDto { Label = g.Key, Value = g.Sum(x => x.Total), Count = g.Count() })
                .OrderByDescending(c => c.Value).ToList(),
            TopEvents = topEvents,
            RecentUsers = recentUsers
        };
    }

    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? q, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var users = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                IsOrganizer = u.IsOrganizer,
                CreatedAt = u.CreatedAt,
                BookingCount = u.Bookings.Count
            }).ToListAsync(ct);

        var entities = await db.Users.AsNoTracking()
            .Where(u => users.Select(x => x.Id).Contains(u.Id))
            .ToListAsync(ct);
        foreach (var user in users)
        {
            var entity = entities.First(u => u.Id == user.Id);
            user.Roles = (await userManager.GetRolesAsync(entity)).ToList();
            user.CreatedAt = user.CreatedAt.AsUtc();
        }

        return new PagedResult<AdminUserDto>(users, page, pageSize, total);
    }

    public async Task SetOrganizerAsync(Guid targetUserId, bool promote, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString())
            ?? throw ApiException.NotFound("User not found.");

        user.IsOrganizer = promote;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        if (promote)
        {
            if (!await userManager.IsInRoleAsync(user, AppConstants.OrganizerRole))
                await userManager.AddToRoleAsync(user, AppConstants.OrganizerRole);
        }
        else if (await userManager.IsInRoleAsync(user, AppConstants.OrganizerRole))
        {
            await userManager.RemoveFromRoleAsync(user, AppConstants.OrganizerRole);
        }
    }

    public async Task DeleteUserAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(targetUserId.ToString())
            ?? throw ApiException.NotFound("User not found.");
        if (await db.Bookings.AnyAsync(b => b.UserId == targetUserId, ct) ||
            await db.Events.AnyAsync(e => e.OrganizerId == targetUserId, ct))
            throw ApiException.Conflict("This user has bookings or events and cannot be deleted.", "user_has_records");

        await userManager.DeleteAsync(user);
    }

    private async Task<IReadOnlyList<ChartPointDto>> BuildSalesSeriesAsync(
        List<Guid> eventIds, List<Booking> confirmed, int days, CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var rows = await db.Bookings.AsNoTracking()
            .Where(b => eventIds.Contains(b.EventId) && b.Status == BookingStatus.Confirmed && b.CreatedAt >= start)
            .Select(b => new { b.CreatedAt, b.Total })
            .ToListAsync(ct);

        var grouped = rows.GroupBy(r => r.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => (Value: g.Sum(x => x.Total), Count: g.Count()));

        var series = new List<ChartPointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var date = start.AddDays(i);
            var row = grouped.GetValueOrDefault(date);
            series.Add(new ChartPointDto
            {
                Label = date.ToString("yyyy-MM-dd"),
                Value = row.Value,
                Count = row.Count
            });
        }
        return series;
    }
}
