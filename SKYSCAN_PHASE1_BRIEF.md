# SkyScan — Cleanup & Refactor Brief (Phase 1: Safe Fixes)

> **Audience:** Coding agent working on the `SkyScan` solution
> **Prerequisite:** `SKYSCAN_CLEANUP_BRIEF.md` (Phase 0) and the resulting audit report.
> **This document governs Phase 1 only.** Everything here has already been decided —
> no design ambiguity remains. Make the changes, then stop and report.

---

## 1. Context

Phase 0's audit is complete. This phase covers only the items that are low-risk,
unambiguous, and don't require any architectural judgment call — security hardening,
dead code removal, and one scoped feature removal. Everything with real design decisions
(CQRS retrofit, Calendar extraction, Identity consistency, account deletion, testing) is
intentionally **out of scope here** and will come in later phases.

---

## 2. Ground Rules

Same as Phase 0, with one change: you are now allowed to edit code, scoped strictly to
the items in §3.

1. **No build/test execution available to you.** Every change must include the manual
   verification steps needed for a human to confirm it (what to run, what to look for).
2. **Stay inside the list in §3.** If you notice something else while working — even
   something that looks trivial — do not fix it. Note it under "Observations" in your
   report instead.
3. **Escalate on ambiguity.** If any item turns out to be more involved than described
   here once you're actually looking at the code (e.g. a "delete this file" turns out to
   have a live reference somewhere), stop and flag it rather than deciding how to resolve
   the surprise yourself.
4. **One commit (or clearly delimited change) per item**, so each is independently
   reviewable and revertible.
5. **No new packages**, no schema/migration changes in this phase.

---

## 3. Scoped Fixes

### 3.1 Security

**Gate exception detail leakage** — `GlobalExceptionMiddleware.cs`
Currently the response body always includes `Detailed = exception.Message`, regardless
of environment. Gate this behind `IHostEnvironment.IsDevelopment()`; return a generic
message in all other environments.
*Verification:* trigger any unhandled exception locally in Development (should still see
detail) vs. simulate/inspect the non-Development path (should not).

### 3.2 Test project alignment

**Fix `.NET` version mismatch** — `SkyScan.Tests.csproj`
Currently targets `net10.0` while every referenced project (`SkyScan.Core`,
`SkyScan.Application`, `SkyScan.Infrastructure`, `SkyScan.Presentation`) targets
`net8.0`. Pin `SkyScan.Tests` to `net8.0`.
*Verification:* confirm the `<TargetFramework>` now matches the rest of the solution;
flag if this surfaces any new compile errors that were previously masked by the version
mismatch (report, don't silently fix).

### 3.3 Feature removal — trending route pricing

**Remove fabricated `MinPrice` from trending routes** — `FlightController.Index()`
(lines ~97, ~120 per the audit report)
Currently falls back to `150 + new Random().Next(50, 400)` — a fake price shown to users
as if real. Per decision: remove the price entirely from this feature. Trending routes
should surface **only** the route information (origin/destination or city names),
nothing price-related. Update the corresponding view(s) to drop whatever UI element
displayed this fabricated price, rather than leaving an empty/placeholder price field.
*Verification:* load the home page (or wherever trending routes render) and confirm
routes display without any price, and no lingering empty price label/styling artifact.

### 3.4 Dead code removal

Remove each of the following. For each, confirm zero references anywhere in the solution
before deleting — if a reference turns up that contradicts the audit report, stop and
flag it rather than deleting anyway.

| Item | Location | Action |
|---|---|---|
| Unused Identity package reference | `SkyScan.Core.csproj` | Remove the `Microsoft.Extensions.Identity.Stores` package reference — confirmed zero usage in Core. |
| Empty DTO folder | `SkyScan.Core/DTOs` | Delete the empty folder (real DTOs already correctly live in `SkyScan.Application/DTOs`). |
| Leftover breadcrumb file | `SkyScan.Infrastructure/Services/FlightFilteringService.cs` | Delete — contains only a comment marking a completed move to Application layer. |
| Scratch debug file | `RunBuild.cs` (solution root) | Delete — not part of any `.csproj`, not part of the build. |
| Stale csproj exclusion rules | `SkyScan.Infrastructure.csproj` | Remove the `<Compile Remove>` / `<EmbeddedResource Remove>` / `<None Remove>` entries pointing at `Data\Repositories Implementations\**` — that folder no longer exists on disk. |
| Unused AutoMapper wiring | `Program.cs`, `SkyScan.Application/Mappings/MappingProfile.cs` | Confirm (re-verify per §2.3) zero call sites of `IMapper`/`_mapper.` anywhere in the solution. If confirmed, remove `AddAutoMapper` registration, the `MappingProfile` class, and the AutoMapper package reference. If you find any usage the audit missed, stop and flag instead of removing. |

### 3.5 Naming/filing consistency

| Item | Location | Action |
|---|---|---|
| Folder naming mismatch | `SkyScan.Core/Repositories Interfaces` | Rename to `Repositories_Interfaces` to match the underscore convention used in the corresponding Infrastructure folder and in both namespaces. |
| Misfiled interface | `SkyScan.Core/Repositories Interfaces/IEmailService.cs` | Move out of the repositories folder — `IEmailService` is a notification port, not a repository. Place in a `Services`/`Interfaces` folder in Core. |
| EF annotation leaking into domain entities | `PriceAlert.cs`, `Booking.cs` | Move `[Column(TypeName = "decimal(18,2)")]` off the entity property and into the corresponding Fluent API configuration (`PriceAlertConfiguration`, matching the pattern already used elsewhere in `DbConfigurations/`). |

### 3.6 Logging consistency

**Replace `Console.WriteLine` with `ILogger<T>`** — `AmadeusFlightService`,
`NominatimGeocodingService`
Inject `ILogger<T>` (constructor injection, matching the pattern already used in
`PriceAlertCheckWorker`) and replace error/diagnostic `Console.WriteLine` calls with the
equivalent `ILogger` calls at an appropriate level (`LogWarning`/`LogError` as fits each
case). Don't change *what* is logged, only *how*.

---

## 4. Required Report Format

### 4.1 Change Log
One row per item completed:

| Item | File(s) | Status | Verification Steps (for you to run) |
|---|---|---|---|
| ... | ... | Done / Flagged (see below) | ... |

### 4.2 Flags
Anything from §3 that turned out to be more involved than expected, or where you found a
contradiction with the audit report (e.g. an unexpected reference before deleting
something). Explain what you found and wait for direction — don't resolve it yourself.

### 4.3 Observations
Anything noticed outside this scope, briefly — for later phases, not for now.

---

## 5. Stop Condition

**Complete the items in §3, produce the report in §4, then stop.**
Do not begin Phase 2 (CQRS/MediatR retrofit) or any other later-phase work. Wait for
explicit direction before continuing.
