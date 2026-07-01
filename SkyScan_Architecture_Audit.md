# SkyScan Architecture Audit

Scope: SkyScan.Core, SkyScan.Application, SkyScan.Infrastructure, SkyScan.Presentation, SkyScan.Tests. Reviewed against the Clean Architecture / DDD / CQRS mandate, SRP/DRY density rules, and the four roadmap items (Redis caching, cookie/session hardening, multi-leg itineraries, price-alert notifications).

**Bottom line:** the layer *boundaries exist as folders* but are not enforced. The Presentation layer routinely reaches straight into EF Core and bypasses every repository interface that Infrastructure already provides. There is no CQRS anywhere — no MediatR, no commands/queries/handlers, no pipeline behaviors. Two roadmap items (multi-leg, price alerts) are stubbed but not wired end-to-end, and one (Redis) has no abstraction to build on at all.

---

## Critical — layering violations

**`FlightController` injects `SkyScanDbContext` directly** (Presentation → Infrastructure/EF, skipping Application and the repository interfaces entirely). `Index`, `Results`, and `ToggleFavorite` run raw LINQ-to-EF queries against `Searches`, `Cities`, `Trips`, and `PriceAlerts` inline in the controller — the exact logic `ISearchRepository` and `IPriceAlertRepository` already exist to encapsulate. `AccountController` and `BookingController` do the same (`SkyScanDbContext` injected alongside `UserManager`/`SignInManager`, used directly in `Profile()` and `Book()`/`MyBookings()`).
**Why it matters:** this is the one rule the project instructions state most explicitly ("Presentation... No business or data access logic allowed here"). As written, swapping the data store, adding caching, or unit-testing these code paths without a live DbContext is not possible.
**Fix:** every raw context query in these three controllers moves behind an existing or new repository method (`ISearchRepository.LogSearchAsync`, a new `IBookingRepository`, `IPriceAlertRepository.ToggleAsync`, etc.). Controllers should only ever see `SkyScan.Application` interfaces.

**No `IBookingRepository` exists.** `Booking` is a first-class entity with its own DbSet and EF configuration, but `BookingController` is the only thing that ever queries or writes it, straight through `SkyScanDbContext`. It's the most glaring instance of the pattern above because there isn't even an abstraction to bypass — one was never built.

**`AmadeusFlightService` (Infrastructure) both searches *and* writes.** On every search call it resolves `SkyScanDbContext` via `IServiceProvider` (service-locator anti-pattern — it should take a repository or `DbContext` through constructor injection like everything else) and does find-or-create writes for `Airline`, `Airplane`, `Flight`, and `Ticket` rows as a side effect of what the interface (`IFlightProviderService.SearchFlightsAsync`) advertises as a read. This conflates query and command responsibility inside a single method — the opposite of CQRS — and means a "search" silently mutates shared state other requests will read.
**Fix:** split into `IFlightProviderService.SearchFlightsAsync` (pure Amadeus → DTO, no DB) and a separate `SyncFlightCatalogCommand`/handler that persists what came back. This is also where the N+1 problem below gets fixed for free.

**`LocationSearchService` also service-locates `SkyScanDbContext`** instead of depending on `IAirportRepository`/`ISearchRepository` abstractions it should be using. Same anti-pattern, different file.

**No CQRS pattern anywhere.** No MediatR package reference, no `Commands`/`Queries`/`Handlers` folders, no pipeline behaviors for validation/logging/caching. `FlightSearchRequestValidator` (FluentValidation) is fully written but never registered in `Program.cs` and never invoked by any controller — dead code. This is a structural gap against the explicit CQRS requirement, not a style nitpick; retrofitting it later means touching every controller action again.

## High — domain purity

**`User : IdentityUser<Guid>`** in `SkyScan.Core.Entities` pulls `Microsoft.AspNetCore.Identity` directly into the domain layer. **`IUserRepository`** (also in Core) returns `IdentityResult` and `SignInResult` from Identity. Both are direct framework leaks into the layer defined as having "absolutely zero external dependencies or framework leaks."
**Fix:** keep `User` as a plain Core entity (`Id`, `Name`, `Email`, ...) and let an Infrastructure-side `ApplicationUser : IdentityUser<Guid>` wrap/map it, or accept the Identity coupling explicitly as a documented, deliberate exception — but as it stands it's silently violating the stated rule. `IUserRepository` should return domain-shaped results (e.g. an `AuthResult` value object), not `IdentityResult`/`SignInResult`.

**`FlightFilteringService` lives in `SkyScan.Infrastructure`** but has zero external dependencies — it's pure LINQ over DTOs. It's an Application-layer (or Core) concern misfiled into Infrastructure, which will confuse the next person deciding where filtering logic belongs.

## Medium — bugs and dead code found during the read

