You are working on **SkyScan**, a .NET 8 Clean Architecture flight search app (`SkyScan.Core` / `SkyScan.Application` / `SkyScan.Infrastructure` / `SkyScan.Presentation` / `SkyScan.Tests`) integrated with the Amadeus Self-Service APIs via `SkyScan.Infrastructure.Services.AmadeusFlightService`.

**Hard constraint: the database is live.** Do not write or apply EF Core migrations without explicit confirmation. If a task below seems to need a schema change, stop and ask before writing one — most of these do not.

**Architecture rules to follow** (already enforced elsewhere in this codebase — match the existing style):
- New Amadeus calls belong in `SkyScan.Infrastructure.Services`, behind an interface defined in `SkyScan.Application.Interfaces`.
- Controllers only depend on Application interfaces / Core repository interfaces — never call `HttpClient` or Amadeus directly, never reach into `SkyScanDbContext` for anything a repository already covers.
- DTOs returned from Infrastructure services live in `SkyScan.Application.DTOs`.
- Keep methods short (SRP: ~15–20 lines); extract private helpers rather than growing one method.

Work through the tasks below in order — each is self-contained. Build and manually smoke-test after each one before moving to the next; don't batch multiple tasks into one uncommitted change.

---

## Task 1 — Fix `GetNearestCityByCoordinatesAsync` with Airport Nearest Relevant

**Problem:** `SkyScan.Infrastructure.Data.Repositories_Implementations.AirportRepository.GetNearestCityByCoordinatesAsync(double latitude, double longitude)` is a stub that always returns `null` (see the comment: "Latitude and Longitude have been removed from the database schema"). The "Detect Location" GPS button on the homepage (`Views/Flight/Index.cshtml`, calls `FlightController.GetNearestCity`) silently fails on every click.

**Fix:**
1. Add a new Application-layer interface `ILocationLookupService` (or extend the existing flight provider abstraction — your call, but keep it separate from `IFlightProviderService`, this is a different Amadeus product) with a method like `Task<NearestCityDto?> GetNearestCityAsync(double latitude, double longitude)`.
2. Implement it in Infrastructure calling Amadeus's **Airport Nearest Relevant** API (`GET /v1/reference-data/locations/airports?latitude=..&longitude=..`).
3. The response gives you the nearest airport's IATA code. Resolve that to one of your existing seeded `City` rows via `IAirportRepository.GetByIataAsync` (already exists) — don't try to create new City/Airport rows from the API response, just match against what's already seeded.
4. Update `FlightController.GetNearestCity` to call the new service, falling back gracefully (return `NotFound`, same as today) if Amadeus has no match or the airport isn't in your seeded data.
5. No schema change — you're not storing lat/long anywhere, just using them as a one-time lookup key against Amadeus.

**Acceptance:** clicking "Detect Location" with geolocation permission granted actually populates the origin city field, instead of always showing the "Could not determine nearest city" alert.

---

## Task 2 — Real airline names via Airline Code Lookup

**Problem:** `AmadeusFlightService.SearchFlightsAsync` fabricates airline names: `Name = carrierCode + " Airlines"` (e.g. carrier `DL` becomes "DL Airlines" instead of "Delta Air Lines"). This is the placeholder used both for display and for the `Airline` row that gets created in the DB the first time a carrier is seen.

**Fix:**
1. Add `GetAirlineNameAsync(string iataCode)` to the flight provider's supporting interface (or a small dedicated one), calling Amadeus's **Airline Code Lookup** API (`GET /v1/reference-data/airlines?airlineCodes=..`).
2. In `AmadeusFlightService.SearchFlightsAsync`, when creating a new `Airline` row (the "static reference data, persist only if not seen before" block), call this instead of the `carrierCode + " Airlines"` fallback. Keep the string-concat as a last-resort fallback only if Amadeus returns nothing for that code.
3. Consider caching lookups in-memory for the lifetime of one search call (a carrier code repeats across many offers in the same response) — don't call the API once per flight offer.

**Acceptance:** newly-created `Airline` rows show real airline names, not `"XX Airlines"`.

---

## Task 3 — Native round-trip search via `returnDate`

**Problem:** `FlightController.Results` currently runs two independent one-way searches (outbound origin→dest, then a second reversed-direction call for the return leg) and sums their prices. Amadeus's Flight Offers Search API natively supports round trips in a single call when you pass `returnDate`, returning a bundled fare (often cheaper than two one-ways summed) with two itineraries per offer instead of one.

**Fix:**
1. Extend `IFlightProviderService.SearchFlightsAsync` (or add an overload) to accept an optional `returnDate`.
2. In `AmadeusFlightService`, when `returnDate` is provided, add it to the query string and parse **both** entries in each offer's `itineraries` array (today's code only reads `itineraries[0]`) — the first is outbound, the second is the return leg.
3. `FlightDto` currently models a single directional flight. You'll need either: (a) a new `RoundTripFlightDto` wrapping an outbound + return `FlightDto` pair with one combined price, or (b) split the bundled offer back into two `FlightDto`s post-parse with the combined price allocated however you decide (e.g. attributed to the outbound, return shown as included). Decide based on how `FlightResultsViewModel`/`Results.cshtml` should present it — this is a product decision, not just a technical one, so pick the simpler option (b) unless there's a reason to model it more richly.
4. Update `FlightController.Results` to make one call instead of two when it's a round trip.
5. Leave `MockFlightProviderService` and `AviationStackFlightService` on the current two-call approach (or drop them from the round-trip path) — this native round-trip support is Amadeus-specific, not a general `IFlightProviderService` capability.

**Acceptance:** round-trip searches hit Amadeus once, not twice, and the displayed total price reflects a real bundled round-trip fare rather than two one-ways summed.

---

## Task 4 — Reprice price alerts with Flight Offers Price

**Problem:** `PriceAlertCheckWorker.EvaluateAlertAsync` calls the full `SearchFlightsAsync` and then guesses which result matches the original flight by flight number. This works but is the wrong tool — it re-searches an entire route instead of asking Amadeus "is this specific offer still available at this price."

**Fix:** this is a nice-to-have refinement, not a bug fix — only do it if Task 1–3 are done and verified. Requires deciding whether to store enough of the original Amadeus offer (or at least re-derive a comparable one) to call **Flight Offers Price** meaningfully; if that's not practical without a schema change, leave the current re-search approach as-is rather than force it.

---

## Explicitly out of scope for this plan
Hotel Search/List/Booking/Ratings, Transfer Search/Booking/Management, Tours and Activities — different product domain than SkyScan's current scope (flight search + price alerts for Guest/User roles). Don't pull these in without a separate product decision.
