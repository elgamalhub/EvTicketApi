# Five-minute demo script

Use this path to see every major feature quickly. The API and web app should both be running (see the root README).

## 1. Guest booking (2 minutes)

1. Open http://localhost:4200 and pick a seated event (for example **Midnight Symphony** or any event at Grand Majestic).
2. On the event page, open the seat map. Zoom and pan; hover a seat to see its section and price.
3. Select two seats and press **Continue**.
4. On checkout, watch the 10-minute hold countdown. Pay with `4242 4242 4242 4242`, `12/30`, `123`.
5. The booking detail page shows the confirmed QR tickets. Try the **demo wallet** option on another booking to see the fast path.

To see a decline, use a card ending in `0002`.

## 2. Concurrency proof (1 minute)

1. Open the same seated event in two normal and one private window.
2. Sign in as `demo@evticket.dev` in one and `organizer@evticket.dev` in another.
3. Select the same seat in both maps and continue in both.
4. One tab opens checkout. The other gets *"Those seats were just taken"* and the map refreshes. The seat is `Held` in the live map of every open window.

## 3. Organizer (1 minute)

1. Sign in as `organizer@evticket.dev`.
2. Open **Organizer studio** — revenue, sell-through, top performer, recent bookings.
3. **New event** — fill the title, pick a category and venue, set a future date, publish.
4. Open **Check-in desk**, choose an event, and paste a ticket code from a confirmed booking. The first scan succeeds; the second reports it is already checked in.
5. Export the attendee CSV.

## 4. Admin (1 minute)

1. Sign in as `admin@evticket.dev`.
2. The admin console opens on overview: revenue chart, category mix, top events, recent signups.
3. Open **Users**, search for `demo`, toggle organizer access, then open **Bookings** and filter by `confirmed`.
4. Export the current bookings page as CSV.

## 5. Recovery flow (30 seconds)

1. Sign out, open **Forgot password**, and request a link for `demo@evticket.dev`.
2. Copy the reset link from the API console log.
3. Set a new password and sign in with it.

> Reset the demo at any time by deleting `api/EvTicket.Api/evticket.db` (and `-shm`/`-wal`) and restarting the API. The seeder recreates everything.
