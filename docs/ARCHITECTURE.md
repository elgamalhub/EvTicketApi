# Architecture

EvTicket is a single repository containing an ASP.NET Core API and an Angular SPA. The API owns all business rules; the SPA is a thin, strongly typed client that renders venue geometry and reacts to realtime seat events.

## Runtime topology

```
Browser ──HTTP/JSON──► ASP.NET Core ──EF Core──► SQLite / SQL Server
   │                        │
   └────WebSocket───────────┴──► SignalR SeatHub
```

In development the SPA runs on `:4200` and the API on `:5080`. In production the Angular build is copied into the API's `wwwroot`, so one process serves both and `environment.apiUrl` is empty (same origin).

## Backend layers

| Folder | Responsibility |
| --- | --- |
| `Domain` | EF entities and enums: users, venues, sections, seats, events, bookings, items, tickets, payments, refresh tokens, notifications |
| `Data` | `AppDbContext`, indexes, migrations, `DbSeeder`, `VenueCatalog` |
| `Contracts` | Request/response DTOs with data annotations |
| `Services` | Auth, tokens, events, bookings, tickets, analytics, email, notifications, realtime |
| `Controllers` | HTTP surface, authorization attributes, current-user resolution |
| `Hubs` | `SeatHub` with per-event groups |
| `Workers` | `SeatHoldExpiryWorker` |
| `Middleware` | Uniform `ApiException` → Problem Details mapping |

Controllers stay thin: validate the request, call one service method, return a DTO. Business rules such as ownership checks, pricing and state transitions live in services so they cannot be bypassed by a different controller.

## Data model

```
AppUser 1─* Booking 1─* BookingItem 1─1 Ticket
   │            │
   │            └─1 Payment
   ├─* RefreshToken
   └─* Notification

Venue 1─* VenueSection 1─* Seat
Venue 1─* Event 1─* VenueSection(EventSection) 1─* Seat(EventSeat)
Event 1─* Booking
Category 1─* Event
```

Important indexes:

- `Seats (EventId, Code)` unique — one physical seat code per event.
- `BookingItems (BookingId)` unique and ticket codes unique — a ticket row maps to one sold seat.
- `Bookings (Status, ExpiresAt)` — fast expiry sweep.
- `RefreshTokens (TokenHash)` unique — only hashes are stored.
- `Events (Slug)` unique, `Venues (Slug)` unique, `Users (NormalizedEmail)` unique.

Money is stored as `decimal`; the API rounds to two decimals at the boundaries.

## Seat locking

The no-double-booking guarantee comes from conditional writes.

**Hold**

```
UPDATE Seats
SET Status = Held, HeldUntil = now + 10m, HeldByBookingId = bookingId
WHERE Id IN (@ids) AND Status = Available
```

EF Core's `ExecuteUpdateAsync` returns the affected row count. If it is lower than the requested count, the transaction is rolled back and the API returns `409 seats_taken`. The database serialises the conflicting row, so exactly one request wins.

**Checkout**

1. Verify the booking is still `Pending` and not expired.
2. Validate the payment (demo gateway).
3. Move seats from `Held` to `Sold` with an update constrained on `HeldByBookingId` and `Status = Held`.
4. Confirm the booking, create one ticket per item, create a payment record and a notification.
5. Broadcast the seat changes.

If step 3 affects fewer rows than expected, checkout fails with `409 hold_expired` and no money is captured.

**Expiry**

`SeatHoldExpiryWorker` runs on a timer, finds pending bookings past `ExpiresAt`, marks them `Expired`, releases their seats to `Available`, and broadcasts the changes.

**General admission**

GA events do not allocate seats. Hold and checkout use the same conditional update against remaining capacity, so the oversell guarantee is identical.

## Authentication

