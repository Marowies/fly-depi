# SkyScan — Cleanup & Refactor Brief (Phase 0: Audit)

> **Audience:** Coding agent working on the `SkyScan` solution
> **Solution:** `SkyScan.slnx` · .NET 8 · Clean/Onion Architecture · ASP.NET Core MVC
> **This document governs Phase 0 only.** Do not modify code. Produce a report. Stop.

---

## 1. Mission

SkyScan is a graduation project and **will be evaluated**, both on functionality and on
how correctly it demonstrates Clean Architecture principles. "Clean and refactor" here
does not mean cosmetic tidy-up — it means the codebase should hold up to scrutiny from
someone deliberately checking:

- Is the dependency direction actually respected between layers?
- Is business logic in the right place, or leaking into controllers?
- Is there real test coverage, or none at all?
- Do the advertised features (auth, user management, external API integrations) actually
  exist and work as expected?

Your job right now is **not to fix any of this**. Your job is to find it, understand it,
and report it clearly enough that a decision can be made about what to do next.

---

## 2. Ground Rules

These apply for the entirety of this engagement, not just Phase 0:

1. **No code changes in this phase.** This is an audit. Read, trace, analyze — don't edit.
2. **No build or test execution is available to you.** You do not have a way to run
   `dotnet build` / `dotnet test` yourself. Every claim you make (e.g. "this doesn't
   compile," "this test project is broken") must be based on static reading of the code,
   and must be flagged as *unverified — needs confirmation by running locally* rather than
   stated as fact.
3. **Escalate on ambiguity — do not resolve it silently.** If you find a logical
   contradiction, two reasonable-but-different ways a piece of code could be interpreted,
   or something that looks like a bug rather than a style issue, do not guess at the
   "right" answer. Flag it as its own item (see §5).
4. **No scope creep.** Stick to what's asked below. If you notice something interesting
   but out of scope (e.g. an unrelated feature idea), note it briefly at the end under
   "Observations," don't chase it.
5. **Nothing is implicitly approved.** Finding a problem does not mean you should fix it.
   Every fix — even an "obvious" one — waits for explicit go-ahead after this report.

---

## 3. Context Snapshot

Use this as ground truth. Don't rediscover it from scratch — verify and extend it.

**Layers:**

| Layer | Project | Role |
|---|---|---|
| Domain | `SkyScan.Core` | Entities, Constants, DTOs, Repository interfaces |
| Application | `SkyScan.Application` | Service interfaces, AutoMapper profiles, FluentValidation validators, DTOs, (empty) Commands & Queries |
| Infrastructure | `SkyScan.Infrastructure` | EF Core DbContext & Migrations, Repository implementations, Identity, external API services (Amadeus, Nominatim, SMTP, Currency), background workers |
| Presentation | `SkyScan.Presentation` | ASP.NET Core MVC — Controllers, Razor Views, Middleware, `Program.cs` |
| Tests | `SkyScan.Tests` | xUnit + Moq |

**Already-known deviations from textbook Clean Architecture** (confirm these still hold,
don't just take them on faith):

- Empty `Commands/` and `Queries/` folders in Application — no MediatR/CQRS in practice;
  controllers call repositories/services directly.
- Some DTOs live in `SkyScan.Core/DTOs`, coupling Domain to view-level concerns.
- `AccountController` (~29 KB), `BookingController` (~29 KB), `FlightController` (~20 KB)
  are large and likely contain business logic that belongs in Application services.
- `SkyScan.Tests` targets **.NET 10** while the rest of the solution targets **.NET 8** —
  possible build/compatibility mismatch.
- Only 1 validator (`FlightSearchRequestValidator`), 1 AutoMapper profile, and 2 test files
  exist in total — effectively no test safety net.

**Evaluation criteria you must explicitly report against:**

1. Google Sign-In (OAuth)
2. User management
3. External API integrations (Amadeus, Nominatim, Currency, SMTP)
4. Clean Architecture correctness, with Presentation layer as MVC

---

## 4. Audit Mandate

Work through each of these and produce findings for every one — "no issues found" is a
valid and useful finding, don't skip a category just because nothing jumped out.

### 4.1 Architecture integrity
- Verify the dependency graph actually matches what's documented (no layer secretly
  depending the wrong way, e.g. Core referencing Infrastructure or Application types).
- Assess the DTO-in-Core issue: which specific DTOs, what do they contain, what would it
  take to relocate them.
- Assess the empty CQRS folders: are they simply unused scaffolding, or is there a partial
  pattern being followed elsewhere that contradicts them?

### 4.2 Fat controllers / misplaced logic
- For `AccountController`, `BookingController`, `FlightController`: identify specific
  methods/blocks that contain business logic (validation beyond input shape, decision
  logic, orchestration across multiple repositories) that should live in Application
  services instead.

### 4.3 Dead code & consistency
- Unused interfaces, unused using-directives, commented-out blocks, orphaned files.
- Naming inconsistencies across layers (e.g. inconsistent verb/noun conventions,
  mismatched terminology between Entities/DTOs/ViewModels for the same concept).

### 4.4 Test project mismatch
- Confirm the `.NET 10` vs `.NET 8` targeting mismatch in `SkyScan.Tests`.
- Based on static inspection, note whether this looks like it would currently fail to
  build/restore, and why — but mark this as **unverified, needs local confirmation.**
- **Do not fix it.** Flag it and wait.

### 4.5 Test coverage gaps
- Given there are effectively 2 test files total, map out — per feature area (Flight
  Search, Booking, User Accounts, Price Alerts, Location Lookup, Currency,
  Internationalization) — what meaningful test coverage *should* exist.
- Since testing is unfamiliar territory here, briefly explain *why* each proposed test
  area matters (what it would protect against), not just *what* to test.

### 4.6 Calendar integration
- The architecture overview does not mention any calendar integration, but it's expected
  to exist for evaluation. Locate it in the codebase (check Infrastructure services,
  Account/Booking controllers, any Google API scopes configured alongside the OAuth
  setup) and report what you find: where it lives, what it does, whether it looks
  complete or partial.

### 4.7 Missing feature: account deletion
- Confirm there is currently no "delete my account" capability.
- While confirming, check for other standard account-lifecycle gaps that graders
  typically look for (password change, email change, data export/download) and note any
  that are also missing.
- **Go a step further than just flagging this one:** propose a design approach (do not
  implement it). Cover:
  - Soft-delete vs. hard-delete, and a recommendation with reasoning.
  - What cascades — `Booking`, `PriceAlert`, `Search` records tied to the user, and any
    other dependent data.
  - Which layer each piece of the deletion logic belongs in (repository method vs.
    Application-layer orchestration vs. controller).
  - Any auth/security or GDPR-style considerations worth flagging for a project that will
    be evaluated professionally.

### 4.8 Evaluation cross-check
- For each of the 4 evaluation criteria in §3, write a short explicit verdict: does it
  exist, is it complete, does it hold up architecturally, and what (if anything) is
  missing or weak.

---

## 5. Escalation Triggers

Stop and raise a question (rather than deciding on your own) whenever you hit:

- A contradiction between two pieces of business logic that can't both be right.
- A fix that could plausibly change observable behavior, even slightly.
- A genuine judgment call on where something *should* architecturally live, where more
  than one placement is defensible.
- Anything touching authentication, authorization, or user data handling.
- Anything where "confirm this is broken" would require actually running the code.

When this happens, don't silently pick an interpretation — log it as its own item in the
report (see §6, "Needs Decision" column) with the two-or-more options you see and your
lean, if you have one.

---

## 6. Required Report Format

Structure the report as follows.

### 6.1 Summary
3–5 sentences: overall health of the codebase, biggest risks, and whether anything found
would likely block a build entirely.

### 6.2 Findings Table

| # | Area | File(s) | Category | Finding | Severity | Needs Decision? | Proposed Approach (if applicable) |
|---|---|---|---|---|---|---|---|
| 1 | ... | ... | Architecture / Code Quality / Missing Feature / Test Gap / Ambiguity | ... | Low/Med/High | Yes/No | ... |

**Category definitions:**
- *Architecture* — violates Clean Architecture principles.
- *Code Quality* — dead code, naming, duplication, fat methods.
- *Missing Feature* — something expected/required that doesn't exist (e.g. account deletion).
- *Test Gap* — area with no/insufficient coverage.
- *Ambiguity* — genuine judgment call, logged per §5.

### 6.3 Evaluation Criteria Cross-Check
One short paragraph per criterion (Google Sign-In, User Management, API Integrations,
Clean Architecture/MVC), each ending with an explicit verdict: **Solid / Needs Work / Missing**.

### 6.4 Account Deletion — Proposed Design
Dedicated write-up per §4.7 — this is the one place in the report where you go beyond
"here's what's wrong" into "here's a shape for the fix," still without touching code.

### 6.5 Observations (optional)
Anything noticed outside the requested scope, kept brief.

---

## 7. Stop Condition

**Deliver the report. Then stop.**

Do not begin any fixes, do not create branches for changes, do not start writing tests.
Wait for explicit direction on what to tackle first and in what order.
