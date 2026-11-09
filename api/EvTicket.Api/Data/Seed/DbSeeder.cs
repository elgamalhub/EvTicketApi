using EvTicket.Api.Common;
using EvTicket.Api.Domain;
using EvTicket.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EvTicket.Api.Data.Seed;

/// <summary>
/// Creates the demo dataset: roles, demo accounts, categories, five hand-tuned venues,
/// events with generated seat maps, and a realistic spread of confirmed bookings.
/// The seeder is idempotent — it only runs when the venue table is empty.
/// </summary>
public static class DbSeeder
{
    private sealed record EventSeed(
        string Title,
        string Category,
        string Venue,
        string Summary,
        string Description,
        int StartsInDays,
        double Hour,
        decimal BasePrice,
        bool Seated,
        bool Featured,
        string Banner,
        string Tags,
        string AgeRating);

    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        var password = config["Seed:DemoPassword"] ?? "Demo@12345";

        foreach (var role in AppConstants.Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new AppRole(role));
        }

        var admin = await EnsureUserAsync(userManager, "admin@evticket.dev", "Ava Morgan", password, [AppConstants.AdminRole, AppConstants.UserRole], organizer: true);
        var organizer = await EnsureUserAsync(userManager, "organizer@evticket.dev", "Noah Bennett", password, [AppConstants.OrganizerRole, AppConstants.UserRole], organizer: true);
        var demo = await EnsureUserAsync(userManager, "demo@evticket.dev", "Mia Rodriguez", password, [AppConstants.UserRole], organizer: false);

        var attendees = new List<AppUser> { demo };
        var attendeeSeeds = new (string Email, string Name)[]
        {
            ("james.okafor@example.com", "James Okafor"),
            ("sofia.lindqvist@example.com", "Sofia Lindqvist"),
            ("kenji.tanaka@example.com", "Kenji Tanaka"),
            ("priya.sharma@example.com", "Priya Sharma"),
            ("lucas.martin@example.com", "Lucas Martin"),
            ("emma.walsh@example.com", "Emma Walsh"),
            ("daniel.rossi@example.com", "Daniel Rossi"),
            ("aisha.bello@example.com", "Aisha Bello"),
            ("mateo.silva@example.com", "Mateo Silva"),
            ("hannah.kim@example.com", "Hannah Kim")
        };
        foreach (var (email, name) in attendeeSeeds)
            attendees.Add(await EnsureUserAsync(userManager, email, name, password, [AppConstants.UserRole], organizer: false));

        if (await db.Venues.AnyAsync(ct))
        {
            logger.LogInformation("Demo data already present — skipping seed.");
            return;
        }

        // ---------- Categories ----------
        var categories = new List<EventCategory>
        {
            new() { Name = "Music", Slug = "music", Icon = "music", Color = "#8b5cf6", ImageUrl = "https://images.unsplash.com/photo-1493225457124-a3eb161ffa5f?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Theater", Slug = "theater", Icon = "drama", Color = "#f59e0b", ImageUrl = "https://images.unsplash.com/photo-1503095396549-807759245b35?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Comedy", Slug = "comedy", Icon = "mic", Color = "#ec4899", ImageUrl = "https://images.unsplash.com/photo-1585699324551-f6c309eedeca?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Sports", Slug = "sports", Icon = "trophy", Color = "#22c55e", ImageUrl = "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Film", Slug = "film", Icon = "film", Color = "#06b6d4", ImageUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Tech", Slug = "tech", Icon = "cpu", Color = "#6366f1", ImageUrl = "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1200&q=80" },
            new() { Name = "Arts & Culture", Slug = "arts", Icon = "palette", Color = "#f43f5e", ImageUrl = "https://images.unsplash.com/photo-1465847899084-d164df4dedc6?auto=format&fit=crop&w=1200&q=80" }
        };
        db.EventCategories.AddRange(categories);

        // ---------- Venues ----------
        var venueSeeds = VenueCatalog.Build();
        foreach (var seed in venueSeeds)
        {
            foreach (var section in seed.Sections)
                seed.Venue.Sections.Add(section);
            db.Venues.Add(seed.Venue);
        }
        await db.SaveChangesAsync(ct);

        var venuesBySlug = venueSeeds.ToDictionary(v => v.Venue.Slug, v => v.Venue);
        var categoriesBySlug = categories.ToDictionary(c => c.Slug, c => c);

        // ---------- Events ----------
        var eventSeeds = new List<EventSeed>
        {
            new("Aurora Nights — Synthwave Live", "music", "skyline-arena",
                "A full 360° audiovisual production with a live synth ensemble, laser architecture and a 40-metre LED halo.",
                "Aurora Nights returns with a brand-new stage designed for arenas. Expect analog synths, a live string section, and a light show built around a 40-metre LED halo suspended above the floor.\n\nThe night opens with an ambient set from resident DJ Kite, followed by the headline performance and a late-night encore. Doors open 90 minutes before the show — arrive early for the immersive lobby installation.",
                12, 20, 89m, true, true,
                "https://images.unsplash.com/photo-1470229722913-7c0e2dbbafd3?auto=format&fit=crop&w=1600&q=80",
                "synthwave, live music, immersive, arena", "All ages"),

            new("Hamilton — The Revolution", "theater", "grand-majestic-theater",
                "The Tony-winning phenomenon returns to the Grand Majestic for a strictly limited six-week engagement.",
                "The story of Alexander Hamilton, told through hip-hop, jazz, R&B and Broadway. This limited engagement features a brand-new cast and the original choreography, staged in the beautifully restored Grand Majestic Theater.\n\nRunning time: 2 hours 45 minutes including one intermission. Latecomers will be seated at an appropriate break in the performance.",
                21, 19.5, 120m, true, true,
                "https://images.unsplash.com/photo-1503095396549-807759245b35?auto=format&fit=crop&w=1600&q=80",
                "broadway, musical, limited run, award-winning", "Recommended 10+"),

            new("Midnight Laughs — Live Stand-Up", "comedy", "velvet-loft",
                "An intimate late show with three headline comedians and a surprise guest. Strictly no phones, all laughter.",
                "Midnight Laughs is the late-night comedy room the city keeps talking about. Three headline acts, one surprise guest, and a host who keeps the show moving.\n\nSeating is cabaret style around the stage. The bar stays open through the show; tables of two and four can be reserved with a seat selection. This show contains strong language and adult themes.",
                6, 21, 65m, true, true,
                "https://images.unsplash.com/photo-1585699324551-f6c309eedeca?auto=format&fit=crop&w=1600&q=80",
                "stand-up, late night, intimate, 18+", "18+"),

            new("Riverside Jazz Festival", "music", "riverside-amphitheater",
                "Two stages, sunset sets and a riverside lawn — the city's favourite open-air jazz weekend.",
                "The Riverside Jazz Festival brings together modern jazz, soul and brass across two stages, framed by the river and the evening skyline.\n\nThe main amphitheater hosts the headline sets; the lawn stage runs between acts with local ensembles. Food trucks and a craft bar line the river walk. Gates open at 15:00 and the music runs until 23:00.",
                34, 17, 55m, true, true,
                "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?auto=format&fit=crop&w=1600&q=80",
                "jazz, festival, outdoor, sunset", "All ages"),

            new("Interstellar — IMAX Experience", "film", "nova-cinema",
                "Christopher Nolan's modern classic, remastered for laser IMAX with a live pre-show introduction.",
                "See Interstellar the way it was meant to be seen: 1.43:1 laser IMAX projection and a Dolby Atmos mix calibrated by the Nova engineering team.\n\nThe screening includes a 20-minute pre-show introduction on the science behind the film, presented by the Nova Cinema & Dolby Lab. Premium recliners in the Premiere Row include in-seat service.",
                3, 18.5, 24m, true, false,
                "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=1600&q=80",
                "imax, sci-fi, remaster, dolby atmos", "PG-13"),

            new("Global Tech Summit 2026", "tech", "skyline-arena",
                "Three days of keynotes, deep-dive workshops and a startup arena — general admission passes available now.",
                "Global Tech Summit brings 4,000 builders, founders and researchers together for three days of talks, workshops and hallway conversations that turn into companies.\n\nYour pass includes all keynotes, the expo floor, the startup arena and the opening night mixer. Workshop seats are reserved separately inside the app after you register.",
                45, 9, 299m, false, true,
                "https://images.unsplash.com/photo-1540575467063-178a50c2df87?auto=format&fit=crop&w=1600&q=80",
                "conference, startups, engineering, keynote", "All ages"),

            new("Champions Watch Party", "sports", "skyline-arena",
                "The final on a 12-metre screen with courtside-style lounges, food trucks and full arena sound.",
                "Watch the championship final with thousands of fans on the largest screen in the city. The arena floor is transformed into a lounge with courtside-style seating, food trucks and a full bar.\n\nGeneral admission standing and seated lounge tickets are available. Doors open one hour before tip-off; the broadcast starts 30 minutes before the game.",
                9, 19, 45m, false, false,
                "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?auto=format&fit=crop&w=1600&q=80",
                "watch party, finals, big screen, arena", "All ages"),

            new("Symphony Under the Stars", "arts", "riverside-amphitheater",
                "An open-air evening of Mahler and film scores, performed by the Riverside Philharmonic.",
                "The Riverside Philharmonic returns for its signature summer concert: an evening of Mahler, contemporary strings and iconic film scores under the open sky.\n\nArrive early for the chamber ensemble on the lawn stage. Blankets and low chairs are welcome on the lawn; the amphitheater seats are reserved. The performance ends with a fireworks finale, weather permitting.",
                18, 19, 49m, true, false,
                "https://images.unsplash.com/photo-1465847899084-d164df4dedc6?auto=format&fit=crop&w=1600&q=80",
                "orchestra, classical, outdoor, family", "All ages"),

            new("Neon Jungle — Club Night", "music", "velvet-loft",
                "Four DJs, one wraparound mezzanine and a Funktion-One sound system tuned for the room.",
                "Neon Jungle takes over the Velvet Loft for a full-room club night: four DJs across house, breaks and garage, with the mezzanine open until close.\n\nA limited number of VIP tables include dedicated service and a private viewing rail over the floor. Last entry is 01:00. This is a 21+ event with valid ID required.",
                4, 22, 39m, true, false,
                "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=1600&q=80",
                "club, house music, late night, dj", "21+"),

            new("The Nutcracker — Winter Ballet", "theater", "grand-majestic-theater",
                "The full classical production with a live orchestra, snow scene and a hand-painted winter set.",
                "The Nutcracker returns to the Grand Majestic with a live orchestra, a company of forty dancers and the theatre's hand-painted winter set.\n\nThis is the full classical production in two acts, suitable for the whole family. A relaxed matinee performance is available on select weekends.",
                52, 19, 95m, true, false,
                "https://images.unsplash.com/photo-1514320291840-2e0a9bf2a9ae?auto=format&fit=crop&w=1600&q=80",
                "ballet, classical, family, winter", "All ages"),

            new("Indie Film Showcase", "film", "nova-cinema",
                "Six short films, one feature and a live Q&A with the directors in the Dolby Lab.",
                "A curated evening of independent cinema: six short films from emerging directors, one acclaimed feature, and a live Q&A hosted by the Nova programming team.\n\nYour ticket includes the full programme, a drink from the lobby bar and access to the after-show mixer in the lounge. Seats are limited to keep the Q&A intimate.",
                15, 20, 18m, true, false,
                "https://images.unsplash.com/photo-1440404653325-ab127d49abc1?auto=format&fit=crop&w=1600&q=80",
                "indie, shorts, q&a, independent film", "16+")
        };

        var events = new List<Event>();
        var seatsByEvent = new Dictionary<Guid, List<EventSeat>>();
        var now = DateTime.UtcNow;

        foreach (var seed in eventSeeds)
        {
            var venue = venuesBySlug[seed.Venue];
            var category = categoriesBySlug[seed.Category];
            var startsAt = now.Date.AddDays(seed.StartsInDays).AddHours(seed.Hour);

            var evt = new Event
            {
                Slug = EventService.Slugify(seed.Title),
                Title = seed.Title,
                Summary = seed.Summary,
                Description = seed.Description,
                CategoryId = category.Id,
                VenueId = venue.Id,
                OrganizerId = organizer.Id,
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(3),
                DoorsOpenAt = startsAt.AddMinutes(-60),
                Status = EventStatus.Published,
                BannerUrl = seed.Banner,
                IsSeated = seed.Seated,
                IsFeatured = seed.Featured,
                BasePrice = seed.BasePrice,
                Currency = "USD",
                Tags = seed.Tags,
                AgeRating = seed.AgeRating,
                CreatedAt = now.AddDays(-20)
            };

            var seats = new List<EventSeat>();
            if (seed.Seated)
            {
                foreach (var vs in venue.Sections.OrderBy(s => s.SortOrder))
                {
                    var layout = SeatMapGenerator.Parse(vs.LayoutJson);
                    var generated = SeatMapGenerator.Generate(layout);
                    var price = Math.Round(seed.BasePrice * vs.PriceMultiplier, 2, MidpointRounding.AwayFromZero);

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
                        var entity = new EventSeat
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
                        };
                        evt.Seats.Add(entity);
                        seats.Add(entity);
                    }
                }
                evt.Capacity = evt.Seats.Count;
            }
            else
            {
                evt.Capacity = venue.Capacity;
            }

            events.Add(evt);
            seatsByEvent[evt.Id] = seats;
            db.Events.Add(evt);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Venues} venues and {Events} events with {Seats} seats",
            venueSeeds.Count, events.Count, seatsByEvent.Values.Sum(s => s.Count));

        // ---------- Bookings ----------
        var random = new Random(20261107);
        var salesStart = now.AddDays(-18);
        var bookingCount = 0;

        foreach (var evt in events)
        {
            var seats = seatsByEvent[evt.Id];
            var bookingsForEvent = random.Next(5, 12);
            var usedSeatIndexes = new HashSet<int>();

            for (var i = 0; i < bookingsForEvent; i++)
            {
                var buyer = attendees[random.Next(attendees.Count)];
                var createdAt = salesStart.AddHours(random.NextDouble() * 18 * 24);
                if (createdAt > now.AddHours(-1)) createdAt = now.AddHours(-random.Next(1, 20));

                var booking = new Booking
                {
                    Reference = NewReference(),
                    UserId = buyer.Id,
                    EventId = evt.Id,
                    Status = BookingStatus.Confirmed,
                    Currency = "USD",
                    CreatedAt = createdAt,
                    ConfirmedAt = createdAt.AddMinutes(random.Next(1, 8))
                };

                List<EventSeat> chosen = [];
                if (evt.IsSeated && seats.Count > 0)
                {
                    var count = Math.Min(random.Next(1, 4), 6);
                    for (var attempt = 0; attempt < 40 && chosen.Count < count; attempt++)
                    {
                        var index = random.Next(seats.Count);
                        if (usedSeatIndexes.Add(index)) chosen.Add(seats[index]);
                    }
                }

                var quantity = evt.IsSeated ? Math.Max(1, chosen.Count) : random.Next(1, 4);

                foreach (var seat in chosen)
                {
                    seat.Status = SeatStatus.Sold;
                    seat.SoldAt = createdAt;
                    seat.HeldByBookingId = booking.Id;
                    seat.HeldByUserId = buyer.Id;
                    seat.HeldUntil = null;

                    booking.Items.Add(new BookingItem
                    {
                        BookingId = booking.Id,
                        EventSeatId = seat.Id,
                        SectionName = evt.Sections.First(s => s.Id == seat.EventSectionId).Name,
                        RowLabel = seat.RowLabel,
                        SeatNumber = seat.SeatNumber,
                        TicketCode = NewTicketCode(),
                        TicketType = "General",
                        Price = seat.Price
                    });
                }

                if (!evt.IsSeated)
                {
                    for (var t = 0; t < quantity; t++)
                    {
                        booking.Items.Add(new BookingItem
                        {
                            BookingId = booking.Id,
                            SectionName = "General Admission",
                            TicketCode = NewTicketCode(),
                            TicketType = "General",
                            Price = evt.BasePrice
                        });
                    }
                }

                if (booking.Items.Count == 0) continue;

                booking.Subtotal = booking.Items.Sum(i => i.Price);
                booking.ItemCount = booking.Items.Count;
                booking.ServiceFee = Math.Round(booking.Subtotal * 0.07m, 2, MidpointRounding.AwayFromZero);
                booking.Total = booking.Subtotal + booking.ServiceFee;
                booking.Payment = new Payment
                {
                    BookingId = booking.Id,
                    Provider = "Demo",
                    ProviderReference = $"DEMO-{Guid.NewGuid():N}".ToUpperInvariant(),
                    Amount = booking.Total,
                    Status = PaymentStatus.Completed,
                    RawPayload = "{\"mode\":\"demo\",\"result\":\"approved\"}",
                    CreatedAt = createdAt,
                    CompletedAt = createdAt.AddMinutes(1)
                };

                evt.TicketsSold += booking.ItemCount;
                db.Bookings.Add(booking);
                bookingCount++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Bookings} confirmed bookings", bookingCount);
        _ = admin;
    }

    private static async Task<AppUser> EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string fullName,
        string password,
        string[] roles,
        bool organizer)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsOrganizer = organizer,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not seed user {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static string NewReference() => $"EVT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    private static string NewTicketCode() => $"TKT-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
}
