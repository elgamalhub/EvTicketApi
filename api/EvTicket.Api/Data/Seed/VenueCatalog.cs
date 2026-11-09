using EvTicket.Api.Domain;
using EvTicket.Api.Services;

namespace EvTicket.Api.Data.Seed;

public sealed record VenueSeed(
    Venue Venue,
    IReadOnlyList<VenueSection> Sections);

/// <summary>Hand-tuned venue floor plans that ship with the demo.</summary>
public static class VenueCatalog
{
    public static IReadOnlyList<VenueSeed> Build()
    {
        return new List<VenueSeed>
        {
            BuildTheater(),
            BuildArena(),
            BuildAmphitheater(),
            BuildCinema(),
            BuildClub()
        };
    }

    private static VenueSection Section(
        string code, string name, SectionKind kind, string color, decimal mult, int order,
        SectionLayout layout, int capacity, string layoutJson = "")
    {
        return new VenueSection
        {
            Code = code,
            Name = name,
            Kind = kind,
            Color = color,
            PriceMultiplier = mult,
            SortOrder = order,
            Capacity = capacity,
            LayoutJson = string.IsNullOrEmpty(layoutJson) ? SeatMapGenerator.Serialize(layout) : layoutJson
        };
    }

    private static VenueSeed BuildTheater()
    {
        var venue = new Venue
        {
            Name = "The Grand Majestic Theater",
            Slug = "grand-majestic-theater",
            Description = "A restored 1920s playhouse with hand-painted ceilings, velvet seating and legendary acoustics in the heart of the arts district.",
            Address = "218 West 44th Street",
            City = "New York",
            Country = "United States",
            Capacity = 560,
            LayoutKind = LayoutKind.Theater,
            ImageUrl = "https://images.unsplash.com/photo-1503095396549-807759245b35?auto=format&fit=crop&w=1600&q=80",
            Latitude = 40.7580,
            Longitude = -73.9855,
            MapWidth = 1000,
            MapHeight = 780,
            StageX = 330,
            StageY = 30,
            StageWidth = 340,
            StageHeight = 80,
            StageLabel = "STAGE"
        };

        var sections = new List<VenueSection>
        {
            Section("ORCH", "Orchestra", SectionKind.Floor, "#6366f1", 1.00m, 1,
                new SectionLayout { Shape = "block", X = 250, Y = 190, Rows = 9, SeatsPerRow = 20, RowGap = 30, SeatGap = 26, AisleEvery = 10, AisleGap = 22 }, 180),
            Section("ORCH-L", "Orchestra Left", SectionKind.Floor, "#8b5cf6", 0.85m, 2,
                new SectionLayout { Shape = "block", X = 60, Y = 190, Rows = 9, SeatsPerRow = 8, RowGap = 30, SeatGap = 26 }, 72),
            Section("ORCH-R", "Orchestra Right", SectionKind.Floor, "#8b5cf6", 0.85m, 3,
                new SectionLayout { Shape = "block", X = 766, Y = 190, Rows = 9, SeatsPerRow = 8, RowGap = 30, SeatGap = 26 }, 72),
            Section("MEZZ", "Mezzanine", SectionKind.Tier, "#06b6d4", 0.90m, 4,
                new SectionLayout { Shape = "arc", CenterX = 500, CenterY = 20, RadiusStart = 520, RadiusStep = 30, Rows = 5, SeatsPerRow = 44, AngleStart = 55, AngleEnd = 125, SeatSize = 8 }, 220),
            Section("BOX-L", "Grand Box Left", SectionKind.Box, "#f59e0b", 1.60m, 5,
                new SectionLayout { Shape = "box", X = 40, Y = 70, Rows = 2, SeatsPerRow = 4, RowGap = 26, SeatGap = 26, SeatSize = 10 }, 8),
            Section("BOX-R", "Grand Box Right", SectionKind.Box, "#f59e0b", 1.60m, 6,
                new SectionLayout { Shape = "box", X = 856, Y = 70, Rows = 2, SeatsPerRow = 4, RowGap = 26, SeatGap = 26, SeatSize = 10 }, 8)
        };

        return new VenueSeed(venue, sections);
    }

