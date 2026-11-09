using System.Globalization;
using System.Text;
using System.Text.Json;
using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class EventService(AppDbContext db, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] PublicStatuses = [nameof(EventStatus.Published), nameof(EventStatus.SoldOut)];

    public async Task<PagedResult<EventSummaryDto>> SearchAsync(EventSearchQuery filter, bool publicOnly, Guid? organizerId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 60);

        var query = db.Events.AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Venue)
            .Include(e => e.Organizer)
            .Include(e => e.Sections)
            .AsQueryable();

        if (publicOnly)
        {
            query = query.Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.SoldOut || e.Status == EventStatus.Completed);
            if (!filter.IncludePast) query = query.Where(e => e.StartsAt >= now.AddHours(-3));
            query = query.Where(e => e.Status != EventStatus.Completed || filter.IncludePast);
        }

        if (organizerId.HasValue) query = query.Where(e => e.OrganizerId == organizerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(q) ||
                (e.Summary != null && e.Summary.ToLower().Contains(q)) ||
                e.Venue.Name.ToLower().Contains(q) ||
                (e.Tags != null && e.Tags.ToLower().Contains(q)));
        }

        if (filter.CategoryId.HasValue) query = query.Where(e => e.CategoryId == filter.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Category)) query = query.Where(e => e.Category.Slug == filter.Category);
        if (!string.IsNullOrWhiteSpace(filter.City)) query = query.Where(e => e.Venue.City.ToLower() == filter.City.Trim().ToLower());
        if (!string.IsNullOrWhiteSpace(filter.Venue)) query = query.Where(e => e.Venue.Slug == filter.Venue);
        if (filter.From.HasValue) query = query.Where(e => e.StartsAt >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(e => e.StartsAt <= filter.To.Value);
        if (filter.MinPrice.HasValue) query = query.Where(e => e.BasePrice >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) query = query.Where(e => e.BasePrice <= filter.MaxPrice.Value);

        query = filter.Sort?.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(e => e.BasePrice).ThenBy(e => e.StartsAt),
            "price_desc" => query.OrderByDescending(e => e.BasePrice).ThenBy(e => e.StartsAt),
            "popular" => query.OrderByDescending(e => e.TicketsSold).ThenBy(e => e.StartsAt),
            "newest" => query.OrderByDescending(e => e.CreatedAt),
            _ => query.OrderBy(e => e.StartsAt)
        };

        var total = await query.CountAsync(ct);
        var events = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var available = await GetAvailableCountsAsync(events, ct);

        var items = events.Select(e => MapSummary(e, available.GetValueOrDefault(e.Id, 0))).ToList();
        return new PagedResult<EventSummaryDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<EventSummaryDto>> GetFeaturedAsync(int take = 6, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var events = await db.Events.AsNoTracking()
            .Include(e => e.Category).Include(e => e.Venue).Include(e => e.Organizer).Include(e => e.Sections)
            .Where(e => (e.Status == EventStatus.Published || e.Status == EventStatus.SoldOut) && e.StartsAt >= now)
            .OrderByDescending(e => e.IsFeatured)
            .ThenByDescending(e => e.TicketsSold)
            .Take(Math.Clamp(take, 1, 24))
            .ToListAsync(ct);

        var available = await GetAvailableCountsAsync(events, ct);
        return events.Select(e => MapSummary(e, available.GetValueOrDefault(e.Id, 0))).ToList();
    }

    public async Task<EventDetailDto> GetDetailAsync(string idOrSlug, Guid? requesterId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var evt = await QueryDetail().FirstOrDefaultAsync(e => e.Slug == idOrSlug, ct);
        if (evt is null && Guid.TryParse(idOrSlug, out var id))
            evt = await QueryDetail().FirstOrDefaultAsync(e => e.Id == id, ct);

        if (evt is null) throw ApiException.NotFound("Event not found.");
        if (evt.Status == EventStatus.Draft && !isAdmin && evt.OrganizerId != requesterId)
            throw ApiException.NotFound("Event not found.");

        return await BuildDetailAsync(evt, ct);
    }

    public async Task<EventDetailDto> GetDetailByIdAsync(Guid id, Guid? requesterId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var evt = await QueryDetail().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evt is null) throw ApiException.NotFound("Event not found.");
        if (evt.Status == EventStatus.Draft && !isAdmin && evt.OrganizerId != requesterId)
            throw ApiException.NotFound("Event not found.");
        return await BuildDetailAsync(evt, ct);
    }

    public async Task<SeatMapDto> GetSeatMapAsync(string idOrSlug, Guid? requesterId = null, bool isAdmin = false, CancellationToken ct = default)
    {
        var evt = await db.Events.AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Sections)
            .FirstOrDefaultAsync(e => e.Slug == idOrSlug, ct);
        if (evt is null && Guid.TryParse(idOrSlug, out var id))
            evt = await db.Events.AsNoTracking().Include(e => e.Venue).Include(e => e.Sections).FirstOrDefaultAsync(e => e.Id == id, ct);

        if (evt is null) throw ApiException.NotFound("Event not found.");
        if (evt.Status == EventStatus.Draft && !isAdmin && evt.OrganizerId != requesterId)
            throw ApiException.NotFound("Event not found.");

        if (!evt.IsSeated)
            throw ApiException.BadRequest("This is a general-admission event; a seat map is not available.");

        var seats = await db.EventSeats.AsNoTracking()
            .Where(s => s.EventId == evt.Id)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var sections = evt.Sections.OrderBy(s => s.SortOrder).Select(section => new SeatSectionDto
        {
            Id = section.Id,
            Code = section.Code,
            Name = section.Name,
            Kind = section.Kind.ToString().ToLowerInvariant(),
            Color = section.Color,
            Price = section.Price,
            Geometry = ParsePoints(section.GeometryJson),
            Seats = seats.Where(s => s.EventSectionId == section.Id).Select(s => new SeatDto
            {
                Id = s.Id,
                Code = s.Code,
                SectionCode = section.Code,
                Row = s.RowLabel,
                Number = s.SeatNumber,
                X = s.X,
                Y = s.Y,
                Rotation = s.Rotation,
                Price = s.Price,
                Status = EffectiveStatus(s, now)
            }).ToList()
        }).ToList();

        var stats = new SeatStatsDto
        {
            Total = seats.Count,
            Available = seats.Count(s => EffectiveStatus(s, now) == "available"),
            Held = seats.Count(s => EffectiveStatus(s, now) == "held"),
            Sold = seats.Count(s => s.Status == SeatStatus.Sold)
        };

        return new SeatMapDto
        {
            EventId = evt.Id,
            Title = evt.Title,
            Slug = evt.Slug,
            VenueName = evt.Venue.Name,
            City = evt.Venue.City,
            LayoutKind = evt.Venue.LayoutKind.ToString().ToLowerInvariant(),
            MapWidth = evt.Venue.MapWidth,
            MapHeight = evt.Venue.MapHeight,
            Stage = new StageDto
            {
                X = evt.Venue.StageX,
                Y = evt.Venue.StageY,
                Width = evt.Venue.StageWidth,
                Height = evt.Venue.StageHeight,
                Label = evt.Venue.StageLabel
            },
            Currency = evt.Currency,
            MaxSeatsPerOrder = configuration.GetValue("App:MaxSeatsPerOrder", 8),
            Stats = stats,
            Sections = sections
        };
    }

    public async Task<IReadOnlyList<EventCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await db.EventCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new EventCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Icon = c.Icon,
                Color = c.Color,
                ImageUrl = c.ImageUrl
            }).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VenueSummaryDto>> GetVenuesAsync(CancellationToken ct = default)
    {
        var venues = await db.Venues.AsNoTracking()
            .Include(v => v.Sections)
            .OrderBy(v => v.Name)
            .ToListAsync(ct);

        return venues.Select(v => new VenueSummaryDto
        {
            Id = v.Id,
            Name = v.Name,
            Slug = v.Slug,
            Address = v.Address,
            City = v.City,
            Country = v.Country,
            Capacity = v.Capacity,
            LayoutKind = v.LayoutKind.ToString().ToLowerInvariant(),
            ImageUrl = v.ImageUrl,
            Latitude = v.Latitude,
            Longitude = v.Longitude,
            Sections = v.Sections.OrderBy(s => s.SortOrder).Select(s => new VenueSectionDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Kind = s.Kind.ToString().ToLowerInvariant(),
                Color = s.Color,
                PriceMultiplier = s.PriceMultiplier,
                Capacity = s.Capacity
            }).ToList()
        }).ToList();
    }

    public async Task<EventDetailDto> CreateAsync(Guid organizerId, CreateEventRequest request, bool isAdmin = false, CancellationToken ct = default)
    {
        var organizer = await db.Users.FirstOrDefaultAsync(u => u.Id == organizerId, ct)
            ?? throw ApiException.NotFound("Organizer not found.");
        if (!organizer.IsOrganizer && !isAdmin)
            throw ApiException.Forbidden("Only event organizers can create events.");

        var venue = await db.Venues.Include(v => v.Sections).FirstOrDefaultAsync(v => v.Id == request.VenueId, ct)
            ?? throw ApiException.BadRequest("Venue not found.");
        if (!await db.EventCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw ApiException.BadRequest("Category not found.");

        var startsAt = Normalize(request.StartsAt);
        if (startsAt <= DateTime.UtcNow.AddMinutes(-1))
            throw ApiException.BadRequest("The event start time must be in the future.");

        var evt = new Event
        {
            Slug = await GenerateUniqueSlugAsync(request.Title, ct),
            Title = request.Title.Trim(),
            Summary = request.Summary?.Trim(),
            Description = request.Description?.Trim(),
            CategoryId = request.CategoryId,
            VenueId = venue.Id,
            OrganizerId = organizerId,
            StartsAt = startsAt,
            EndsAt = Normalize(request.EndsAt ?? startsAt.AddHours(3)),
            DoorsOpenAt = Normalize(request.DoorsOpenAt ?? startsAt.AddMinutes(-60)),
            Status = request.Publish ? EventStatus.Published : EventStatus.Draft,
            BannerUrl = request.BannerUrl,
            IsSeated = request.IsSeated && venue.Sections.Count > 0,
            IsFeatured = isAdmin && request.IsFeatured,
            BasePrice = Math.Max(0, request.BasePrice),
            Tags = request.Tags,
            AgeRating = string.IsNullOrWhiteSpace(request.AgeRating) ? "All ages" : request.AgeRating.Trim(),
            Currency = "USD"
        };

        if (evt.IsSeated)
        {
            foreach (var vs in venue.Sections.OrderBy(s => s.SortOrder))
            {
                var layout = SeatMapGenerator.Parse(vs.LayoutJson);
                var generated = SeatMapGenerator.Generate(layout);
                var price = RoundMoney(evt.BasePrice * vs.PriceMultiplier);

                var section = new EventSection
                {
                    EventId = evt.Id,
                    VenueSectionId = vs.Id,
                    Code = vs.Code,
                    Name = vs.Name,
                    Kind = vs.Kind,
                    Color = vs.Color,
                    PriceMultiplier = vs.PriceMultiplier,
                    Price = price,
                    SortOrder = vs.SortOrder,
                    Capacity = generated.Seats.Count,
                    GeometryJson = SeatMapGenerator.SerializePoints(generated.Outline)
                };
                evt.Sections.Add(section);

                foreach (var seat in generated.Seats)
                {
                    evt.Seats.Add(new EventSeat
                    {
                        EventId = evt.Id,
                        EventSectionId = section.Id,
                        Code = $"{section.Code}-{seat.RowLabel}{seat.SeatNumber}",
                        RowLabel = seat.RowLabel,
                        SeatNumber = seat.SeatNumber,
                        X = seat.X,
                        Y = seat.Y,
                        Rotation = seat.Rotation,
                        Price = price,
                        Status = SeatStatus.Available
                    });
                }
            }
            evt.Capacity = evt.Seats.Count;
        }
        else
        {
            evt.Capacity = venue.Capacity;
        }

        if (evt.Capacity <= 0) throw ApiException.BadRequest("This venue has no configured capacity.");

        db.Events.Add(evt);
        await db.SaveChangesAsync(ct);
        return await GetDetailByIdAsync(evt.Id, organizerId, isAdmin, ct);
    }

    public async Task<EventDetailDto> UpdateAsync(Guid eventId, Guid userId, UpdateEventRequest request, bool isAdmin = false, CancellationToken ct = default)
    {
        var evt = await db.Events.Include(e => e.Sections).FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ApiException.NotFound("Event not found.");
        if (!isAdmin && evt.OrganizerId != userId) throw ApiException.Forbidden("You do not own this event.");

        if (!string.IsNullOrWhiteSpace(request.Title)) evt.Title = request.Title.Trim();
        if (request.Summary is not null) evt.Summary = request.Summary.Trim();
        if (request.Description is not null) evt.Description = request.Description.Trim();
        if (request.CategoryId.HasValue)
        {
            if (!await db.EventCategories.AnyAsync(c => c.Id == request.CategoryId.Value, ct))
                throw ApiException.BadRequest("Category not found.");
            evt.CategoryId = request.CategoryId.Value;
        }
        if (request.StartsAt.HasValue) evt.StartsAt = Normalize(request.StartsAt.Value);
        if (request.EndsAt.HasValue) evt.EndsAt = Normalize(request.EndsAt.Value);
        if (request.DoorsOpenAt.HasValue) evt.DoorsOpenAt = Normalize(request.DoorsOpenAt.Value);

        if (request.BasePrice.HasValue)
        {
            if (evt.TicketsSold > 0 && request.BasePrice.Value != evt.BasePrice)
                throw ApiException.BadRequest("Pricing cannot change after tickets have been sold.");
            evt.BasePrice = Math.Max(0, request.BasePrice.Value);
            foreach (var section in evt.Sections)
            {
                section.Price = RoundMoney(evt.BasePrice * section.PriceMultiplier);
                await db.EventSeats.Where(s => s.EventSectionId == section.Id && s.Status != SeatStatus.Sold)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, section.Price), ct);
            }
        }

        if (request.BannerUrl is not null) evt.BannerUrl = request.BannerUrl;
        if (request.Tags is not null) evt.Tags = request.Tags;
        if (request.AgeRating is not null) evt.AgeRating = request.AgeRating;
        if (isAdmin && request.IsFeatured.HasValue) evt.IsFeatured = request.IsFeatured.Value;

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<EventStatus>(request.Status, true, out var status))
            evt.Status = status;

        evt.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetDetailByIdAsync(evt.Id, userId, isAdmin, ct);
    }

    public async Task DeleteAsync(Guid eventId, Guid userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var evt = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ApiException.NotFound("Event not found.");
        if (!isAdmin && evt.OrganizerId != userId) throw ApiException.Forbidden("You do not own this event.");

        if (await db.Bookings.AnyAsync(b => b.EventId == eventId, ct))
            throw ApiException.Conflict("This event already has bookings. Cancel it instead of deleting it.", "event_has_bookings");

        db.Events.Remove(evt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<EventSummaryDto>> GetOrganizerEventsAsync(Guid organizerId, int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var filter = new EventSearchQuery
        {
            Page = page,
            PageSize = pageSize,
            IncludePast = true,
            Sort = "date"
        };
        var result = await SearchAsync(filter, publicOnly: false, organizerId, ct);
        if (string.IsNullOrWhiteSpace(status)) return result;

        var filtered = result.Items.Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        return new PagedResult<EventSummaryDto>(filtered, result.Page, result.PageSize, filtered.Count);
    }

    public async Task<PagedResult<EventSummaryDto>> GetAdminEventsAsync(int page, int pageSize, string? q, string? status, CancellationToken ct = default)
    {
        var filter = new EventSearchQuery { Page = page, PageSize = pageSize, Q = q, IncludePast = true, Sort = "newest" };
        var result = await SearchAsync(filter, publicOnly: false, ct: ct);
        if (string.IsNullOrWhiteSpace(status)) return result;
        var filtered = result.Items.Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        return new PagedResult<EventSummaryDto>(filtered, result.Page, result.PageSize, filtered.Count);
    }

    public static string StatusText(Event e, int available)
    {
        if (e.Status == EventStatus.Cancelled) return "cancelled";
        if (e.Status == EventStatus.Draft) return "draft";
        if (e.StartsAt < DateTime.UtcNow) return "completed";
        if (e.Status == EventStatus.SoldOut || available <= 0) return "soldout";
        return "published";
    }

    private IQueryable<Event> QueryDetail() => db.Events.AsNoTracking()
        .Include(e => e.Category)
        .Include(e => e.Venue)
        .Include(e => e.Organizer)
        .Include(e => e.Sections);

    private async Task<EventDetailDto> BuildDetailAsync(Event evt, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var seatRows = await db.EventSeats.AsNoTracking()
            .Where(s => s.EventId == evt.Id)
            .Select(s => new { s.EventSectionId, s.Status, s.HeldUntil })
            .ToListAsync(ct);

        int Effective(SeatStatus status, DateTime? heldUntil) =>
            status == SeatStatus.Held && heldUntil <= now ? 0 : (int)status;

        var bySection = seatRows.GroupBy(s => s.EventSectionId).ToDictionary(
            g => g.Key,
            g => new
            {
                Total = g.Count(),
                Available = g.Count(s => Effective(s.Status, s.HeldUntil) == (int)SeatStatus.Available)
            });

        var availableSeats = seatRows.Count(s => Effective(s.Status, s.HeldUntil) == (int)SeatStatus.Available);
        var available = evt.IsSeated ? availableSeats : Math.Max(0, evt.Capacity - evt.TicketsSold - evt.HeldQuantity);
        var minPrice = evt.Sections.Count > 0 ? evt.Sections.Min(s => s.Price) : evt.BasePrice;

        var detail = new EventDetailDto
        {
            Id = evt.Id,
            Slug = evt.Slug,
            Title = evt.Title,
            Summary = evt.Summary,
            Description = evt.Description,
            StartsAt = evt.StartsAt,
            EndsAt = evt.EndsAt,
            DoorsOpenAt = evt.DoorsOpenAt,
            VenueName = evt.Venue.Name,
            VenueSlug = evt.Venue.Slug,
            City = evt.Venue.City,
            Address = evt.Venue.Address,
            VenueAddress = $"{evt.Venue.Address}, {evt.Venue.City}",
            Category = evt.Category.Name,
            CategorySlug = evt.Category.Slug,
            BannerUrl = evt.BannerUrl,
            BasePrice = evt.BasePrice,
            MinPrice = minPrice,
            Currency = evt.Currency,
            Capacity = evt.Capacity,
            TicketsSold = evt.TicketsSold,
            AvailableTickets = available,
            IsSeated = evt.IsSeated,
            Status = StatusText(evt, available),
            IsFeatured = evt.IsFeatured,
            AgeRating = evt.AgeRating,
            OrganizerName = evt.Organizer.FullName,
            OrganizerId = evt.OrganizerId,
            Tags = SplitTags(evt.Tags),
            Latitude = evt.Venue.Latitude,
            Longitude = evt.Venue.Longitude,
            LayoutKind = evt.Venue.LayoutKind.ToString().ToLowerInvariant(),
            MapWidth = evt.Venue.MapWidth,
            MapHeight = evt.Venue.MapHeight,
            StageX = evt.Venue.StageX,
            StageY = evt.Venue.StageY,
            StageWidth = evt.Venue.StageWidth,
            StageHeight = evt.Venue.StageHeight,
            StageLabel = evt.Venue.StageLabel,
            Sections = evt.Sections.OrderBy(s => s.SortOrder).Select(s => new EventSectionDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Kind = s.Kind.ToString().ToLowerInvariant(),
                Color = s.Color,
                Price = s.Price,
                PriceMultiplier = s.PriceMultiplier,
                Capacity = s.Capacity,
                Available = bySection.TryGetValue(s.Id, out var stats) ? stats.Available : 0
            }).ToList(),
            Stats = new SeatStatsDto
            {
                Total = seatRows.Count,
                Available = availableSeats,
                Held = seatRows.Count(s => s.Status == SeatStatus.Held && s.HeldUntil > now),
                Sold = seatRows.Count(s => s.Status == SeatStatus.Sold)
            }
        };
        return detail;
    }

    private async Task<Dictionary<Guid, int>> GetAvailableCountsAsync(List<Event> events, CancellationToken ct)
    {
        if (events.Count == 0) return new Dictionary<Guid, int>();
        var ids = events.Select(e => e.Id).ToList();
        var seatCounts = await db.EventSeats.AsNoTracking()
            .Where(s => ids.Contains(s.EventId) && s.Status == SeatStatus.Available)
            .GroupBy(s => s.EventId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return events.ToDictionary(e => e.Id, e => e.IsSeated
            ? seatCounts.GetValueOrDefault(e.Id)
            : Math.Max(0, e.Capacity - e.TicketsSold - e.HeldQuantity));
    }

    private static EventSummaryDto MapSummary(Event e, int available)
    {
        var minPrice = e.Sections.Count > 0 ? e.Sections.Min(s => s.Price) : e.BasePrice;
        return new EventSummaryDto
        {
            Id = e.Id,
            Slug = e.Slug,
            Title = e.Title,
            Summary = e.Summary,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            DoorsOpenAt = e.DoorsOpenAt,
            VenueName = e.Venue.Name,
            VenueSlug = e.Venue.Slug,
            City = e.Venue.City,
            Address = e.Venue.Address,
            Category = e.Category.Name,
            CategorySlug = e.Category.Slug,
            BannerUrl = e.BannerUrl,
            BasePrice = e.BasePrice,
            MinPrice = minPrice,
            Currency = e.Currency,
            Capacity = e.Capacity,
            TicketsSold = e.TicketsSold,
            AvailableTickets = available,
            IsSeated = e.IsSeated,
            Status = StatusText(e, available),
            IsFeatured = e.IsFeatured,
            AgeRating = e.AgeRating,
            OrganizerName = e.Organizer.FullName,
            Tags = SplitTags(e.Tags)
        };
    }

    private static List<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags) ? [] : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string EffectiveStatus(EventSeat seat, DateTime now)
    {
        if (seat.Status == SeatStatus.Held && seat.HeldUntil <= now) return "available";
        return seat.Status.ToString().ToLowerInvariant();
    }

    private static List<MapPointDto> ParsePoints(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<MapPointDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var suffix = 1;
        while (await db.Events.AnyAsync(e => e.Slug == slug, ct))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }
        return slug;
    }

    public static string Slugify(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c is '-' or '_' or '/') sb.Append('-');
        }
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? $"event-{Guid.NewGuid():N}"[..12] : slug;
    }

    private static DateTime Normalize(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
