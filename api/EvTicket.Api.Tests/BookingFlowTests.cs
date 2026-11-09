using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EvTicket.Api.Contracts;

namespace EvTicket.Api.Tests;

public sealed class BookingFlowTests(EvTicketApiFactory factory) : IClassFixture<EvTicketApiFactory>
{
    private const string DemoPassword = "Demo@12345";
    private const string SeatedEventSlug = "aurora-nights-synthwave-live";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Concurrent_holds_on_the_same_seat_produce_one_winner_and_one_conflict()
    {
        using var alice = await LoginAsync("demo@evticket.dev");
        using var bob = await LoginAsync("james.okafor@example.com");

        var seat = await FindAvailableSeatAsync(alice);
        var request = new HoldSeatsRequest
        {
            EventId = seat.EventId,
            SeatIds = [seat.SeatId],
            Quantity = 1
        };

        var responses = await Task.WhenAll(
            alice.PostAsJsonAsync("/api/bookings/hold", request, Json),
            bob.PostAsJsonAsync("/api/bookings/hold", request, Json));

        var winnerIndex = Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.OK);
        var loserIndex = Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.True(winnerIndex >= 0, $"Expected one hold to win, got {Describe(responses)}.");
        Assert.True(loserIndex >= 0, $"Expected one hold to conflict, got {Describe(responses)}.");

        var winner = responses[winnerIndex];
        var loser = responses[loserIndex];

        using (var problem = JsonDocument.Parse(await loser.Content.ReadAsStringAsync()))
        {
            Assert.Equal("seats_taken", problem.RootElement.GetProperty("code").GetString());
        }

        var held = await winner.Content.ReadFromJsonAsync<HeldSeatsResponse>(Json);
        Assert.NotNull(held);
        Assert.True(held!.ExpiresAt > DateTime.UtcNow);

        // Release the winning hold so the remaining tests see a clean seat map.
        var winnerClient = winnerIndex == 0 ? alice : bob;
        var release = await winnerClient.PostAsync($"/api/bookings/{held.BookingId}/release", null);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
    }

    [Fact]
    public async Task Demo_wallet_checkout_confirms_booking_and_issues_a_ticket()
    {
        using var client = await LoginAsync("demo@evticket.dev");

        var seat = await FindAvailableSeatAsync(client);
        var holdResponse = await client.PostAsJsonAsync("/api/bookings/hold", new HoldSeatsRequest
        {
            EventId = seat.EventId,
            SeatIds = [seat.SeatId],
            Quantity = 1
        }, Json);

        Assert.Equal(HttpStatusCode.OK, holdResponse.StatusCode);
        var hold = await holdResponse.Content.ReadFromJsonAsync<HeldSeatsResponse>(Json);
        Assert.NotNull(hold);

        var checkout = await client.PostAsJsonAsync(
            $"/api/bookings/{hold!.BookingId}/checkout",
            new CheckoutRequest { PaymentMethod = "demo" },
            Json);

        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var booking = await checkout.Content.ReadFromJsonAsync<BookingDto>(Json);
        Assert.NotNull(booking);
        Assert.Equal("confirmed", booking!.Status);
        Assert.Equal("completed", booking.PaymentStatus);
        Assert.NotNull(booking.ConfirmedAt);
        Assert.Null(booking.ExpiresAt);
        Assert.True(booking.Total > 0);
        Assert.Equal(booking.Subtotal + booking.ServiceFee, booking.Total);
        Assert.Single(booking.Tickets);

        var ticket = booking.Tickets[0];
        Assert.StartsWith("TKT-", ticket.Code);
        Assert.False(string.IsNullOrWhiteSpace(ticket.SectionName));
        Assert.False(ticket.IsCheckedIn);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = DemoPassword
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<(Guid EventId, Guid SeatId)> FindAvailableSeatAsync(HttpClient client)
    {
        var map = await client.GetFromJsonAsync<SeatMapDto>($"/api/events/{SeatedEventSlug}/seat-map", Json);
        Assert.NotNull(map);

        var seat = map!.Sections
            .SelectMany(section => section.Seats)
            .FirstOrDefault(s => string.Equals(s.Status, "available", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(seat);
        return (map.EventId, seat!.Id);
    }

    private static string Describe(IEnumerable<HttpResponseMessage> responses) =>
        string.Join(", ", responses.Select(r => (int)r.StatusCode));
}