- **Register/login** → Identity verifies the password, then `TokenService` issues a signed JWT (60 min) and a random refresh token (14 days).
- Refresh tokens are stored only as SHA-256 hashes. On refresh, the old token is revoked and a new one is issued (rotation). Reuse of a revoked token revokes the whole chain.
- **Logout** revokes the presented refresh token.
- Role claims drive `[Authorize(Roles = "Admin")]` and `Organizer` policies. "Organizer" is both a role and an `IsOrganizer` flag so admins can also run events.
- The SignalR hub accepts the access token from the query string only for `/hubs` paths.
- Password reset uses Identity tokens; when SMTP is disabled the link is logged so the flow remains testable.

## Realtime

`SeatHub` groups connections by event ID. Services call `IRealtimeNotifier` after commits:

- `SeatChanged` — one or more seats moved between available/held/sold.
- `BookingConfirmed` — a checkout completed.
- `Notification` — a user-facing notification was created.

The Angular `RealtimeService` connects with the access token, reconnects with backoff, and exposes a signal for connection state. Seat-map pages merge incoming events into their local map without refetching.

## Frontend architecture

- **Zoneless + signals.** State lives in services and page-level signals; computed values derive totals, filtered lists and chart geometry.
- **Standalone components**, lazy-loaded per route. Functional `CanActivate` guards enforce auth and roles; a functional interceptor attaches the JWT and refreshes once on 401.
- **Design system** in `styles.scss`: tokens, typography, buttons, fields, badges, cards, tables, skeletons and motion. Page styles stay local.
- **SVG seat maps** are generated from venue geometry returned by the API. The client only renders; it never decides availability.
- **Custom charts** avoid a chart dependency: a reusable area chart computes its own path, grid and hover points.
- **QR codes** are rendered client-side from ticket codes, so tickets work offline once loaded.
- **Toasts** provide a single feedback channel for success, info and API errors.

## Error contract

All handled failures use Problem Details with a stable `code` extension:

| Status | `code` examples | Meaning |
| --- | --- | --- |
| 400 | `validation_failed` | Invalid input |
| 401 | `unauthorized` | Missing/expired token |
| 403 | `forbidden` | Authenticated but not allowed |
| 404 | `not_found` | Missing resource |
| 409 | `seats_taken`, `hold_expired`, `sold_out` | State conflict; client should refresh |
| 429 | — | Rate limit exceeded |
| 500 | `server_error` | Unexpected failure |

The Angular `apiError()` helper turns these into human-readable messages.

## Operational concerns

- **Health:** `GET /health` checks the database via EF Core.
- **Rate limiting:** fixed-window global limiter (400/min/IP) plus a stricter `auth` policy (12/min/IP).
- **Compression:** enabled for HTTPS responses.
- **Forwarded headers:** configured for reverse proxies.
- **Validation:** data annotations plus service-level checks; invalid models return Problem Details automatically.
- **Seeding:** idempotent; only runs when enabled and the database has no users.
- **Migrations:** applied automatically at startup; a failed migration aborts startup loudly.

## Deliberate trade-offs

| Decision | Why |
| --- | --- |
| SQLite by default | Zero-setup demo, deterministic tests, easy Docker image; SQL Server provider kept for production |
| Server-side seat geometry | Venue layouts are data, not client logic, so new venues do not require SPA changes |
| Conditional updates over distributed locks | Works with SQLite and SQL Server without extra infrastructure |
| Query-string token for SignalR | WebSockets cannot set an Authorization header in browsers; path is restricted to the hub |
| Demo payment gateway | Keeps the project runnable without third-party accounts while preserving the full state machine |
| Access token in memory/localStorage | Simple SPA demo; production guidance is HttpOnly refresh cookies |

## Extending

- **New venue:** add a layout to `VenueCatalog`, seed sections and seats, and the existing seat-map UI renders it.
- **New category:** add it to the seeder or the admin/catalog data; event forms read categories dynamically.
- **Stripe:** implement an `IPaymentGateway`, switch `Payments:Mode`, and turn checkout into a webhook-confirmed transition.
- **Scale-out:** replace the in-memory SignalR backplane with Redis and move hold expiry to a scheduled job.
