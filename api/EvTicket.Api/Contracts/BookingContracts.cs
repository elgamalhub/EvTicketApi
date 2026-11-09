using System.ComponentModel.DataAnnotations;

namespace EvTicket.Api.Contracts;

public class HoldSeatsRequest
{
    [Required]
    public Guid EventId { get; set; }

    /// <summary>Seat ids for seated events. Leave empty for general admission.</summary>
    public List<Guid>? SeatIds { get; set; }

    /// <summary>Number of general admission tickets (ignored when SeatIds are supplied).</summary>
    [Range(1, 20)]
    public int Quantity { get; set; } = 1;

    [MaxLength(40)]
    public string? TicketType { get; set; }
}

public class CheckoutRequest
{
    [Required, MaxLength(40)]
    public string PaymentMethod { get; set; } = "card";

    [MaxLength(30)]
    public string? CardNumber { get; set; }

    [MaxLength(120)]
    public string? CardHolder { get; set; }

    [MaxLength(7)]
    public string? Expiry { get; set; }

    [MaxLength(4)]
    public string? Cvv { get; set; }
}

public class CancelBookingRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class CheckInRequest
{
    [Required, MaxLength(40)]
    public string TicketCode { get; set; } = string.Empty;
}

public sealed class TicketDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public string? Row { get; set; }
    public int? SeatNumber { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

public sealed class BookingDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventSlug { get; set; } = string.Empty;
    public DateTime EventStartsAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string? VenueImageUrl { get; set; }
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool CanCancel { get; set; }
    public IReadOnlyList<TicketDto> Tickets { get; set; } = Array.Empty<TicketDto>();
}

public sealed class BookingSummaryDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string EventSlug { get; set; } = string.Empty;
    public DateTime EventStartsAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class HeldSeatsResponse
{
    public Guid BookingId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public IReadOnlyList<TicketDto> Tickets { get; set; } = Array.Empty<TicketDto>();
}
