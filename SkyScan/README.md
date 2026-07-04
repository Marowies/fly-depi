# SkyScan

SkyScan is a flight search engine — not a booking platform. It lets users search live flight fares (Amadeus-powered), compare routes, and redirect onward to a real OTA/booking site to complete the purchase. It does not process payments or issue tickets itself.

## Roles

- **Guest** — search flights, view results, get redirected to book elsewhere. Bookings/favorites persist via a browser cookie, not an account.
- **User** — everything a Guest can do, plus account management via Google OAuth or email/password, saved favorite routes, and price-drop email alerts on watched routes.

## Architecture

Clean Architecture / DDD, four layers with strict inward-only dependencies:

```
SkyScan.Core            → domain entities, repository interfaces, value objects. Zero framework dependencies.
SkyScan.Application     → DTOs, service interfaces, validators, AutoMapper profiles, FlightFilteringService.
SkyScan.Infrastructure   → EF Core (SQL Server), repository implementations, Amadeus/SMTP/location integrations,
                          ASP.NET Identity, background workers.
SkyScan.Presentation     → ASP.NET Core MVC controllers, Razor views, middleware, DI composition root (Program.cs).
SkyScan.Tests            → unit tests (validators, controllers).
```

**Domain/Identity split:** `Core.Entities.User` is a plain POCO with zero Identity dependency. The real EF/Identity-backed user is `Infrastructure.Identity.ApplicationUser : IdentityUser<Guid>`. They're bridged by extension methods (`ToDomain()`, `ToAuthResult()`) so `IUserRepository` in Core never leaks `IdentityResult`/`SignInResult` — it returns a Core-owned `AuthResult` value object instead.

**Repository pattern:** every entity is accessed through an interface in `Core/Repositories Interfaces`, implemented in `Infrastructure/Data/Repositories_Implementations`, built on a shared `IGenericRepository<T>`/`GenericRepository<T>` base. Controllers depend only on these interfaces — no direct `DbContext` usage in the presentation layer.

## Domain model

| Entity | Purpose |
|---|---|
| `Country`, `City`, `Airport` | Static geographic/reference data. |
| `Airline`, `Airplane` | Static reference data — persisted lazily the first time SkyScan encounters a new carrier/aircraft code from Amadeus, never bulk-loaded. |
| `Flight` | A single flight leg (flight number, times, airports, airline/airplane, redirect URL). Materialized on-demand when a user favorites or "books" a result — not written on every search. |
| `Trip` | Groups one or more `Flight`s a user has taken an interest in (favorited/booked). |
| `Search` | Logs a user's search (route + date) for trending-route analytics. |
| `PriceAlert` | Links a `User` to a `Trip` with a target price; checked periodically by a background worker. |
| `Booking` | A lightweight record of intent-to-book (`UserId`, `FlightId`, timestamp) created just before redirecting the user off-site. **Not a real reservation** — no payment, no PNR, no airline/OTA confirmation. |

## Key features

- **Flight search** — one-way, round-trip (native Amadeus `returnDate`, single API call, both itineraries parsed), and multi-city legs. Results cached per-leg for 15 minutes via `IMemoryCache`.
- **Trending routes** — homepage surfaces popular searches from the last 30 days, with a city-search-count fallback when there isn't enough search history yet.
- **Favorites & price alerts** — favoriting a result materializes the underlying `Flight`/`Trip` rows and creates a `PriceAlert`. A `PriceAlertCheckWorker` background service re-queries the route every 6 hours and emails the user (via SMTP) if the same flight or a cheaper alternative on the route drops below their target price; the alert is deleted once it fires.
- **Guest and authenticated booking-intent tracking** — logged-in users get a `Booking` row per leg; guests get the equivalent stored in an `HttpOnly`/`Secure` cookie. Either way, the user is redirected onward to complete the actual purchase.
- **Currency conversion** — live conversion of displayed prices via CurrencyFreaks.
- **Google OAuth + email/password auth** via ASP.NET Core Identity, with two-factor support.

## External integrations

| Service | Used for | Config keys |
|---|---|---|
| Amadeus Self-Service | Flight Offers Search (v2, with round-trip `returnDate`), Airline Code Lookup | `Amadeus:BaseUrl`, `Amadeus:ClientId`, `Amadeus:ClientSecret` |
| SMTP (Gmail) | Price alert emails, account emails | `Smtp:Host`, `Smtp:Port`, `Smtp:FromEmail`, `Smtp:FromName`, `Smtp:Password` |
| Google OAuth | Social login | `Authentication:Google:ClientId/ClientSecret` |
| CurrencyFreaks | Price conversion | `CurrencyFreaks:ApiKey` |

## Tech stack

.NET 8, ASP.NET Core MVC, Entity Framework Core (SQL Server), ASP.NET Core Identity, AutoMapper, `IMemoryCache`.

## Known state & caveats

- **Amadeus is currently pointed at the test/sandbox environment** (`test.api.amadeus.com`), not production — displayed prices are synthetic, not real quotes. Amadeus Self-Service is also understood to be sunsetting soon; confirm the exact deadline directly with Amadeus before relying on this integration further.
- **Redirect target is currently a generic Google Flights search URL**, not a real affiliate/booking deep link — it can't guarantee the OTA shows the same flight or price found on SkyScan. An affiliate integration with matched search-and-checkout inventory (e.g. Kiwi.com's Tequila API with its `booking_token` deep-link handoff) is under discussion as the more reliable path, precisely because SkyScan doesn't process bookings itself.
- **The production database is live.** Any schema change (migration) requires explicit review and sign-off before being applied — see `SkyScan_Current_Issues_Fix_Plan.md` for the tiering convention used on this project.
- Open architectural findings, prioritized fixes, and forward-looking roadmap items (multi-leg itineraries, CQRS/MediatR, distributed caching, notification engine) are tracked in `SkyScan_Current_Issues_Fix_Plan.md` and `SkyScan_Future_Roadmap_Plan.md` at the repo root.

## Getting started

1. Restore NuGet packages and build (`dotnet build`) targeting .NET 8 (note: `SkyScan.Tests.csproj` currently targets net10.0 — a known mismatch, see the fix plan).
2. Set connection strings and the config keys listed above via user secrets or environment variables — do not commit real credentials to `appsettings.json`.
3. Do not run `dotnet ef migrations add` or `database update` against the live connection string without reviewing the generated migration first — the EF model snapshot has known, deliberate drift pending review (see the fix plan, Tier 0).
