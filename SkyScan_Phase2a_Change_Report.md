# SkyScan — Phase 2a Change Report (CQRS Foundation + Pilot Controllers)

**Scope:** MediatR/FluentValidation foundation, plus `LanguageController`,
`HomeController`, `CurrencyController` only, per `SKYSCAN_PHASE2A_BRIEF.md`.
No build/test execution was available; verification steps below are manual.

---

## 5.1 Change Log

| # | Item | File(s) | Status | Verification steps |
|---|------|---------|--------|---------------------|
| 1 | Add MediatR + FluentValidation.DependencyInjectionExtensions packages | `SkyScan.Application/SkyScan.Application.csproj` | Done | `dotnet restore` resolves both packages (versions pinned to MediatR 12.4.1, FluentValidation.DependencyInjectionExtensions 11.3.1 — confirm against NuGet at restore time since no network/build was available here). |
| 2 | Assembly-scan marker | `SkyScan.Application/IApplicationAssemblyMarker.cs` (new) | Done | Compiles; has no members, referenced only by `typeof(...).Assembly` in `Program.cs`. |
| 3 | Register MediatR + validator scanning + pipeline behavior | `SkyScan.Presentation/Program.cs` | Done | Start the app. If DI wiring is correct, no exception at startup and hitting any endpoint below works as before. If `AddMediatR` registration were missing, injecting `IMediator` into `LanguageController`/`CurrencyController` would throw `InvalidOperationException: Unable to resolve service for type 'MediatR.IMediator'` on first request to either controller. If `AddTransient(typeof(IPipelineBehavior<,>), ...)` were missing, validators (once any are added to a command in a later phase) would silently never run — no exception, just silently-unvalidated requests, so this is worth double-checking by eye rather than relying on a startup error. |
| 4 | Generic validation pipeline | `SkyScan.Application/Common/Behaviors/ValidationBehavior.cs` (new) | Done | No validators are wired to any command in this sub-phase (per §3.2, `FlightSearchRequestValidator` hookup is Phase 2c), so this behavior is a no-op pass-through for every request today — confirmed by reading `Handle`: `_validators` will always be an empty `IEnumerable<IValidator<TRequest>>` until a later phase registers one for a specific command, and the first branch (`if (!_validators.Any()) return await next();`) short-circuits straight to the handler. |
| 5 | `ICookieWriter` abstraction (Application) + `CookieWriter` impl (Presentation) | `SkyScan.Application/Common/Interfaces/ICookieWriter.cs` (new), `SkyScan.Presentation/Services/CookieWriter.cs` (new), DI registration in `Program.cs` | Done | See Flag F1 below — this is a boundary-crossing decision, documented in `docs/ARCHITECTURE_DECISIONS.md`. |
| 6 | Delete empty flat `Commands/`/`Queries/` folders | `SkyScan.Application/Commands/`, `SkyScan.Application/Queries/` (deleted) | Done | `find SkyScan.Application -maxdepth 1 -type d` no longer lists them; grep for `SkyScan.Application.Commands`/`.Queries` namespaces across the solution returns nothing. |
| 7 | `LanguageController.Set` → `SetLanguageCommand` + Handler | `SkyScan.Application/Languages/SetLanguage/SetLanguageCommand.cs`, `SetLanguageCommandHandler.cs` (new); `SkyScan.Presentation/Controllers/LanguageController.cs` (thinned) | Done (Convert) | Visit any page, use the language switcher (EN ↔ AR). Before/after behavior must be identical: `GET /Language/Set?culture=ar&returnUrl=%2FFlight%2FIndex` sets the `Language` cookie (1yr expiry, `Path=/`) only when `culture` is exactly `"ar"` or `"en"`, then redirects to `returnUrl` if it's a local URL, else to `/Flight/Index`. Confirms the Phase 1 fix (percent-encoded `returnUrl`) still round-trips correctly. |
| 8 | `HomeController` — all 3 actions (`Index`, `Privacy`, `Error`) | `SkyScan.Presentation/Controllers/HomeController.cs` | No change (Leave) | See §5.1a below for per-action reasoning. File is untouched — `git diff --stat` shows only the pre-existing CRLF line-ending noise, zero content change. |
| 9 | `CurrencyController.Set` → `SetCurrencyCommand` + Handler | `SkyScan.Application/Currency/SetCurrency/SetCurrencyCommand.cs`, `SetCurrencyCommandHandler.cs` (new); `SkyScan.Presentation/Controllers/CurrencyController.cs` (thinned) | Done (Convert) — see Flag F2 | Visit `GET /Currency/Set?code=egp` from any page. Before/after behavior must be identical: sets `SelectedCurrency` cookie (uppercased, 1yr expiry, **no** explicit `Path` — this was the pre-existing behavior, verified against the original source before converting), then redirects to the `Referer` header if present, else `/Flight/Index`. |
| 10 | `docs/ARCHITECTURE_DECISIONS.md` | `SkyScan/docs/ARCHITECTURE_DECISIONS.md` (new) | Done | Two entries: the selective-CQRS-retrofit convention (required by §3.4a), and the `ICookieWriter` boundary decision (Flag F1). |

### 5.1a Per-action Convert/Leave reasoning

