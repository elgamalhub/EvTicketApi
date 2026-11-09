using EvTicket.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueSection> VenueSections => Set<VenueSection>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSection> EventSections => Set<EventSection>();
    public DbSet<EventSeat> EventSeats => Set<EventSeat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.AvatarUrl).HasMaxLength(500);
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ExpiresAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Venue>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.Country).HasMaxLength(120);
            e.Property(x => x.StageLabel).HasMaxLength(40);
            e.HasMany(x => x.Sections).WithOne(x => x.Venue).HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VenueSection>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.PriceMultiplier).HasPrecision(5, 2);
            e.HasIndex(x => new { x.VenueId, x.Code }).IsUnique();
        });

        b.Entity<EventCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Icon).HasMaxLength(40);
            e.Property(x => x.Color).HasMaxLength(20);
        });

        b.Entity<Event>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.BannerUrl).HasMaxLength(1000);
            e.Property(x => x.Tags).HasMaxLength(300);
            e.Property(x => x.AgeRating).HasMaxLength(20);
            e.Property(x => x.BasePrice).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasIndex(x => new { x.Status, x.StartsAt });
            e.HasIndex(x => new { x.VenueId, x.StartsAt });
            e.HasOne(x => x.Category).WithMany(x => x.Events).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Venue).WithMany(x => x.Events).HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Organizer).WithMany(x => x.OrganizedEvents).HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EventSection>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.PriceMultiplier).HasPrecision(5, 2);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasIndex(x => new { x.EventId, x.Code }).IsUnique();
            e.HasOne(x => x.Event).WithMany(x => x.Sections).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventSeat>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.RowLabel).HasMaxLength(10);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasIndex(x => new { x.EventId, x.Code }).IsUnique();
            e.HasIndex(x => new { x.EventId, x.Status });
            e.HasIndex(x => x.HeldUntil);
            e.HasOne(x => x.Event).WithMany(x => x.Seats).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Section).WithMany(x => x.Seats).HasForeignKey(x => x.EventSectionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Booking>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.Reference).IsUnique();
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.ServiceFee).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.CancellationReason).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => new { x.Status, x.ExpiresAt });
            e.HasOne(x => x.User).WithMany(x => x.Bookings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Event).WithMany(x => x.Bookings).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BookingItem>(e =>
        {
            e.Property(x => x.TicketCode).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.TicketCode).IsUnique();
            e.Property(x => x.SectionName).HasMaxLength(120);
            e.Property(x => x.RowLabel).HasMaxLength(10);
            e.Property(x => x.TicketType).HasMaxLength(40);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasOne(x => x.Booking).WithMany(x => x.Items).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EventSeat).WithMany().HasForeignKey(x => x.EventSeatId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Provider).HasMaxLength(40);
            e.Property(x => x.ProviderReference).HasMaxLength(200);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.FailureReason).HasMaxLength(500);
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne(x => x.Booking).WithOne(x => x.Payment).HasForeignKey<Payment>(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Link).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
