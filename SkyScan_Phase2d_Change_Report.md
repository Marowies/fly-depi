# SkyScan — Phase 2d Change Report (Dead Endpoint + AccountController)

**Scope:** Dead-endpoint investigation (carried from Phase 2c O1) plus all of
`AccountController`, per `SKYSCAN_PHASE2D_BRIEF.md`. No build/test execution was
available; verification steps below are manual (Read-tool cross-checks per Ground
Rule 6, plus solution-wide grep sweeps).

**Stop condition honored:** per the brief, Phase 2e (`BookingController`) was **not**
started. This report closes out Phase 2d only.

---

## 4. Dead Endpoint Investigation

**Finding: the leading hypothesis was correct.** Read `Views/Flight/Index.cshtml` (the
only search view — there is no separate `Search.cshtml`) and confirmed the actual
autocomplete mechanism: a native HTML5 `<datalist id="cityList">`, populated
server-side from `Model.CitiesWithAirports` (itself sourced from
`GetHomeSearchDataQuery` → `AirportDropdownCache`, built in Phase 2c), with
`<input asp-for="OriginCity" list="cityList">` and the same on `DestinationCity`.
The browser filters suggestions against this preloaded `<option>` list entirely
client-side — no JavaScript, no fetch call, nothing server-round-trip per
keystroke. `wwwroot/js/site.js` was re-confirmed to still be the empty default
scaffold stub (4 lines, no logic at all).

Grepped the whole solution for `CityAutocompleteController`, `api/CityAutocomplete`,
`api/Search`, `ILocationSearchService`, and `LocationSearchService`: the only
consumers were the controller itself, its interface, its implementation, and
`Program.cs`'s DI registration + startup warm-up call — no view, no JS file, no
other controller referenced any of it. This confirms `CityAutocompleteController`
had zero live purpose (branch 5.4.3 of the brief applies, not 5.4.4).

**Removed** (commit `69aefe7`):
- `SkyScan.Presentation/Controllers/CityAutocompleteController.cs`
- `SkyScan.Application/Interfaces/ILocationSearchService.cs`
- `SkyScan.Infrastructure/Services/LocationSearchService.cs`
- `SkyScan.Application/DTOs/CitySuggestionDto.cs` (its only consumer was the
  service above)
- `Program.cs`: the `AddSingleton<ILocationSearchService, LocationSearchService>()`
  registration and the "warm up the in-memory search index" startup block. With
  that block gone, `Main()` no longer awaits anything, so its signature was
  simplified from `async Task Main` to `void Main` (avoids a compiler warning
  for an async method with no `await`).
- Updated a comment in `SearchFlightsQueryHandler` (Phase 2c Flag F2) that named
  `ILocationSearchService` as the alternative approach that was deliberately not
  taken — the comment now notes that service was itself removed here, so a
  future reader isn't sent looking for a type that no longer exists.

**Verification:** grepped again post-removal for all four names — zero remaining
references outside this report and commit message. Confirmed via Read tool that
`Program.cs` and `SearchFlightsQueryHandler.cs` both compile-read correctly
(cross-checked against a bash-side desync that briefly appeared here — see Flag
F4).

---

## 5.1 Change Log

