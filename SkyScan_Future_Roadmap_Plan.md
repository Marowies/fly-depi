# SkyScan — Future/Roadmap Plan

**Status: DRAFT — DO NOT EXECUTE until explicitly told to proceed.**

This covers forward-looking work: features and structural changes from the project's own roadmap that aren't bugs today but are needed for where SkyScan is headed. These are larger, mostly additive, but several carry real schema implications that must be scoped and approved individually before work starts — this document is for sequencing and sizing, not a green light.

---

## 1. Multi-leg & transit trip architecture
**Why:** `Trip` currently models a flat list of `Flight`s with no explicit leg ordering, layover data, or itinerary structure. This is the "current bottleneck" called out in the project's own instructions.
- Introduce a `TripLeg` (or `Itinerary`) concept: ordered legs, each referencing one or more `Flight`s, with explicit sequence and connection-time fields.
- Amadeus's Flight Offers Search already returns this structure (segments within itineraries) — `AmadeusFlightService` needs to map into the new composite shape instead of flattening to first-segment-only.
- **Schema impact:** new table(s) (`TripLegs` or similar) and likely a new FK from `Flight`→`TripLeg` instead of `Flight`→`Trip` directly. This is the single biggest schema change on the roadmap — needs its own dedicated planning pass and staged migration strategy (additive first, backfill, then cut over) given the live-DB constraint.
- **Sequencing note:** doing this after the round-trip work (native `returnDate` API usage, from the Amadeus expansion plan) makes sense, since round-trip is a special case of multi-leg.

## 2. CQRS / MediatR introduction
**Why:** Application layer is supposed to separate reads from writes via CQRS per the project's architecture mandate; today it's still direct repository calls from controllers.
- Introduce MediatR (or a hand-rolled equivalent) with `IRequest`/`IRequestHandler` pairs for existing operations first — start with something low-risk and self-contained like `GetTrendingRoutesQuery` or `ToggleFavoriteCommand` as a pilot, not a big-bang rewrite.
- Add pipeline behaviors for validation (wrap `FlightSearchRequestValidator`, which is currently built but not wired in anywhere — worth confirming), logging, and caching, per the project's own "Pipeline Behaviors, not polluted handlers" instruction.
- **Schema impact:** none. Pure Application-layer restructuring.
- **Sequencing note:** natural to introduce once Tier 1 of the current-issues plan (thin controllers, no direct DbContext) is done — CQRS handlers are the next layer up from clean repositories.

## 3. Caching layer abstraction (`IDistributedCache` / Redis)
**Why:** Flight search results are cached today via raw `IMemoryCache` in `FlightController`, which doesn't survive across instances/restarts and isn't ready for horizontal scaling.
- Introduce an `ICacheService` abstraction in Application, with an `IMemoryCache`-backed implementation now and a Redis-backed (`IDistributedCache`) implementation later — same interface, swappable via DI.
- Move cache-key construction (currently ad hoc string concatenation in `FlightController.SearchLegAsync`) into the abstraction so cache-key format is centralized.
- **Schema impact:** none (this is infrastructure, not persisted data) — though if Redis is added, it's a new external dependency/config surface, not a DB change.
- **Sequencing note:** can happen independently of everything else; lowest-risk item on this list.

## 4. Price alert & notification engine
**Why:** `PriceAlertCheckWorker` exists and emails on match, but the roadmap calls for both in-site (SignalR/WebSocket) and email notifications dispatched asynchronously, and `SavedRoute`/`PriceAlert` tracking should be "highly observable."
- Add a `MediatR` notification (or lightweight domain event) raised when a price-alert match is found, with two handlers: existing email path, and a new SignalR hub push for in-site notifications.
- Consider whether `PriceAlertCheckWorker`'s 6-hour poll interval should become event-driven (e.g., triggered by a scheduled search refresh) rather than pure polling, once Amadeus Flight Offers Price is wired in (see Amadeus expansion plan, Task 4) for cheaper repricing checks.
- **Schema impact:** none required for notifications themselves; if you want notification history/read-state persisted, that's a new additive table (`Notifications`), which is low-risk (new table, no changes to existing ones) but still needs sign-off.

## 5. Amadeus API expansion (carried over from `SkyScan_Amadeus_API_Expansion_Plan.md`)
Already scoped in detail in that file — restated here for sequencing against the rest of this roadmap:
- Task 1: Airport Nearest Relevant (fixes dead `GetNearestCityByCoordinatesAsync` stub) — no schema impact.
- Task 2: Airline Code Lookup (replaces `carrierCode + " Airlines"` placeholder) — no schema impact, reuses existing `Airline` table.
- Task 3: Native round-trip via `returnDate` + real itinerary parsing — feeds directly into item 1 (multi-leg architecture) above; do this first since it's the smaller step toward the same goal.
- Task 4: Flight Offers Price for alert repricing — optional, improves `PriceAlertCheckWorker` accuracy.

## 6. Domain/dependency cleanup worth doing alongside this roadmap
- Revisit whether `IEmailService` belongs in Core at all (it's arguably an Application-layer port, not a domain concept) — small, no schema impact, good to fold in whenever CQRS work touches notification handlers.
- Normalize the `Repositories Interfaces` folder name (space → no space) — cosmetic, zero risk, do whenever convenient.
- Revisit `[Column(TypeName = "decimal(18,2)")]` attributes sitting directly on Core entities (`PriceAlert`, `Booking`) — EF-flavored metadata leaking into "pure" POCOs. Moving this into Fluent configuration (already partially done elsewhere) instead of data annotations would tighten the Core/Infrastructure boundary. No schema impact, purely where the column-type declaration lives.

---

## Suggested overall sequencing
1. Amadeus expansion Tasks 1–2 (quick wins, no schema impact) → Task 3 (round-trip via `returnDate`).
2. Caching abstraction (`ICacheService`) — independent, do anytime.
3. CQRS/MediatR pilot on 1–2 existing operations.
4. Multi-leg trip architecture (largest item — needs its own dedicated schema-migration plan, staged and reviewed separately given the live-DB constraint).
5. Notification engine (SignalR + MediatR events), once multi-leg/price-alert data shape is settled.

Nothing in this document should be started without explicit confirmation — especially item 1 (multi-leg), which needs its own follow-up planning conversation before any schema work is scoped in detail.
