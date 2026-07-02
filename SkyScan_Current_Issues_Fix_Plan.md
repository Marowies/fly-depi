# SkyScan — Current Issues Fix Plan

**Status: DRAFT — DO NOT EXECUTE until explicitly told to proceed.**

This plan addresses findings from the 2026-07-02 architecture audit. It is tiered by risk to the live database. Nothing here should be started without a green light, and Tier 2/3 items each need their own explicit confirmation even after the plan overall is approved — the live-DB constraint applies per change, not just once.

---

## Tier 0 — Critical, zero schema risk, do first

### 1. Patch the migration snapshot to match `ApplicationUser`
`SkyScanDbContextModelSnapshot.cs` still models the identity table as `SkyScan.Core.Entities.User`. The live DbContext/configs already use `ApplicationUser`. This is not a database change — it's editing a C# file that describes EF's *belief* about the database, so `dotnet ef migrations add` stops trying to drop/recreate `AspNetUsers`.
- Edit the snapshot's `SkyScan.Core.Entities.User` entity block → `SkyScan.Infrastructure.Identity.ApplicationUser`, matching table name (`AspNetUsers`) and all FK references (Search/PriceAlert/Booking configs already point at `ApplicationUser`).
- Do the same for the corresponding `.Designer.cs` in whichever migration last touched Identity (`20260430234204_AddIdentitySupport.Designer.cs`).
- **Acceptance:** running `dotnet ef migrations add ProbeNoop --dry-run`-equivalent (or just `migrations add` and inspecting the generated file before applying) produces an empty or near-empty diff — no `DropTable`/`CreateTable` on `AspNetUsers`. **Do not run `database update`** — this step is about the snapshot file only.

---

## Tier 1 — Code-only fixes, no schema impact

### 2. Move `EnsureTripExistsForFlightAsync` out of `IPriceAlertRepository`
Relocate to `IFlightRepository` (which already owns flight-existence logic) or a new `ITripRepository`. `PriceAlertRepository` should only read/write `PriceAlert` rows.
- Update `FlightController.ToggleFavorite` call site accordingly.
- No schema change — same `Trips`/`Flights` tables, just called from the correct repository.

### 3. Remove direct `SkyScanDbContext` from `AccountController`
`Profile()` currently runs a 5-level `Include/ThenInclude` inline. Replace with a repository method (extend `IUserRepository` or `IPriceAlertRepository` with a `GetProfileDataAsync(Guid userId)`-style query) so the controller only calls interfaces.

### 4. Fix `GlobalExceptionMiddleware` leaking exception details in production
Gate `Detailed = exception.Message` behind `env.IsDevelopment()` (inject `IHostEnvironment` or check `IWebHostEnvironment`); return a generic message otherwise.

### 5. Fix `SkyScan.Tests.csproj` targeting `net10.0`
Change `<TargetFramework>` to `net8.0` to match every other project (including the one it references). Build-breaking risk if left as-is.

### 6. Fix `IGenericRepository<T>.GetByIdAsync(int id)` signature
Change to `Guid id` — every entity in the domain uses a `Guid` key; the current signature can't be legitimately implemented.

### 7. Amadeus service hardening
- Add Polly retry/timeout policy around the Amadeus HTTP client (registered in `Program.cs` via `AddHttpClient` + `.AddPolicyHandler(...)`).
- Replace `Console.WriteLine` calls with injected `ILogger<AmadeusFlightService>`.
- Fix the silent `DateTime.Now` fallback when a date field is missing from the Amadeus response — this should throw or return a clearly-flagged error, not fabricate a date.

### 8. Remove dead code / repo hygiene
- Delete unused `FlightResultsViewModel.ReturnFlights` property (superseded by `FlightDto.ReturnLeg`); confirm no view still reads it first.
- Delete `RunBuild.cs` from the solution root (scratch file, not part of any project).
- Remove now-unused `using SkyScan.Infrastructure.Data.Data_Sources;` / `using Microsoft.EntityFrameworkCore;` from `FlightController.cs`.

### 9. `IdentityResultExtensions` — map `SignInResult.IsNotAllowed`
Add explicit handling (e.g. "email not confirmed") instead of falling through to a generic failure message.

### 10. Pin `Microsoft.Extensions.Identity.Stores` in `SkyScan.Core.csproj` to `8.0.26`
Matches the rest of the solution. (Separately worth discussing: whether Core should reference this package at all, given `User` is documented Identity-free — flagged for the roadmap discussion, not fixed here.)