| # | Item | File(s) | Status | Verification steps |
|---|------|---------|--------|---------------------|
| 1 | Dead endpoint removal | See §4 | Done | Grep sweep pre/post removal; Read-tool cross-check on Program.cs and SearchFlightsQueryHandler.cs. |
| 2 | `IUserRepository` extended | `SkyScan.Core/Repositories_Interfaces/IUserRepository.cs` | Done | Added `GetUserByIdAsync`, `GetCurrentUserAsync`, `ChangePasswordAsync`, `GetTwoFactorAuthenticationUserAsync`, `GetExternalLoginInfoAsync`, `ExternalLoginSignInAsync`, `LinkExternalLoginAsync`. Read-tool verified. |
| 3 | `UserRepository` implements new methods | `SkyScan.Infrastructure/Data/Repositories_Implementations/UserRepository.cs` | Done | Each new method wraps the exact same `UserManager`/`SignInManager` call the controller used to make directly — same arguments, same order. Read-tool verified full file. |
| 4 | `ExternalLoginData` bridge type | `SkyScan.Core/Entities/ExternalLoginData.cs` (new) | Done | Mirrors `AuthResult`'s existing pattern for wrapping an ASP.NET Identity type (`ExternalLoginInfo`) into a Core-safe shape. |
| 5 | `IUrlBuilder` bridge | `SkyScan.Application/Common/Interfaces/IUrlBuilder.cs` (new), `SkyScan.Presentation/Services/UrlBuilder.cs` (new, via `LinkGenerator`) | Done | Same pattern as `ICookieWriter`/`ICurrentLanguageProvider`. Registered in `Program.cs`. |
| 6 | Register, ConfirmEmail, ResendEmailConfirmation, Login, ForgotPassword, ResetPassword converted | `SkyScan.Application/Account/{Register,ConfirmEmail,ResendEmailConfirmation,Login,ForgotPassword,ResetPassword}/*` (new) | Done (Convert) | Each Handler reproduces the original controller logic verbatim (same lookup → generate-token/verify → conditional email pattern), only relocated. |
| 7 | TwoFactorLogin (GET status query + POST command) | `SkyScan.Application/Account/TwoFactorLogin/*` (new) | Done (Convert) | GET's direct `_signInManager.GetTwoFactorAuthenticationUserAsync()` now routes through `IUserRepository` per the Identity-consistency decision. |
| 8 | EnableTwoFactor (GET setup query + POST command), DisableTwoFactor | `SkyScan.Application/Account/{EnableTwoFactor,DisableTwoFactor}/*` (new), `SkyScan.Application/Account/Common/{EnableTwoFactorHelper,EnableTwoFactorSetupResult,AuthenticatorKeyFormatter}.cs` (new) | Done (Convert) | `EnableTwoFactorHelper.BuildAsync`'s `initializeIfMissing` flag preserves the original's two distinct code paths (GET resets a missing key; POST redisplay branches just re-fetch) exactly. |
| 9 | ExternalLoginCallback | `SkyScan.Application/Account/ExternalLoginCallback/*` (new) | Done (Convert) | Uses the three new `IUserRepository` external-login methods; see Flag F1 for the one deliberate behavior-preservation decision (non-throwing lookup). |
| 10 | ChangePassword | `SkyScan.Application/Account/ChangePassword/*` (new) | Done (Convert) | `IUserRepository.ChangePasswordAsync` folds in the original's follow-up `RefreshSignInAsync` call on success. |
| 11 | Profile — Phase 0 Finding #1 resolved | `SkyScan.Application/Account/GetUserProfile/*` (new), `Views/Account/Profile.cshtml` (`@model` line only) | Done (Convert) | No new repository methods needed — reused `ISearchRepository.GetRecentSearchesByUserIdAsync` and `IPriceAlertRepository.GetPriceAlertsByUserIdAsync`, both of which already eager-load exactly what the view reads. See Flag F2 for the minor limit-count change this introduces. |
| 12 | `AccountController.cs` rewritten as thin controller | `SkyScan.Presentation/Controllers/AccountController.cs` (rewrite) | Done | Constructor reduced to `IMediator`, `IUserRepository`, `SignInManager<ApplicationUser>` (the last only for `GoogleLogin`, see Flag F3). Dead constructor params (`IEmailService`, `UrlEncoder`, `UserManager<ApplicationUser>`, `SkyScanDbContext`) all removed — each now lives inside the Handler(s) that actually need it. |
| 13 | `AccountControllerTests.cs` updated | `SkyScan.Tests/AccountControllerTests.cs` | Done | Rewritten against the new 3-parameter constructor, mocking `IMediator.Send(...)` instead of the individual dependencies the old tests drove directly. See Flag F5 — this narrows what these particular tests exercise. |
| 14 | `Program.cs` DI registrations | `SkyScan.Presentation/Program.cs` | Done | Added `IUrlBuilder → UrlBuilder`. All other dependencies the new handlers need (`IUserRepository`, `ISearchRepository`, `IPriceAlertRepository`, `IEmailService`, `UrlEncoder`) were already registered. |

---

### 5.1a Per-action Convert/Leave reasoning

