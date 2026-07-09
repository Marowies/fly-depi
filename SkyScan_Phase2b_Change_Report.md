# SkyScan — Phase 2b Change Report (SearchController)

**Scope:** One carried-over ADR addition, plus `SearchController` only, per
`SKYSCAN_PHASE2B_BRIEF.md`. No build/test execution was available; verification steps
below are manual.

**Headline finding:** `SearchController` has no relationship to `ISearchRepository`.
It's a single-action city-autocomplete API endpoint backed by `ILocationSearchService`
(an in-memory index). `ISearchRepository` — trending routes, recent searches, logging a
search — is used exclusively by `FlightController`, which is Phase 2c scope. See Flag F1.

---

## 5.1 Change Log

| # | Item | File(s) | Status | Verification steps |
|---|------|---------|--------|---------------------|
| 1 | ADR: display formatting vs. stateful decisions | `SkyScan/docs/ARCHITECTURE_DECISIONS.md` | Done | Read the new (third) entry; confirms `ICurrencyConversionService` calls from Razor views stay put, distinct from `SetCurrencyCommand`. No code behavior change — documentation only. |
| 2 | `SearchController.SearchCities` reviewed against §2 criteria | `SkyScan.Presentation/Controllers/SearchController.cs` | No change (Leave) | See §5.1a. `GET /api/Search/cities?q=lon` still returns the same `IEnumerable<CitySuggestionDto>` JSON as before — file is byte-for-byte untouched, so behavior is guaranteed identical. |

No `SkyScan.Application/Search/` folder was created — there is nothing in this
sub-phase's scope that meets the Convert bar (see below), so creating an empty feature
folder would be premature (same call made for `Home/` in Phase 2a).

### 5.1a Per-action Convert/Leave reasoning

| Controller | Action | Decision | Reason |
|---|---|---|---|
| `SearchController` | `SearchCities(string q)` | **Leave** | Single call to one service (`ILocationSearchService.Search(q)`), with only trivial glue: a blank/whitespace guard that returns an empty array (equivalent to the "null-check → default result" pattern the criteria explicitly call out as Leave), then `Ok(results)`. No validation beyond input-shape, no multi-service orchestration, no branching on domain rules, no write/side effect. This is the clearest possible instance of the stated one-line-pass-through Leave case. |

That's the entirety of `SearchController` — it has exactly one action.

---

## 5.2 Flags

**F1 — `SearchController` and `ISearchRepository` are unrelated; the brief's framing
assumed otherwise.** The brief's §1 and §4 both hedge carefully around this
("`ISearchRepository` exists per the architecture overview, but its actual controller
usage hasn't been examined in detail"), which turned out to be the right instinct to
build in. Concretely, verified via grep across the whole solution:

- `SearchController` (`api/Search/cities`) depends only on `ILocationSearchService`, an
  in-memory prefix-match index over cities/airports, warmed once at startup in
  `Program.cs` (`InitializeAsync()`). It never touches `ISearchRepository`,
  `SkyScanDbContext`, or any repository.
- `ISearchRepository` (`GetTrendingRoutesSinceAsync`, `GetTopTrendingSearchesAsync`,
  `GetRecentSearchesByUserIdAsync`, `LogSearchAsync`) has exactly one consumer solution-wide:
  `FlightController`, specifically `Index()` (trending routes for the homepage) and
  `Results()` (`LogSearchAsync`, to record what the user searched). Both call sites are
  in `FlightController`, which is explicitly Phase 2c scope, not this one.

Per Ground Rule 3 and the brief's own instruction ("don't resolve the overlap
yourself — flag it"), **not touching `ISearchRepository` or `FlightController` in this
sub-phase.** This is purely informational for whoever scopes Phase 2c: the trending-routes
and log-search logic in `FlightController.Index`/`Results` both look like strong Convert
candidates under the same §2 criteria (multi-repository orchestration in `Index`'s
trending-routes fallback logic; a write with analytics side effects in `LogSearchAsync`'s
call site) — worth having in mind when Phase 2c is scoped, since that's where
`ISearchRepository`'s actual usage lives, not here.

**F2 — Nothing to convert this sub-phase.** `SearchController` has a single action and it
clearly falls on the Leave side of the line (§5.1a). No Command/Query/Handler files were
created, and the controller file itself has zero content changes — confirmed via
`git diff --stat` showing only the pre-existing CRLF-noise diff, not a real change.

---

## 5.3 Observations (out of scope for this sub-phase)

**O1 — `SearchController` is an orphaned/misleadingly-named API controller.** Given F1,
the name `SearchController` (and its route `api/Search/cities`) reads as if it owns
"search" broadly, but it only does city-name autocomplete for the search form's typeahead
— the actual flight-search-history/trending functionality lives entirely in
`FlightController` via `ISearchRepository`. Not a CQRS concern and out of scope to rename
here, but worth a naming-consistency pass in a future cleanup phase (e.g.
`CityAutocompleteController` or similar) if the team wants the name to match what it does.

**O2 — Sandbox file-view sync issue recurred a third time**, this time on
`docs/ARCHITECTURE_DECISIONS.md` after an Edit-tool append: the bash-mounted view kept
showing the pre-edit 62-line/3593-byte version even after the edit succeeded (confirmed
correct via the separate Windows-side Read tool). Same fix as Phase 2a Flag F3: delete +
recreate the file via a bash heredoc from the known-good content, then re-verify line/byte
counts. Flagging again per the brief's §3.6 instruction, since this is now a consistent
pattern across Phase 2a and 2b rather than a one-off.