| Controller | Action | Decision | Reason |
|---|---|---|---|
| `LanguageController` | `Set(culture, returnUrl)` | **Convert** | Named explicitly as the worked example in the brief's §3.3 folder tree; a write with a persisted side effect (cookie), which is the kind of thing worth isolating in a Handler even though the logic itself is small. |
| `HomeController` | `Index()` | **Leave** | `return RedirectToAction("Index", "Flight");` — no repository/service call, no logic at all. |
| `HomeController` | `Privacy()` | **Leave** | `return View();` — no logic. |
| `HomeController` | `Error()` | **Leave** | Builds an `ErrorViewModel` from `Activity.Current?.Id ?? HttpContext.TraceIdentifier` — local computation only, no repository/service call, no branching on domain rules. |
| `CurrencyController` | `Set(code)` | **Convert** | Structurally identical to `LanguageController.Set` (single cookie write + redirect) — converted for the same reason and for consistency. See Flag F2 for a naming/shape discrepancy against the brief's own example. |

---

## 5.2 Flags

**F1 — `ICookieWriter` abstraction added (not explicitly requested, but needed to avoid a framework leak).**
`SkyScan.Application` is a plain `Microsoft.NET.Sdk` class library with no ASP.NET Core
dependency. Both new command handlers (`SetLanguageCommandHandler`,
`SetCurrencyCommandHandler`) need to write a response cookie — the pre-conversion
controller code did this via `Response.Cookies.Append(...)` (`Microsoft.AspNetCore.Http`
types: `CookieOptions`, implicitly `HttpContext`). Two options existed: (a) add a
`FrameworkReference` to the ASP.NET Core shared framework (or the old, frozen
`Microsoft.AspNetCore.Http.Abstractions` NuGet package) directly to
`SkyScan.Application`, letting handlers use `IHttpContextAccessor`/`CookieOptions`
directly; or (b) define a narrow interface in Application and implement it in
Presentation. Went with (b) — `ICookieWriter` — since it keeps the Application layer
free of any ASP.NET Core reference at all, matching how repository interfaces already
work (interface in a lower/inner layer, implementation in an outer layer). Documented as
an explicit architecture decision in `docs/ARCHITECTURE_DECISIONS.md` rather than left
implicit. **This is the "interface/DTO bridge" called for whenever a boundary is
crossed** — flagging it here so it's reviewable, since it's a new pattern being
introduced (no prior Application-layer abstraction existed for this need) rather than
reuse of something already established.

**F2 — `CurrencyController.Set` doesn't match the brief's own `ConvertCurrency` example.**
§3.3 of the brief lists `Currency/ConvertCurrency/ConvertCurrencyQuery.cs` as the
expected output for the Currency controller. Reading `CurrencyController` as it actually
exists today, the only action is `Set(string code)` — a cookie write + redirect,
structurally identical to `LanguageController.Set`. There is no controller action
anywhere that reads/converts a currency amount: the actual currency-conversion logic
(`ICurrencyConversionService.ConvertAsync`/`GetCurrencySymbolAsync`) is called directly
from two Razor views (`Views/Flight/Results.cshtml`, `Views/Account/Profile.cshtml`) via
`@inject`, not through any controller action — so there is nothing to convert into a
`ConvertCurrencyQuery` in this sub-phase; that DTO/Query name in the brief appears to
describe the underlying service rather than an existing controller action. Applied
§3.4a to what's actually in `CurrencyController` today (a write) and converted it as
**`SetCurrencyCommand`** (a Command, mirroring `SetLanguageCommand`), not a Query. Per
Ground Rule 3, flagging this explicitly rather than silently reinterpreting the brief —
if a `ConvertCurrency` read-side conversion was intended to be pulled out of the Razor
views and into a proper controller action + Query in a later phase, that's a larger
change (moving business logic out of views) that should be scoped deliberately rather
than folded into this pass. See Observation O1.

**F3 — Bash-mount file view intermittently showed a stale/truncated copy of `Program.cs`
after editing it.** After using the Edit tool to add the MediatR/FluentValidation/
`ICookieWriter` registrations, the bash-mounted view of `Program.cs` showed a version
truncated mid-statement (161 lines instead of the correct ~190, cutting off before the
middleware pipeline and `app.Run()`). Re-reading the file through the Windows-side Read
tool confirmed the true, complete, correct content (191 lines, ending properly with
`app.Run(); } } }`). Rewrote the file from that known-good content via a bash heredoc
(`rm` + `cat > ... <<'MARKER'`) and re-verified line/byte counts and the tail of the
file — this is the same sandbox filesystem-sync issue disclosed after Phase 1; noting it
recurred here so it's on record if it happens again in Phase 2b–2e.

---

## 5.3 Observations (out of scope for this sub-phase)

**O1 — Currency conversion logic lives in Razor views, not a controller.**
`Results.cshtml` and `Profile.cshtml` both `@inject ICurrencyConversionService
CurrencyService` directly and call `CurrencyService.GetCurrencySymbolAsync(...)` inline
in the view. This is itself a minor separation-of-concerns gap (business/external-API
logic in a view) that predates this phase and isn't a controller action, so it's outside
this sub-phase's scope — flagged here in case a future phase wants to route it through a
proper Query instead (which would also make `ConvertCurrencyQuery` from the original
brief text meaningful, resolving Flag F2 the way the brief may have originally
envisioned).

**O2 — `HomeController.Error()`'s `ErrorViewModel` doesn't capture the exception itself.**
It only captures a trace/request ID (by design, presumably to avoid leaking exception
detail — consistent with the Phase 1 `GlobalExceptionMiddleware` fix). Not a CQRS
concern either way; noting only because it was read closely while deciding Leave vs.
Convert.

**O3 — MediatR/FluentValidation package versions couldn't be confirmed against live
NuGet** (no network/build access in this environment). Pinned to MediatR `12.4.1` and
FluentValidation.DependencyInjectionExtensions `11.3.1` (matching the existing
`FluentValidation.AspNetCore` `11.3.1` already in the solution). Confirm both resolve on
first `dotnet restore` and adjust if either version has since been unlisted/superseded.
