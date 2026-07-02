You are working on **SkyScan**, a .NET 8 flight search app (ASP.NET MVC + Amadeus API) built as a Clean Architecture / DDD solution: `SkyScan.Core` (domain entities, repository interfaces), `SkyScan.Application` (DTOs, interfaces, validators, mappings), `SkyScan.Infrastructure` (EF Core, repository implementations, external services, ASP.NET Identity), `SkyScan.Presentation` (MVC controllers/views), `SkyScan.Tests`.

**Important constraint: the database is live.** Do not write or apply EF Core migrations without explicit confirmation, and do not assume schema changes are safe. If a fix seems to require a schema change, prefer a design that avoids one, or stop and ask.

## What was just completed (previous session)

- **Round-trip search**: `FlightController.Search`/`Results` now search both legs and render Outbound/Return sections.
- **Price alerts**: fully working — `ToggleFavorite` materializes a `Flight`/`Trip` on demand (since providers no longer persist flight rows per search), and a `PriceAlertCheckWorker` background service checks alerts every 6 hours and emails via `IEmailService`, deleting the alert after it fires (one-shot, no extra schema needed).
- **Amadeus integration**: search only persists static reference data (Airline, Airplane lookups) — it no longer writes a `Flight`/`Ticket` row on every search result.
- **DI / Clean Architecture enforcement**:
  - `SkyScan.Core.Entities.User` is now a plain POCO with zero Identity dependency. The real EF/Identity-backed type is `SkyScan.Infrastructure.Identity.ApplicationUser : IdentityUser<Guid>`.
  - `IUserRepository` (Core) returns a new `AuthResult` value object instead of `IdentityResult`/`SignInResult` — Core has no reference to `Microsoft.AspNetCore.Identity` anywhere.
  - `Search`, `Booking`, `PriceAlert` dropped their `User` nav property (kept `UserId` only); relationships to `ApplicationUser` are configured FK-only in Infrastructure (`SearchConfiguration`, `PriceAlertConfiguration`, and a new `BookingConfiguration`).
  - **No schema change was needed for any of this** — table/column shapes are driven by property names and Fluent config, not CLR class names.
  - Added `IBookingRepository`; `BookingController` no longer touches `SkyScanDbContext` directly.
  - `FlightController`'s trending-routes and search-logging queries now go through `ISearchRepository`/`IAirportRepository` instead of raw context queries.
  - `AmadeusFlightService` takes `SkyScanDbContext` via constructor instead of service-locating a scope.
  - `FlightFilteringService` moved from Infrastructure to `SkyScan.Application.Services` (it's pure DTO logic with no external dependencies).

## Immediate next step — do this first

**Build the solution and smoke-test it.** The previous session had no shell access to compile or run anything — every change was verified by careful reading and grep, not by an actual build. Before any new feature work:
1. `dotnet build` the whole solution and fix any compile errors.
2. Manually test: register, confirm email, login, 2FA setup/verify, Google external login, change password, profile page, flight search (one-way and round-trip), favoriting a flight, viewing/removing price alerts, booking (logged-in and guest).
3. If you have a non-production DB copy, run against that first.

## After that, in priority order

1. **Housekeeping** (low risk, ~1 hour): wire `FlightSearchRequestValidator` (FluentValidation) into the actual request pipeline — it's written but currently never invoked. Add explicit `options.Cookie.SameSite` / `options.Cookie.SecurePolicy` to `ConfigureApplicationCookie` in `Program.cs`. Delete `SkyScan\db_test.cs` (stray debug script with a hardcoded local path) and the unreferenced `AviationStackFlightService`. Pin `SkyScan.Tests.csproj` to `net8.0` to match the rest of the solution.
2. **Multi-leg trips** (the project's own roadmap flags this as the "current bottleneck"): `TripType.MultiWay` and `FlightSearchRequestDto.Legs` exist, and the search form collects multiple legs, but `FlightController.Results` and every `IFlightProviderService` implementation still only handle a single origin/destination pair. Extend the same `SearchLegAsync` pattern used for round-trip to loop over all legs and render a composite itinerary.
3. **CQRS/MediatR**: the project's own instructions call for CQRS with pipeline behaviors (validation, logging, caching), but the codebase is currently a repository/service pattern throughout. Rather than a big-bang retrofit of existing controllers, introduce MediatR alongside the multi-leg work above — e.g. a `SearchMultiLegQuery`/handler as the first vertical slice — so new code lands in the target shape without a separate risky migration of working code.
4. **Caching abstraction**: introduce an `ICacheService` (Application) wrapping `IDistributedCache`, and route the existing `IMemoryCache` call sites (`FlightController`'s airport-dropdown/flight-results cache, `CurrencyConversionService`'s exchange-rate cache) through it. This makes a future Redis swap a one-line DI change instead of touching every call site.

## Known, deliberately-accepted gaps (not urgent)

- `FlightController.ToggleFavorite` still queries `SkyScanDbContext.Trips` directly — there's no `ITripRepository`. Left as-is since Trip has no dedicated repository yet.
- `AccountController` still calls `UserManager<ApplicationUser>`/`SignInManager<ApplicationUser>` directly for 2FA setup, external login, and change-password flows rather than exclusively through `IUserRepository`. This mirrors how the code already worked and is a reasonable, common ASP.NET Identity pattern — not a regression.
