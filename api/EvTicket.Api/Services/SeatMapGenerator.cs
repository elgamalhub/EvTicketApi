using System.Text.Json;

namespace EvTicket.Api.Services;

/// <summary>
/// Describes how a venue section is physically laid out. Seat coordinates are produced
/// once when an event is created and stored with the event so the floor plan is stable.
/// </summary>
public sealed class SectionLayout
{
    public string Shape { get; set; } = "block"; // block | arc | ring | box
    public int Rows { get; set; } = 5;
    public int SeatsPerRow { get; set; } = 10;
    public double X { get; set; }
    public double Y { get; set; }
    public double RowGap { get; set; } = 30;
    public double SeatGap { get; set; } = 26;
    public double SeatSize { get; set; } = 9;
    public double Curve { get; set; }
    public int AisleEvery { get; set; }
    public double AisleGap { get; set; } = 16;
    public double AngleStart { get; set; } = 30;
    public double AngleEnd { get; set; } = 150;
    public double RadiusStart { get; set; } = 300;
    public double RadiusStep { get; set; } = 28;
    public double CenterX { get; set; } = 500;
    public double CenterY { get; set; } = 100;
    public bool ReverseRows { get; set; }
    public string RowPrefix { get; set; } = "";
    public int NumberStart { get; set; } = 1;
}

public sealed record MapPoint(double X, double Y);

public sealed record GeneratedSeat(string RowLabel, int SeatNumber, double X, double Y, double Rotation);

public sealed record GeneratedSection(IReadOnlyList<GeneratedSeat> Seats, IReadOnlyList<MapPoint> Outline);

public static class SeatMapGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static SectionLayout Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SectionLayout();
        try
        {
            return JsonSerializer.Deserialize<SectionLayout>(json, JsonOptions) ?? new SectionLayout();
        }
        catch (JsonException)
        {
            return new SectionLayout();
        }
    }

    public static string Serialize(SectionLayout layout) => JsonSerializer.Serialize(layout, JsonOptions);

    public static string SerializePoints(IEnumerable<MapPoint> points) => JsonSerializer.Serialize(points, JsonOptions);

    public static GeneratedSection Generate(SectionLayout layout)
    {
        return layout.Shape.ToLowerInvariant() switch
        {
            "arc" => GenerateCurved(layout, fullCircle: false),
            "ring" => GenerateCurved(layout, fullCircle: true),
            _ => GenerateBlock(layout)
        };
    }

    private static GeneratedSection GenerateBlock(SectionLayout l)
    {
        var seats = new List<GeneratedSeat>(l.Rows * l.SeatsPerRow);
        var rowSeatLists = new List<List<(double X, double Y)>>();

        for (var r = 0; r < l.Rows; r++)
        {
            var row = new List<(double X, double Y)>();
            var rowIndex = l.ReverseRows ? l.Rows - 1 - r : r;
            var y = l.Y + rowIndex * l.RowGap;
            var label = RowName(r, l.RowPrefix);

            for (var s = 0; s < l.SeatsPerRow; s++)
            {
                var aisleOffset = l.AisleEvery > 0 ? (s / l.AisleEvery) * l.AisleGap : 0;
                var x = l.X + s * l.SeatGap + aisleOffset;
                if (l.Curve != 0 && l.SeatsPerRow > 1)
                {
                    var t = (s - (l.SeatsPerRow - 1) / 2.0) / ((l.SeatsPerRow - 1) / 2.0);
                    y += l.Curve * t * t;
                }
                row.Add((x, y));
                seats.Add(new GeneratedSeat(label, l.NumberStart + s, Math.Round(x, 2), Math.Round(y, 2), 0));
            }
            rowSeatLists.Add(row);
        }

        var pad = l.SeatSize * 0.95;
        var minX = rowSeatLists.Min(r => r.Min(p => p.X)) - pad;
        var maxX = rowSeatLists.Max(r => r.Max(p => p.X)) + pad;
        var minY = rowSeatLists.Min(r => r.Min(p => p.Y)) - pad;
        var maxY = rowSeatLists.Max(r => r.Max(p => p.Y)) + pad;

        var outline = new List<MapPoint>
        {
            new(Math.Round(minX, 1), Math.Round(minY, 1)),
            new(Math.Round(maxX, 1), Math.Round(minY, 1)),
            new(Math.Round(maxX, 1), Math.Round(maxY, 1)),
            new(Math.Round(minX, 1), Math.Round(maxY, 1))
        };

        return new GeneratedSection(seats, outline);
    }

    private static GeneratedSection GenerateCurved(SectionLayout l, bool fullCircle)
    {
        var seats = new List<GeneratedSeat>(l.Rows * l.SeatsPerRow);
        var rows = new List<List<(double X, double Y)>>();
        var span = l.AngleEnd - l.AngleStart;
        var step = fullCircle
            ? span / Math.Max(1, l.SeatsPerRow)
            : span / Math.Max(1, l.SeatsPerRow - 1);

        for (var r = 0; r < l.Rows; r++)
        {
            var rowIndex = l.ReverseRows ? l.Rows - 1 - r : r;
            var radius = l.RadiusStart + rowIndex * l.RadiusStep;
            var label = RowName(r, l.RowPrefix);
            var row = new List<(double X, double Y)>();

            for (var s = 0; s < l.SeatsPerRow; s++)
            {
                var angle = l.AngleStart + s * step;
                var rad = angle * Math.PI / 180.0;
                var x = l.CenterX + radius * Math.Cos(rad);
                var y = l.CenterY + radius * Math.Sin(rad);
                row.Add((x, y));
                seats.Add(new GeneratedSeat(label, l.NumberStart + s, Math.Round(x, 2), Math.Round(y, 2), Math.Round(angle + 90, 2)));
            }
            rows.Add(row);
        }

        var pad = l.SeatSize * 1.1;
        var inner = rows[0];
        var outer = rows[^1];
        var outline = new List<MapPoint>(inner.Count + outer.Count);

        foreach (var p in inner) outline.Add(Expand(p.X, p.Y, l.CenterX, l.CenterY, -pad));
        for (var i = outer.Count - 1; i >= 0; i--) outline.Add(Expand(outer[i].X, outer[i].Y, l.CenterX, l.CenterY, pad));

        return new GeneratedSection(seats, outline);
    }

    private static MapPoint Expand(double x, double y, double cx, double cy, double delta)
    {
        var dx = x - cx;
        var dy = y - cy;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return new MapPoint(Math.Round(x, 1), Math.Round(y, 1));
        var factor = (len + delta) / len;
        return new MapPoint(Math.Round(cx + dx * factor, 1), Math.Round(cy + dy * factor, 1));
    }

    /// <summary>1 => A, 26 => Z, 27 => AA.</summary>
    public static string RowName(int index, string prefix = "")
    {
        var n = index + 1;
        var letters = string.Empty;
        while (n > 0)
        {
            n--;
            letters = (char)('A' + n % 26) + letters;
            n /= 26;
        }
        return prefix + letters;
    }
}