    private static VenueSeed BuildArena()
    {
        var venue = new Venue
        {
            Name = "Skyline Arena",
            Slug = "skyline-arena",
            Description = "A 360° entertainment bowl built for stadium tours, championship finals and arena-scale productions.",
            Address = "1 Skyline Boulevard",
            City = "Los Angeles",
            Country = "United States",
            Capacity = 428,
            LayoutKind = LayoutKind.Arena,
            ImageUrl = "https://images.unsplash.com/photo-1546519638-68e109498ffc?auto=format&fit=crop&w=1600&q=80",
            Latitude = 34.0430,
            Longitude = -118.2673,
            MapWidth = 1000,
            MapHeight = 840,
            StageX = 360,
            StageY = 330,
            StageWidth = 280,
            StageHeight = 160,
            StageLabel = "COURT"
        };

        var sections = new List<VenueSection>
        {
            Section("FLR-E", "Floor East", SectionKind.Floor, "#f59e0b", 1.60m, 1,
                new SectionLayout { Shape = "block", X = 190, Y = 350, Rows = 5, SeatsPerRow = 6, RowGap = 26, SeatGap = 26 }, 30),
            Section("FLR-W", "Floor West", SectionKind.Floor, "#f59e0b", 1.60m, 2,
                new SectionLayout { Shape = "block", X = 680, Y = 350, Rows = 5, SeatsPerRow = 6, RowGap = 26, SeatGap = 26 }, 30),
            Section("LOWER", "Lower Bowl", SectionKind.Tier, "#6366f1", 1.00m, 3,
                new SectionLayout { Shape = "ring", CenterX = 500, CenterY = 410, RadiusStart = 250, RadiusStep = 28, Rows = 5, SeatsPerRow = 40, AngleStart = 0, AngleEnd = 360, SeatSize = 8 }, 200),
            Section("UPPER", "Upper Bowl", SectionKind.Balcony, "#06b6d4", 0.75m, 4,
                new SectionLayout { Shape = "ring", CenterX = 500, CenterY = 410, RadiusStart = 360, RadiusStep = 22, Rows = 3, SeatsPerRow = 56, AngleStart = 0, AngleEnd = 360, SeatSize = 7 }, 168)
        };

        return new VenueSeed(venue, sections);
    }

    private static VenueSeed BuildAmphitheater()
    {
        var venue = new Venue
        {
            Name = "Riverside Amphitheater",
            Slug = "riverside-amphitheater",
            Description = "An open-air crescent carved into the riverbank — sunset shows, symphony nights and festival weekends.",
            Address = "400 River Walk",
            City = "Austin",
            Country = "United States",
            Capacity = 532,
            LayoutKind = LayoutKind.Amphitheater,
            ImageUrl = "https://images.unsplash.com/photo-1470229722913-7c0e2dbbafd3?auto=format&fit=crop&w=1600&q=80",
            Latitude = 30.2672,
            Longitude = -97.7431,
            MapWidth = 1000,
            MapHeight = 700,
            StageX = 350,
            StageY = 20,
            StageWidth = 300,
            StageHeight = 70,
            StageLabel = "STAGE"
        };

        var sections = new List<VenueSection>
        {
            Section("CENTER", "Center Fan", SectionKind.Tier, "#6366f1", 1.00m, 1,
                new SectionLayout { Shape = "arc", CenterX = 500, CenterY = 40, RadiusStart = 170, RadiusStep = 28, Rows = 8, SeatsPerRow = 30, AngleStart = 50, AngleEnd = 130, SeatSize = 8 }, 240),
            Section("SIDE-L", "West Terrace", SectionKind.Tier, "#8b5cf6", 0.80m, 2,
                new SectionLayout { Shape = "arc", CenterX = 500, CenterY = 40, RadiusStart = 170, RadiusStep = 28, Rows = 8, SeatsPerRow = 10, AngleStart = 133, AngleEnd = 165, SeatSize = 8 }, 80),
            Section("SIDE-R", "East Terrace", SectionKind.Tier, "#8b5cf6", 0.80m, 3,
                new SectionLayout { Shape = "arc", CenterX = 500, CenterY = 40, RadiusStart = 170, RadiusStep = 28, Rows = 8, SeatsPerRow = 10, AngleStart = 15, AngleEnd = 47, SeatSize = 8 }, 80),
            Section("LAWN", "River Lawn", SectionKind.Lawn, "#22c55e", 0.55m, 4,
                new SectionLayout { Shape = "arc", CenterX = 500, CenterY = 40, RadiusStart = 470, RadiusStep = 30, Rows = 3, SeatsPerRow = 44, AngleStart = 45, AngleEnd = 135, SeatSize = 7 }, 132)
        };

        return new VenueSeed(venue, sections);
    }

