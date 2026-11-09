using EvTicket.Api.Data;
using EvTicket.Api.Domain;
using EvTicket.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Services;

/// <summary>
/// Releases unpaid seat holds and general-admission inventory every few seconds.
/// This is the safety net that keeps the live seat map accurate when buyers abandon checkout.
/// </summary>
public sealed class SeatHoldExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<SeatHoldExpiryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
                var expired = await bookings.ExpireStaleBookingsAsync(stoppingToken);

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;

                // Defensive cleanup for orphaned holds.
                await db.EventSeats
                    .Where(s => s.Status == SeatStatus.Held && s.HeldUntil != null && s.HeldUntil < now && s.HeldByBookingId == null)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.Status, SeatStatus.Available)
                        .SetProperty(s => s.HeldByUserId, (Guid?)null)
                        .SetProperty(s => s.HeldUntil, (DateTime?)null), stoppingToken);

                if (expired > 0)
                    logger.LogInformation("Expired {Count} unpaid bookings", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seat hold expiry worker failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