- `GenericRepository<T>.GetByIdAsync(int id)` — every entity in this codebase keys on `Guid`, so this method can never return a match. It compiles because `DbSet.FindAsync(object)` boxes the `int`, but it's a landmine for whoever calls it first.
- `db_test.cs` sits at the solution root outside any `.csproj`, hardcodes `d:\depi gp\fly-depi\...` as a base path, and declares its own `class Program { static void Main() }`. It's a debugging scratch file that should be deleted, not shipped in the tree.
- `SkyScan.Infrastructure.csproj` explicitly excludes a stale `Data\Repositories Implementations\` (with a space) folder via `<Compile Remove>` — meaning an old, abandoned copy of the repository implementations still exists on disk next to the real `Repositories_Implementations` folder. Dead weight; delete the excluded folder instead of excluding it.
- `SkyScan.Tests.csproj` targets `net10.0` while every other project targets `net8.0`. Works today because test projects don't need to match, but it's an easy source of confusion and should be pinned to `net8.0` to match the app.
- `BookingController.cs` has a duplicated `using` block (lines 1–19 repeat lines 9 onward) and stray leading indentation on the namespace — cosmetic, but a sign this file was pasted together rather than authored cleanly.
- `AviationStackFlightService` and `AmadeusFlightService` both exist as `IFlightProviderService` implementations alongside `MockFlightProviderService`, but `Program.cs` only ever wires up Mock or Amadeus — `AviationStackFlightService` is unreferenced dead code.

## Roadmap items — current state

**Redis / caching abstraction:** not started. Every cache today is `IMemoryCache` (`FlightController`'s airport-dropdown and flight-results cache, `CurrencyConversionService`'s exchange-rate cache) or a hand-rolled in-process singleton (`LocationSearchService`'s `_searchIndex`). None of this sits behind `IDistributedCache` or any app-owned caching interface, so swapping in Redis later means touching every call site, not just a DI registration. **Recommendation:** introduce an `ICacheService` in Application (wrapping `IDistributedCache` with JSON serialization) now, route the three cache users above through it, and Redis becomes a one-line `AddStackExchangeRedisCache` swap later.

**Cookie/session hardening:** partially done. `ConfigureApplicationCookie` sets paths and sliding expiration but never sets `Cookie.SameSite` or explicit `Cookie.SecurePolicy` — it's relying on ASP.NET Identity defaults rather than the explicit policy the instructions call for. The hand-rolled guest-booking cookie in `BookingController` sets `HttpOnly`/`Secure` but also omits `SameSite`. **Fix:** set `options.Cookie.SameSite = SameSiteMode.Lax` (or `Strict`) and `options.Cookie.SecurePolicy = CookieSecurePolicy.Always` explicitly in both places.

**Multi-leg / transit itineraries — "current bottleneck," confirmed still a bottleneck.** `TripType.MultiWay` and `FlightSearchRequestDto.Legs`/`FlightLegDto` exist, and `FlightController.Search` does build a full `Legs` list for multi-city input. But past that point everything collapses to one origin/destination pair: `Results()` only ever takes a single `origin`/`destination`/`date`, and every `IFlightProviderService` implementation (Amadeus, AviationStack, Mock) returns a flat `FlightDto` with an `int Stops` count, not a composite itinerary of legs. There is no code path that turns a `MultiWay` search into more than one search call or renders more than one leg. `Trip` (Core) does have `List<Flight> Flights` and could model a composite itinerary, but nothing populates it from a real multi-leg search.

**Price alerts / notifications:** data model only. `PriceAlert` entity, `IPriceAlertRepository.GetAlertsTriggeredByPriceAsync`, and `FlightController.ToggleFavorite` (which creates/removes alerts) all exist. Nothing ever calls `GetAlertsTriggeredByPriceAsync` — there's no background worker, no MediatR notification, no SignalR hub, no email dispatch tied to price changes. This is 100% ahead of you, not partially built.

---

## Suggested sequencing

1. **Stop the bleeding first:** move the direct `SkyScanDbContext` usage out of `FlightController`, `AccountController`, and `BookingController` into repositories (add `IBookingRepository`). This is the highest-leverage fix — it's what every other cleanup depends on being able to test in isolation.
2. **Split read/write in `AmadeusFlightService`** so search stops silently writing to the database; this also fixes the N+1 query pattern and sets up the seam CQRS commands will slot into.
3. **Introduce MediatR + a first vertical slice of CQRS** (e.g. `SearchFlightsQuery`/`ToggleFavoriteCommand`) so new work goes in the right shape instead of adding more to the controllers.
4. **`ICacheService`/`IDistributedCache` abstraction**, then swap the three `IMemoryCache` call sites over — cheap now, expensive later.
5. **Explicit cookie policy** (`SameSite`, `SecurePolicy`) — small, low-risk, do it opportunistically.
6. **Multi-leg end-to-end** and **price-alert dispatch worker** are the two big feature builds; both need the CQRS/repository cleanup above done first or they'll just add more direct-DbContext code to controllers.
7. Housekeeping: delete `db_test.cs`, delete the excluded `Repositories Implementations` folder, remove `AviationStackFlightService` (or wire it in if it's meant to be a fallback provider), pin `SkyScan.Tests` to `net8.0`.
