# Architecture Decisions

Short, dated entries explaining decisions that aren't obvious from the code alone.
Newest at the bottom.

---

## 2026-07-09 — Selective CQRS retrofit (not every action becomes a Command/Query)

**Decision:** The MediatR/CQRS retrofit (Phase 2, sub-phases 2a–2e) does not convert
every controller action into a Command or Query + Handler. An action is converted only
if it does at least one of the following:

- Contains validation logic beyond basic input-shape checks.
- Orchestrates more than one repository/service call, or a multi-step process.
- Contains real business/decision logic (branching on domain rules, not just
  if-null-return-404 style guards).
- Is a write with side effects beyond the one entity being written.

A genuine one-line pass-through — a single call to one repository/service method with
only trivial glue around it (null-check → `NotFound()`, or map the result straight into
a `ViewResult`/`JsonResult`) — stays a direct call in the controller.

**Why:** Wrapping every trivial action in a Command/Query + Handler adds a file, a
namespace, and a DI registration for logic that is, and will remain, one line. That's
boilerplate without a payoff — there's no validation, orchestration, or business rule to
isolate. Reserving the pattern for actions that actually have logic worth relocating
keeps the Application layer meaningful: every Handler in there is doing something a
future maintainer would want to unit-test or reason about independently of HTTP.

**How to apply:** When touching a controller action during the CQRS retrofit, run it
through the checklist above. If borderline, convert — the cost of an unnecessary Handler
is much lower than an inconsistent-looking judgment call. Each sub-phase's change report
lists every action left as a direct call, with a one-line reason, so the decision is
visible and reviewable rather than looking like an incomplete retrofit.

---

## 2026-07-09 — `ICookieWriter` abstraction for cookie-writing command handlers

**Decision:** Command handlers in `SkyScan.Application` that need to write an HTTP
response cookie (`SetLanguageCommandHandler`, `SetCurrencyCommandHandler`) depend on a
new `SkyScan.Application.Common.Interfaces.ICookieWriter` abstraction, not on
`IHttpContextAccessor`/`CookieOptions` directly. `ICookieWriter` is implemented by
`SkyScan.Presentation.Services.CookieWriter`, which is the only place that touches
`IHttpContextAccessor`.

**Why:** `SkyScan.Application` is a plain class library with no reference to ASP.NET
Core. Letting handlers reach for `IHttpContextAccessor`/`CookieOptions` directly would
mean either adding a `FrameworkReference` to the whole ASP.NET Core shared framework (or
an old, frozen `Microsoft.AspNetCore.Http.Abstractions` NuGet package) just to write a
cookie — a framework leak into the layer that's supposed to hold framework-agnostic
business logic. A one-method interface avoids that entirely: Application defines *what*
it needs (append a persistent cookie), Presentation provides *how*, exactly matching the
Dependency Inversion direction the rest of the solution already follows (e.g.
repository interfaces in Core, implementations in Infrastructure).

**How to apply:** Any future Application-layer handler that needs to read or write
something HTTP-specific (cookies, headers, etc.) should get a small abstraction like this
one rather than a direct ASP.NET Core dependency. Don't grow `ICookieWriter` into a
general-purpose `IHttpContext` wrapper — add a new, narrowly-scoped interface per actual
need instead.

---

## 2026-07-09 — Display formatting stays in views; only stateful decisions get a Handler

**Decision:** Calls like `ICurrencyConversionService.GetCurrencySymbolAsync(...)` made
directly from Razor views (`Views/Flight/Results.cshtml`, `Views/Account/Profile.cshtml`)
are left as-is — they are not pulled into a Query as part of the CQRS retrofit. This is
distinct from `SetCurrencyCommand` (Phase 2a), which *is* a Handler.

**Why:** The dividing line isn't "does this call a service" — it's whether the call
changes or persists anything. `GetCurrencySymbolAsync` and `ConvertAsync` are pure,
read-only formatting: given the currency the user already selected (via the
`SelectedCurrency` cookie, itself set through `SetCurrencyCommand`), compute a symbol or
converted amount to render. Nothing is written, no decision is made that outlives the
response, and there's no orchestration beyond one lookup. That's a presentation concern —
the view deciding how to *display* a number — not a business operation. `SetCurrencyCommand`
is different: it's the one place that actually changes the user's stored preference, which
is exactly the kind of state-changing, side-effecting action the retrofit criteria target.

**How to apply:** When deciding whether a piece of view-level logic belongs in a
Query/Handler, ask whether it reads already-decided state to format output (leave in the
view) or whether it decides/persists something new (convert). Don't treat "it calls an
Application-layer service" as sufficient reason to convert — plenty of Application
services exist precisely so views and controllers can call read-only formatting logic
without duplicating it, and that's fine left where it is.
