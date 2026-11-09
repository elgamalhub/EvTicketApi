namespace EvTicket.Api.Common;

/// <summary>
/// EF Core's SQLite provider materialises <see cref="DateTime"/> values without
/// <see cref="DateTimeKind"/>, which makes System.Text.Json emit them without a
/// trailing "Z". Clients then read UTC instants as local time. Use these helpers
/// on every UTC timestamp that crosses the API boundary.
/// </summary>
public static class DateTimeExtensions
{
    public static DateTime AsUtc(this DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(this DateTime? value) =>
        value?.AsUtc();
}
