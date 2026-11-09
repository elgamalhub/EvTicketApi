using Microsoft.AspNetCore.Identity;

namespace EvTicket.Api.Domain;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsOrganizer { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string roleName) : base(roleName) { }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}

public enum LayoutKind
{
    Theater,
    Arena,
    Amphitheater,
    Cinema,
    Club,
    Stadium
}

public enum SectionKind
{
    Floor,
    Tier,
    Balcony,
    Box,
    Lawn,
    GeneralAdmission
}

public class Venue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "United States";
    public int Capacity { get; set; }
    public LayoutKind LayoutKind { get; set; }
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double MapWidth { get; set; } = 1000;
    public double MapHeight { get; set; } = 800;
    public double StageX { get; set; } = 340;
    public double StageY { get; set; } = 40;
    public double StageWidth { get; set; } = 320;
    public double StageHeight { get; set; } = 70;
    public string StageLabel { get; set; } = "STAGE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<VenueSection> Sections { get; set; } = new List<VenueSection>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public class VenueSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SectionKind Kind { get; set; }
    public string Color { get; set; } = "#6366f1";
    public decimal PriceMultiplier { get; set; } = 1m;
    public int SortOrder { get; set; }
    public int Capacity { get; set; }
    public string LayoutJson { get; set; } = "{}";
}

public class EventCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Icon { get; set; } = "music";
    public string Color { get; set; } = "#6366f1";
    public string? ImageUrl { get; set; }

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public enum EventStatus
{
    Draft,
    Published,
    SoldOut,
    Completed,
    Cancelled
}

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public EventCategory Category { get; set; } = null!;
    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public Guid OrganizerId { get; set; }
    public AppUser Organizer { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime DoorsOpenAt { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public string? BannerUrl { get; set; }
    public bool IsSeated { get; set; } = true;
    public bool IsFeatured { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }
    /// <summary>General-admission inventory currently held by unpaid bookings.</summary>
    public int HeldQuantity { get; set; }
    public string? Tags { get; set; }
    public string AgeRating { get; set; } = "All ages";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<EventSection> Sections { get; set; } = new List<EventSection>();
    public ICollection<EventSeat> Seats { get; set; } = new List<EventSeat>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public class EventSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid? VenueSectionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SectionKind Kind { get; set; }
    public string Color { get; set; } = "#6366f1";
    public decimal PriceMultiplier { get; set; } = 1m;
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public int Capacity { get; set; }
    /// <summary>Polygon outline (JSON array of {x,y}) used to draw the section on the seat map.</summary>
    public string GeometryJson { get; set; } = "[]";

    public ICollection<EventSeat> Seats { get; set; } = new List<EventSeat>();
}

public enum SeatStatus
{
    Available,
    Held,
    Sold,
    Blocked
}

public class EventSeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid EventSectionId { get; set; }
    public EventSection Section { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public decimal Price { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public Guid? HeldByUserId { get; set; }
    public Guid? HeldByBookingId { get; set; }
    public DateTime? HeldUntil { get; set; }
    public DateTime? SoldAt { get; set; }
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Expired
}

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal Subtotal { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
    public Payment? Payment { get; set; }
}

public class BookingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid? EventSeatId { get; set; }
    public EventSeat? EventSeat { get; set; }
    public string? SectionName { get; set; }
    public string? RowLabel { get; set; }
    public int? SeatNumber { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string TicketType { get; set; } = "General";
    public decimal Price { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public Guid? CheckedInByUserId { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Provider { get; set; } = "Demo";
    public string? ProviderReference { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureReason { get; set; }
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