| Action | Decision | Reason |
|---|---|---|
| `Register` GET | Leave | One-line `View()`. |
| `Register` POST | **Convert** | Write (creates user) + orchestration (token, URL, email) — side effects beyond the one entity. |
| `RegisterConfirmation` GET | Leave | One-line `View()`. |
| `ConfirmEmail` GET | **Convert** | Multi-step: parse Guid, look up user, confirm — plus the Identity-consistency fix (direct `UserManager.FindByIdAsync` → `IUserRepository.GetUserByIdAsync`). |
| `ConfirmEmailError` GET | Leave | One-line `View()`. |
| `ResendEmailConfirmation` GET | Leave | One-line `View()`. |
| `ResendEmailConfirmation` POST | **Convert** | Lookup + conditional token/email side effect. |
| `Login` GET | Leave | Sets `ViewData`, returns view — trivial glue. |
| `Login` POST | **Convert** | Real branching decision logic (success/2FA/lockout/invalid). |
| `Logout` POST | Leave | Single repository call, already routed through `IUserRepository` (no Identity-consistency fix needed) — genuine one-line pass-through. |
| `ForgotPassword` GET | Leave | One-line `View()`. |
| `ForgotPassword` POST | **Convert** | Lookup + conditional token/email side effect, mirrors `ResendEmailConfirmation`. |
| `ResetPassword` GET | Leave | Guard clause is input-shape validation only (`email == null \|\| token == null`), explicitly a Leave case per §2. |
| `ResetPassword` POST | **Convert** | Lookup + reset + multi-error handling. |
| `RefreshCookie` POST | Leave | Single repository call (`RefreshSignInAsync`) with a trivial null-guard — but **gets the Identity-consistency fix anyway** (`UserManager.GetUserAsync` → `IUserRepository.GetCurrentUserAsync`). Demonstrates the fix is orthogonal to the Convert/Leave axis — see Flag F6. |
| `TwoFactorLogin` GET | **Convert** | Needs the Identity-consistency fix (`SignInManager.GetTwoFactorAuthenticationUserAsync` direct call) plus has branching (null → redirect, else → view); borderline size-wise, converted per "if borderline, convert." |
| `TwoFactorLogin` POST | **Convert** | Code sanitization + branching, already partly routed through the repository pre-Phase-2d. |
| `EnableTwoFactor` GET | **Convert** | Multi-repo-call orchestration (get-or-reset key) + real conditional logic. |
| `EnableTwoFactor` POST | **Convert** | Same orchestration plus validation-beyond-shape (code verification) and a write. |
| `TwoFactorEnabled` GET | Leave | One-line `View()`. |
| `DisableTwoFactor` POST | **Convert** | A single-entity write, but matches the `SetCurrencyCommand` precedent (Phase 2a/ADR #2) of converting the one action that actually changes stored user state, plus needs the Identity-consistency fix. |
| `GoogleLogin` POST | **Leave (structural exception)** | See Flag F3 — cannot be represented as MediatR handler output. |
| `ExternalLoginCallback` GET | **Convert** | Per the brief's explicit instruction; heavy multi-step orchestration across three new repository methods. |
| `ChangePassword` POST | **Convert** | Per the brief's explicit instruction. |
| `Profile` GET | **Convert** | Per the brief's explicit instruction (Finding #1). |
| `AccessDenied` GET | Leave | One-line `View()`. |

---

## 5.2 Flags

**F1 — `LinkExternalLoginAsync` uses a non-throwing lookup, unlike the rest of
`UserRepository`.** Every other Identity-native operation on `IUserRepository`
re-resolves the `ApplicationUser` via the shared `RequireAppUserAsync` helper,
which throws `InvalidOperationException` if the user isn't found (acceptable
everywhere else, since those callers already have a confirmed-to-exist `User`).
The original `ExternalLoginCallback`, however, explicitly guarded this exact
lookup and returned a graceful `"Unable to complete Google sign-in."` error
instead of letting an exception reach `GlobalExceptionMiddleware`'s generic 500.
`LinkExternalLoginAsync` preserves that by doing its own non-throwing
`_userManager.FindByIdAsync` check rather than calling `RequireAppUserAsync`.
Also preserved verbatim: the original never checked `AddLoginAsync`'s result
before calling `SignInAsync` unconditionally — a pre-existing latent gap, not
introduced or fixed here (out of scope for an architecture-only phase).

**F2 — `GetUserProfileQueryHandler` requests 10 recent searches instead of the
repository's default of 5.** `Profile.cshtml`'s own Razor code already did
`Model.Searches.OrderByDescending(s => s.TimeStamp).Take(10)` on an unfiltered
`ApplicationUser.Searches` collection, so the page's effective display limit was
already 10. Calling `ISearchRepository.GetRecentSearchesByUserIdAsync(user.Id,
count: 10)` moves that same limit into the query instead of loading full history
and truncating in the view — same end-user-visible result, less data fetched.

