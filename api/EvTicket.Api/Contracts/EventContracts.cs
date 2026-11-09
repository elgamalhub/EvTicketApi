using System.ComponentModel.DataAnnotations;

namespace EvTicket.Api.Contracts;

public sealed class MapPointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class VenueSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string LayoutKind { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public IReadOnlyList<VenueSectionDto> Sections { get; set; } = Array.Empty<VenueSectionDto>();
}

public sealed class VenueSectionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal PriceMultiplier { get; set; }
    public int Capacity { get; set; }
}

public sealed class EventCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}

public class EventSummaryDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime DoorsOpenAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueSlug { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }
    public decimal BasePrice { get; set; }
    public decimal MinPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }
    public int AvailableTickets { get; set; }
    public bool IsSeated { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public string AgeRating { get; set; } = string.Empty;
    public string OrganizerName { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

public sealed class EventSectionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PriceMultiplier { get; set; }
    public int Capacity { get; set; }
    public int Available { get; set; }
}

public sealed class EventDetailDto : EventSummaryDto
{
    public string? Description { get; set; }
    public string VenueAddress { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LayoutKind { get; set; } = string.Empty;
    public double MapWidth { get; set; }
    public double MapHeight { get; set; }
    public double StageX { get; set; }
    public double StageY { get; set; }
    public double StageWidth { get; set; }
    public double StageHeight { get; set; }
    public string StageLabel { get; set; } = string.Empty;
    public Guid OrganizerId { get; set; }
    public IReadOnlyList<EventSectionDto> Sections { get; set; } = Array.Empty<EventSectionDto>();
    public SeatStatsDto Stats { get; set; } = new();
}

public sealed class SeatStatsDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public int Held { get; set; }
    public int Sold { get; set; }
}

public sealed class SeatMapDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string LayoutKind { get; set; } = string.Empty;
    public double MapWidth { get; set; }
    public double MapHeight { get; set; }
    public StageDto Stage { get; set; } = new();
    public string Currency { get; set; } = "USD";
    public int MaxSeatsPerOrder { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public SeatStatsDto Stats { get; set; } = new();
    public IReadOnlyList<SeatSectionDto> Sections { get; set; } = Array.Empty<SeatSectionDto>();
}

public sealed class StageDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Label { get; set; } = "STAGE";
}

public sealed class SeatSectionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public IReadOnlyList<MapPointDto> Geometry { get; set; } = Array.Empty<MapPointDto>();
    public IReadOnlyList<SeatDto> Seats { get; set; } = Array.Empty<SeatDto>();
}

public sealed class SeatDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "available";
}

public class CreateEventRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(8000)]
    public string? Description { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid VenueId { get; set; }

    [Required]
    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }
    public DateTime? DoorsOpenAt { get; set; }

    [Range(0, 100000)]
    public decimal BasePrice { get; set; } = 49m;

    public bool IsSeated { get; set; } = true;

    [Url, MaxLength(1000)]
    public string? BannerUrl { get; set; }

    [MaxLength(300)]
    public string? Tags { get; set; }

    [MaxLength(20)]
    public string AgeRating { get; set; } = "All ages";

    public bool Publish { get; set; } = true;
    public bool IsFeatured { get; set; }
}

public class UpdateEventRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(8000)]
    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? DoorsOpenAt { get; set; }

    [Range(0, 100000)]
    public decimal? BasePrice { get; set; }

    [Url, MaxLength(1000)]
    public string? BannerUrl { get; set; }

    [MaxLength(300)]
    public string? Tags { get; set; }

    [MaxLength(20)]
    public string? AgeRating { get; set; }

    public bool? IsFeatured { get; set; }
    public string? Status { get; set; }
}

public sealed class EventSearchQuery
{
    public string? Q { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Sort { get; set; } = "date";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public bool IncludePast { get; set; }
}

public sealed class EventPerformanceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }
    public double SellThrough { get; set; }
    public decimal Revenue { get; set; }
    public int CheckedIn { get; set; }
}