### 11. Configure global cookie policy in `Program.cs`
Set `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, `SameSite = SameSiteMode.Lax` (or `Strict`, needs a call on whether it breaks the Google OAuth redirect flow) on `ConfigureApplicationCookie`.

### 12. Method-length cleanup (SRP)
Break down `FlightController.Results()` (~75 lines), `FlightController.Search()` (~90 lines), and `BookingController.Book()` (~180 lines) into smaller private helpers, per the project's own 15–20 line guideline. Purely refactor — no behavior change.

### 13. `PriceAlertCheckWorker` — add success-path logging
Right now the worker only logs on failure (`LogWarning` in the catch block); there's no way to confirm a check cycle ran, how many alerts it evaluated, or whether an email actually sent without waiting for the `PriceAlert` row to disappear or the email to land. Add `LogInformation` around `CheckAlertsAsync` (alerts evaluated count) and `EvaluateAlertAsync` (match found / email sent per alert). No schema or behavior change — logging only.

### 14. Search pipeline performance — Amadeus fan-out and per-offer redundancy
Diagnosed 2026-07-02: search latency is mostly self-inflicted, not Amadeus's baseline network latency. Three fixes, all code-only:
- **Parallelize the origin/destination fan-out** in `AmadeusFlightService.SearchFlightsAsync` — the nested `foreach (origin) foreach (destination) await GetAsync(...)` runs every airport-pair call sequentially. Multi-airport cities (e.g. London, New York) turn one logical search into many serial round trips. Switch to `Task.WhenAll` over the origin×destination combinations.
- **Hoist airport resolution out of the per-offer loop.** `ResolveAirportAsync` currently re-queries the DB (with an `Include(City).ThenInclude(Country)` chain) once per offer, even though origin/destination airport is identical across every offer in a given origin-destination pair. Resolve once per pair, before the offer loop, and reuse the result.
- **Widen the airline-name cache beyond a single request.** `ResolveAirlineNameWithCacheAsync`'s cache is a local `Dictionary` scoped to one `SearchFlightsAsync` call. On a cold cache it can trigger a live nested Amadeus API call (`GetAirlineNameAsync`) mid-request for any carrier not yet in the DB. Promote this to an `IMemoryCache` entry (or rely on the DB check, which already exists, and just avoid the redundant per-request reset) so a carrier is only ever looked up from Amadeus once, solution-wide, not once per request.
- Related to item 7 (Amadeus hardening) — do together since both touch `SearchFlightsAsync`.

### 15. Secrets committed in `appsettings.json`
`SkyScan.Presentation\appsettings.json` has live plaintext secrets checked into source control: Amadeus client secret, the `SmarterASPNetConnection` DB password, the Gmail SMTP app password, and the Google OAuth client secret. Recommend moving these to user secrets (local dev) / environment variables or a secrets manager (deployed), with `appsettings.json` holding only placeholders. **Flagging separately because rotating any of these (especially the DB password or Amadeus secret) is a live-credential change, not a pure code change — needs its own confirmation on timing even though it's not a schema change.**

---

## Tier 2 — Needs explicit go-ahead (schema-adjacent, even if low-risk)

These don't require a destructive migration, but they do add to the schema, so each needs a separate "yes, do it" before touching anything.

### 16. Add index on `Flights(FlightNumber, DepartureTime)`
`FlightRepository` filters on this pair directly with no supporting index. An additive index migration is low-risk (no data loss, no column changes) but is still a live-DB migration and needs sign-off.

### 17. Align `Trip.TotalPrice` type with `decimal`
Currently `double`; everywhere else money is `decimal(18,2)`. Comparing a `double` total against a `decimal` alert threshold risks rounding bugs. This **is** a column type change on a live table — needs explicit confirmation and a plan for how existing `TotalPrice` values get cast/migrated.

---

## Explicitly out of scope for this plan
- Any change that drops or recreates a table.
- Anything touching `AspNetUsers` data itself (only the snapshot *file* is touched in Tier 0).
- N+1 query fixes and missing `AsNoTracking()` calls in `GenericRepository` — real, but lower priority than the items above; can be folded into Tier 1 on request.
- Actually rotating the secrets found in item 15 — this plan only covers moving them out of source control structurally; swapping to new credential values is a separate, live-service-affecting action.

---

## Suggested execution order
Tier 0 (item 1) → Tier 1 (items 2–15, any order, independently shippable) → pause for confirmation → Tier 2 (items 16–17) only if explicitly approved.