**F3 — `GoogleLogin` intentionally was not routed through `IUserRepository`,
despite the Identity-consistency decision's blanket framing.** Its only job is
building an ASP.NET Core `AuthenticationProperties` object and returning a
`Challenge()` result — both framework-native constructs a MediatR handler cannot
return (handlers return plain data, not `IActionResult`). Wrapping this in an
abstraction would mean either leaking `AuthenticationProperties` through Core, or
building a second `ICookieWriter`-style bridge for a single call site with no
reuse. `SignInManager<ApplicationUser>` is injected directly into the controller
for this one action, exactly like `ICookieWriter`'s ADR entry says any *other*
web-framework need should be handled — a small, explicit exception rather than a
strained abstraction. This is a good candidate for a fourth `ARCHITECTURE_DECISIONS.md`
entry if a future phase hits the same shape of problem again (not added yet since
it's a single occurrence so far).

**F4 — Sandbox file-view desync recurred again this phase**, and for the first
time it affected `git` itself rather than just `Read`/bash: `.git/index` was
found to be missing entirely (not just stale), making `git status`/`git diff`
briefly report the *entire* repository as deleted-then-untracked. Recovered with
the same `git read-tree HEAD` fix already used for the "bad signature
0x00000000" index-corruption variant of this bug — confirming both symptoms
share a root cause (an intermittently-lost/corrupted `.git/index`), not two
separate bugs. Also hit the ordinary file-content variant on `Program.cs` twice
(NUL-padded trailing bytes, byte count matching the pre-edit size) — same
Read-tool-confirm-then-heredoc-rewrite fix as every prior phase.

**F5 — `AccountControllerTests.cs` now tests less than it used to, by design.**
The old tests drove `IUserRepository`/`IEmailService`/`UserManager` mocks
directly and asserted on the controller's `IActionResult`, which meant they were
incidentally exercising business logic that has since moved into
`RegisterCommandHandler`, `LoginCommandHandler`, etc. The rewritten tests mock
`IMediator.Send(...)` instead, so they now verify only the controller's own
remaining logic (ModelState guards, Command construction, Result-to-IActionResult
mapping) — correct for what the controller does now, but the business-logic
coverage those old tests provided has no replacement yet. Per Ground Rule 2
(stay inside this phase's scope), new Handler-level unit tests were not added;
flagging this gap explicitly rather than silently narrowing coverage. Also
still blocked, as in every prior phase, by the pre-existing .NET 8 vs .NET 10
`SkyScan.Tests` version mismatch (Phase 0 Finding, Phase 1 fix applied but no
build available to confirm it holds).

**F6 — The Identity-consistency decision was applied independently of the
Convert/Leave decision.** `RefreshCookie` is the clearest example: it stayed
**Leave** (single repository call, trivial glue) but still had its direct
`UserManager.GetUserAsync(User)` call replaced with
`IUserRepository.GetCurrentUserAsync(User)`, per the brief's instruction to
apply the Identity fix "as part of converting each action, not as a separate
pass" — read here as applying to any action touched this phase, converted or
not, since the brief's whole premise is that no direct `UserManager`/
`SignInManager` call should remain in the controller once this phase is done.

---

## 5.3 Observations

**O1 — Profile.cshtml's diff is much larger than its actual change.** The only
intentional edit was the `@model` line (from `SkyScan.Infrastructure.Identity.
ApplicationUser` to `SkyScan.Application.Account.GetUserProfile.
UserProfileResult`); the Razor body is untouched. The committed diff shows the
whole file as changed due to a line-ending normalization difference (consistent
with the CRLF-related noise flagged in the Phase 2b report's O2 and visible
repo-wide in `git status` on files nobody touched, e.g. bundled `bootstrap.js`/
`jquery.js`). Confirmed via `git diff` that the only semantic change is that one
line; not something to "fix" here since normalizing line endings repo-wide is a
separate, out-of-scope concern.

**O2 — Two commits, not the "rename → conversions → validator wiring" split
some other phases used**, matching this phase's own actual shape: one commit
for the dead-endpoint removal (`69aefe7`), one for the entire `AccountController`
conversion (`5f9cf8b`). The 24-action conversion was kept as a single commit
rather than split per-action because nearly every Handler shares the same new
`IUserRepository` methods and the same `Program.cs`/DI changes — splitting
further would have produced non-buildable intermediate states with no way to
verify them (same reasoning Phase 2c used, per that report's O3).

**O3 — `ConfirmEmail`'s two failure paths were collapsed to one.** The original
controller used `RedirectToAction(nameof(ConfirmEmailError))` for
missing-parameter/user-not-found failures, but `View("ConfirmEmailError")`
(direct render, no redirect) for a failed `ConfirmEmailAsync` call. The new
`ConfirmEmailCommandHandler` reports all three as the same `Succeeded = false`,
and the controller always does `View("ConfirmEmailError")`. The page the user
sees is identical in all three cases; only the browser's redirect-vs-direct-render
mechanics differ, with no visible effect. Flagging as a deliberate simplification
rather than an oversight.