    private static VenueSeed BuildCinema()
    {
        var venue = new Venue
        {
            Name = "Nova Cinema & Dolby Lab",
            Slug = "nova-cinema",
            Description = "A boutique cinema with laser projection, Dolby Atmos and recliners you will never want to leave.",
            Address = "77 Market Lane",
            City = "Seattle",
            Country = "United States",
            Capacity = 306,
            LayoutKind = LayoutKind.Cinema,
            ImageUrl = "https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?auto=format&fit=crop&w=1600&q=80",
            Latitude = 47.6062,
            Longitude = -122.3321,
            MapWidth = 900,
            MapHeight = 620,
            StageX = 250,
            StageY = 25,
            StageWidth = 400,
            StageHeight = 35,
            StageLabel = "SCREEN"
        };

        var sections = new List<VenueSection>
        {
            Section("PREM", "Premiere Row", SectionKind.Box, "#f59e0b", 1.80m, 1,
                new SectionLayout { Shape = "block", X = 310, Y = 100, Rows = 1, SeatsPerRow = 10, RowGap = 24, SeatGap = 30, SeatSize = 10 }, 10),
            Section("STALL", "Stalls", SectionKind.Floor, "#6366f1", 1.00m, 2,
                new SectionLayout { Shape = "block", X = 200, Y = 150, Rows = 8, SeatsPerRow = 16, RowGap = 32, SeatGap = 30, AisleEvery = 8, AisleGap = 24 }, 128),
            Section("REAR", "Rear Recliners", SectionKind.Tier, "#06b6d4", 0.85m, 3,
                new SectionLayout { Shape = "block", X = 170, Y = 410, Rows = 6, SeatsPerRow = 20, RowGap = 30, SeatGap = 30, AisleEvery = 10, AisleGap = 24 }, 120),
            Section("SIDE-L", "Side Left", SectionKind.Tier, "#8b5cf6", 0.70m, 4,
                new SectionLayout { Shape = "block", X = 60, Y = 170, Rows = 6, SeatsPerRow = 4, RowGap = 34, SeatGap = 26 }, 24),
            Section("SIDE-R", "Side Right", SectionKind.Tier, "#8b5cf6", 0.70m, 5,
                new SectionLayout { Shape = "block", X = 762, Y = 170, Rows = 6, SeatsPerRow = 4, RowGap = 34, SeatGap = 26 }, 24)
        };

        return new VenueSeed(venue, sections);
    }

    private static VenueSeed BuildClub()
    {
        var venue = new Venue
        {
            Name = "The Velvet Loft",
            Slug = "velvet-loft",
            Description = "An intimate 220-cap loft with a wraparound mezzanine, craft bar and the best sightlines in the city.",
            Address = "12 Warehouse Row",
            City = "Chicago",
            Country = "United States",
            Capacity = 220,
            LayoutKind = LayoutKind.Club,
            ImageUrl = "https://images.unsplash.com/photo-1501386761578-eac5c94b800a?auto=format&fit=crop&w=1600&q=80",
            Latitude = 41.8781,
            Longitude = -87.6298,
            MapWidth = 900,
            MapHeight = 600,
            StageX = 250,
            StageY = 30,
            StageWidth = 400,
            StageHeight = 60,
            StageLabel = "STAGE"
        };

        var sections = new List<VenueSection>
        {
            Section("FLOOR", "Main Floor", SectionKind.Floor, "#6366f1", 1.00m, 1,
                new SectionLayout { Shape = "block", X = 210, Y = 170, Rows = 7, SeatsPerRow = 14, RowGap = 34, SeatGap = 30, AisleEvery = 7, AisleGap = 20 }, 98),
            Section("VIP", "VIP Tables", SectionKind.Box, "#f59e0b", 2.00m, 2,
                new SectionLayout { Shape = "block", X = 620, Y = 190, Rows = 4, SeatsPerRow = 5, RowGap = 34, SeatGap = 34, SeatSize = 10 }, 20),
            Section("MEZZ", "Mezzanine", SectionKind.Balcony, "#06b6d4", 0.90m, 3,
                new SectionLayout { Shape = "arc", CenterX = 450, CenterY = 20, RadiusStart = 480, RadiusStep = 26, Rows = 3, SeatsPerRow = 34, AngleStart = 55, AngleEnd = 125, SeatSize = 8 }, 102)
        };

        return new VenueSeed(venue, sections);
    }
}
