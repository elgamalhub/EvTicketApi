namespace EvTicket.Api.Contracts;

public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}

public sealed class OrganizerDashboardDto
{
    public int TotalEvents { get; set; }
    public int LiveEvents { get; set; }
    public int TicketsSold { get; set; }
    public decimal GrossRevenue { get; set; }
    public int TotalAttendees { get; set; }
    public double AverageSellThrough { get; set; }
    public IReadOnlyList<EventPerformanceDto> Events { get; set; } = Array.Empty<EventPerformanceDto>();
    public IReadOnlyList<ChartPointDto> SalesByDay { get; set; } = Array.Empty<ChartPointDto>();
    public IReadOnlyList<BookingSummaryDto> RecentBookings { get; set; } = Array.Empty<BookingSummaryDto>();
}

public sealed class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int Organizers { get; set; }
    public int TotalEvents { get; set; }
    public int PublishedEvents { get; set; }
    public int TotalBookings { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal Refunded { get; set; }
    public double AverageOrderValue { get; set; }
    public IReadOnlyList<ChartPointDto> RevenueByDay { get; set; } = Array.Empty<ChartPointDto>();
    public IReadOnlyList<ChartPointDto> BookingsByCategory { get; set; } = Array.Empty<ChartPointDto>();
    public IReadOnlyList<EventPerformanceDto> TopEvents { get; set; } = Array.Empty<EventPerformanceDto>();
    public IReadOnlyList<AdminUserDto> RecentUsers { get; set; } = Array.Empty<AdminUserDto>();
}

public sealed class AdminUserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsOrganizer { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public int BookingCount { get; set; }
}

public sealed class CheckInResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AttendeeName { get; set; }
    public string? TicketCode { get; set; }
    public string? EventTitle { get; set; }
    public string? Seat { get; set; }
    public DateTime? CheckedInAt { get; set; }
}

public sealed class AttendeeDto
{
    public string TicketCode { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Section { get; set; }
    public string? Row { get; set; }
    public int? Seat { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime BookedAt { get; set; }
}

public sealed class AdminBookingDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventStartsAt { get; set; }
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
