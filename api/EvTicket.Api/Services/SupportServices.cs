using System.Net;
using System.Net.Mail;
using EvTicket.Api.Common;
using EvTicket.Api.Contracts;
using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

public sealed class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    private readonly bool _enabled = configuration.GetValue("Email:Enabled", false);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            logger.LogInformation("Email disabled. Would send '{Subject}' to {Email}", subject, toEmail);
            return;
        }

        try
        {
            var host = configuration["Email:Host"] ?? "localhost";
            var port = configuration.GetValue("Email:Port", 587);
            var fromAddress = configuration["Email:FromAddress"] ?? "no-reply@evticket.dev";
            var fromName = configuration["Email:FromName"] ?? "EvTicket";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = configuration.GetValue("Email:UseSsl", true),
                Credentials = new NetworkCredential(
                    configuration["Email:UserName"],
                    configuration["Email:Password"])
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send email to {Email}", toEmail);
        }
    }

    public Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            // Demo mode: surface the reset link in the API log so the flow is testable without SMTP.
            logger.LogInformation(
                "Email disabled. Password reset link for {Email}: {ResetLink}",
                toEmail,
                resetLink);
        }

        var body = $"""
        <div style="font-family:Inter,Arial,sans-serif;max-width:560px;margin:0 auto;background:#0b1020;color:#e6e9f5;padding:32px;border-radius:16px">
          <h2 style="margin:0 0 8px">Reset your password</h2>
          <p style="color:#9aa3bd">Hi {WebUtility.HtmlEncode(fullName)},</p>
          <p style="color:#9aa3bd">We received a request to reset your EvTicket password. This link expires in 30 minutes.</p>
          <p style="margin:28px 0">
            <a href="{resetLink}" style="background:#6366f1;color:#fff;padding:14px 22px;border-radius:10px;text-decoration:none;font-weight:600">Choose a new password</a>
          </p>
          <p style="color:#66708c;font-size:13px">If you did not request this, you can safely ignore this email.</p>
        </div>
        """;
        return SendAsync(toEmail, "Reset your EvTicket password", body, ct);
    }

    public Task SendBookingConfirmationAsync(Booking booking, Event evt, AppUser user, CancellationToken ct = default)
    {
        var seats = string.Join(", ", booking.Items.Select(i =>
            string.IsNullOrEmpty(i.RowLabel) ? i.TicketType : $"{i.SectionName} {i.RowLabel}{i.SeatNumber}"));

        var body = $"""
        <div style="font-family:Inter,Arial,sans-serif;max-width:600px;margin:0 auto;background:#0b1020;color:#e6e9f5;padding:32px;border-radius:16px">
          <div style="font-size:13px;letter-spacing:.16em;color:#818cf8;text-transform:uppercase">EvTicket</div>
          <h1 style="margin:8px 0 4px;font-size:26px">You're going!</h1>
          <p style="color:#9aa3bd;margin:0 0 24px">{WebUtility.HtmlEncode(user.FullName)}, your booking is confirmed.</p>
          <div style="background:#131a30;border:1px solid #223055;border-radius:14px;padding:20px;margin-bottom:20px">
            <div style="font-size:20px;font-weight:700;margin-bottom:4px">{WebUtility.HtmlEncode(evt.Title)}</div>
            <div style="color:#9aa3bd">{evt.StartsAt:dddd, MMMM d, yyyy · h:mm tt}</div>
            <div style="color:#9aa3bd">{WebUtility.HtmlEncode(evt.Venue.Name)}, {WebUtility.HtmlEncode(evt.Venue.City)}</div>
            <hr style="border:none;border-top:1px solid #223055;margin:16px 0" />
            <div style="color:#9aa3bd">Booking reference</div>
            <div style="font-family:monospace;font-size:18px;color:#a5b4fc">{booking.Reference}</div>
            <div style="color:#9aa3bd;margin-top:12px">Seats</div>
            <div>{WebUtility.HtmlEncode(seats)}</div>
            <div style="color:#9aa3bd;margin-top:12px">Total paid</div>
            <div style="font-size:18px;font-weight:700">${booking.Total:F2} {booking.Currency}</div>
          </div>
          <p style="color:#66708c;font-size:13px">Open EvTicket to view your QR tickets. See you there!</p>
        </div>
        """;
        return SendAsync(user.Email ?? string.Empty, $"Booking confirmed · {evt.Title}", body, ct);
    }
}

public sealed class NotificationService(AppDbContext db, IRealtimeNotifier realtime) : INotificationService
{
    public async Task<NotificationDto> CreateAsync(Guid userId, string title, string body, string type = "info", string? link = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            Link = link
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(notification);
        await realtime.NotifyUserAsync(userId, dto, ct);
        return dto;
    }

    public async Task<PagedResult<NotificationDto>> GetAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                Type = n.Type,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToListAsync(ct);
        foreach (var item in items) item.CreatedAt = item.CreatedAt.AsUtc();
        return new PagedResult<NotificationDto>(items, page, pageSize, total);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid userId, Guid? id, CancellationToken ct = default)
    {
        var query = db.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (id.HasValue) query = query.Where(n => n.Id == id.Value);
        await query.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default) => MarkReadAsync(userId, null, ct);

    private static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Body = n.Body,
        Type = n.Type,
        Link = n.Link,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt.AsUtc()
    };
}
