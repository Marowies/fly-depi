# SkyScan — Project Report

*A Flight Search Engine Powered by the Amadeus API*

**Document type:** Graduation Project Report
**Status:** Draft — content-complete, pending conversion to formal .docx template
**Date:** July 2026

---

## Table of Contents

1. Introduction
2. Problem Statement & Motivation
3. Objectives
4. Scope and Limitations
5. Stakeholders and User Roles
6. System Requirements
7. System Analysis
8. System Architecture and Design
9. Implementation
10. Testing
11. Challenges Faced and Solutions
12. Current Limitations and Known Issues
13. Future Work
14. Conclusion
15. References

---

## 1. Introduction

Air travel pricing is fragmented across airlines, global distribution systems (GDS), and online travel agencies (OTAs), each maintaining its own inventory and pricing engine. For a traveler, finding the best fare for a given route means checking multiple sources independently, with no single tool aggregating live options in one place for a straightforward, no-login-required search experience.

SkyScan is a web-based flight search engine that addresses this by aggregating live flight offers from the Amadeus Self-Service API, presenting them in a unified, comparable format, and letting the user proceed to a booking provider of their choice to complete the purchase. SkyScan deliberately does not process bookings or payments itself — its scope is search, comparison, and price monitoring, not transaction processing.

## 2. Problem Statement & Motivation

Travelers who want to compare fares across airlines and routes currently must either use a single airline's own search (missing competing airlines entirely) or a metasearch engine that may not expose live pricing, saved-route monitoring, or a lightweight no-account search experience. There is also a common pain point once a price is found: fares change quickly, and a traveler who isn't ready to book immediately has no easy way to be notified if the price on a route they're interested in drops later.

SkyScan's motivation is to solve both problems in one product: a fast, live flight search with no account required, plus an account-based price-watching feature for travelers who want to wait for a better fare without manually re-checking.

## 3. Objectives

- Provide live, comparable flight search results (one-way, round-trip, and multi-city) sourced from a real flight-data provider (Amadeus).
- Require no account to search or view results — search is a Guest-accessible feature.
- Allow registered users to save ("favorite") a route and be notified automatically by email if the price drops.
- Surface popular/trending routes to help undecided users discover options.
- Support secure account creation and login, including third-party authentication (Google OAuth).
- Keep the codebase maintainable and extensible by following a strict layered architecture (Clean Architecture / DDD), so that new data providers, booking partners, or features can be added without large-scale rewrites.
- Redirect the user to a real booking destination once they're ready to purchase, rather than attempting to process the transaction within SkyScan itself.

## 4. Scope and Limitations

**In scope:**
- Flight search (one-way, round-trip, multi-city) against the Amadeus Self-Service Flight Offers Search API.
- Guest and registered-User experiences, including Google OAuth login.
- Saving favorite routes and receiving automated price-drop email alerts.
- Trending-route discovery on the homepage.
- Currency conversion for displayed prices.
- Redirecting the user onward to complete their purchase elsewhere.

**Explicitly out of scope:**
- Payment processing of any kind.
- Issuing real tickets, PNRs, or airline/OTA confirmations.
- Guaranteeing that the redirect destination shows the exact fare/flight found on SkyScan — the two systems have independent inventories (see Section 11 and the Design Document for a full discussion of this limitation).

This scope decision is deliberate: SkyScan positions itself as a search and discovery tool, not a travel agency, which avoids the substantial regulatory, security (PCI), and operational overhead of handling payments while still delivering the core value of fast, comparable, live-priced flight search.

## 5. Stakeholders and User Roles

| Role | Capabilities |
|---|---|
| **Guest** | Search flights, view results and flight details, redirect to a booking destination, retain a "my bookings" list via browser cookie (no account). |
| **User** | Everything a Guest can do, plus: register/login (email or Google), save favorite routes, receive price-drop email alerts, view a persistent profile with saved alerts and past search-driven bookings, manage two-factor authentication. |

## 6. System Requirements

### 6.1 Functional Requirements

- The system shall allow any visitor to search for flights by origin city, destination city, and date(s), without requiring an account.
- The system shall support one-way, round-trip, and multi-city trip types.
- The system shall display, for each result: airline, flight number, departure/arrival times, duration, number of stops, price, and available amenities (Wi-Fi, meals, entertainment).
- The system shall cache search results for a short period to reduce redundant calls to the external flight-data provider.
- The system shall let a registered user "favorite" a flight/route, creating a monitored price alert.
- The system shall periodically re-check monitored routes and email the user if the fare drops below their saved target price, or if a cheaper alternative on the same route appears.
- The system shall allow account registration via email/password or Google OAuth.
- The system shall support two-factor authentication for registered users.
- The system shall redirect the user to an external destination to complete any booking.
- The system shall surface trending/popular routes based on recent search activity.
- The system shall convert and display prices in the user's preferred currency.

### 6.2 Non-Functional Requirements

- **Maintainability:** the codebase shall follow a layered Clean Architecture with strict dependency direction (Presentation → Application/Infrastructure → Core), so the domain layer has zero framework dependencies.
- **Performance:** flight search results shall be served from cache where possible; a single search shall avoid redundant external API calls for data that doesn't change within the request (e.g., resolving the same airport twice).
- **Reliability:** failures in the external flight-data provider shall be handled gracefully and shall not crash the request pipeline.
- **Security:** authentication cookies shall be `HttpOnly` and `Secure`; user passwords shall never be stored or logged in plaintext (delegated to ASP.NET Core Identity's hashing).
- **Data integrity:** the production database shall not be altered by unreviewed schema migrations; all schema changes require explicit review given the system operates against a live database.

## 7. System Analysis

### 7.1 Actors

- **Guest** — an anonymous visitor.
- **Registered User** — an authenticated account holder.
- **Amadeus Self-Service API** — external system, source of flight search data.
- **SMTP provider** — external system, delivers price-alert emails.
- **Google OAuth** — external system, third-party authentication.
- **CurrencyFreaks API** — external system, currency conversion rates.

A full use-case diagram, entity-relationship diagram, and sequence diagrams for the core flows are provided in the companion **Design Document**.

### 7.2 Core Domain Concepts

- A **Flight** represents a single flight leg (airline, times, origin/destination airport, aircraft). Flights are materialized in the database only when a user interacts with a result (favoriting it or proceeding to book) — not on every search — to avoid flooding the database with transient search data.
- A **Trip** groups one or more Flights a user has expressed interest in.
- A **PriceAlert** links a User to a Trip with a target price, monitored by a background process.
- A **Booking** is a lightweight record of booking *intent* — created just before the user is redirected off-site — not a confirmed reservation.
- **Airline**, **Airplane**, **Airport**, **City**, and **Country** are static reference data, populated lazily the first time SkyScan encounters a new code from the Amadeus API, rather than pre-seeded in bulk.

## 8. System Architecture and Design

SkyScan follows a four-layer Clean Architecture / Domain-Driven Design structure:

| Layer | Responsibility |
|---|---|
| **SkyScan.Core** | Domain entities, repository interfaces, constants, and value objects. No dependency on EF Core, ASP.NET, or any external framework. |
| **SkyScan.Application** | DTOs, application-service interfaces, request validators, and mapping profiles (AutoMapper). Bridges Core's abstractions to concrete request/response shapes. |
| **SkyScan.Infrastructure** | EF Core (SQL Server) persistence, repository implementations, the Amadeus integration, SMTP email delivery, ASP.NET Core Identity, and the background price-check worker. |
| **SkyScan.Presentation** | ASP.NET Core MVC controllers, Razor views, middleware, and the dependency-injection composition root. |

Dependencies point strictly inward: Presentation depends on Application/Core abstractions; Infrastructure implements those abstractions; Core depends on nothing external. This means the domain model and business rules are fully testable and portable independent of the database engine, the flight-data provider, or the web framework.

A notable architectural decision is the split between `Core.Entities.User` (a plain domain object) and `Infrastructure.Identity.ApplicationUser` (the concrete ASP.NET Core Identity-backed type). This keeps the domain layer free of a hard dependency on the Identity framework, while still using Identity's production-grade authentication machinery underneath, bridged by a small set of mapping extension methods.

Full architecture, ER, use-case, and sequence diagrams are provided in the **Design Document**.

### Technology Stack

| Concern | Technology |
|---|---|
| Backend framework | ASP.NET Core MVC, .NET 8 |
| Persistence | Entity Framework Core, SQL Server |
| Authentication | ASP.NET Core Identity, Google OAuth |
| Object mapping | AutoMapper |
| Caching | `IMemoryCache` (in-process) |
| External flight data | Amadeus Self-Service API (Flight Offers Search, Airline Code Lookup) |
| Email delivery | SMTP (Gmail) |
| Currency conversion | CurrencyFreaks API |
| Testing | xUnit / Moq (validators and controllers) |

## 9. Implementation

### 9.1 Flight Search

Search requests are resolved to one or more IATA airport codes per city (a city may have multiple airports), then passed to the Amadeus integration. Round-trip searches use Amadeus's native `returnDate` parameter on a single Flight Offers Search call, parsing both the outbound and return itinerary from one response rather than issuing two independent searches. Results are cached for 15 minutes per origin/destination/date combination to avoid re-querying Amadeus for identical searches in quick succession.

### 9.2 Static Reference Data Persistence

A deliberate policy governs what gets written to the database from a search: only static reference data — airlines and aircraft types not already known to the system — are persisted as they're encountered. Flights themselves are not persisted on every search; they are materialized on demand only when a user takes an action on a specific result (favoriting or proceeding toward booking). This keeps the database from accumulating large volumes of transient search data that has no lasting value.

### 9.3 Price Alerts

When a user favorites a result, SkyScan materializes the underlying Flight/Trip records and creates a `PriceAlert` with the user's target price. A background worker (`PriceAlertCheckWorker`, implemented as an ASP.NET Core `BackgroundService`) re-queries the route every six hours, and if the same flight or a cheaper alternative on the route is found below the target price, emails the user and removes the alert (each alert is one-shot).

### 9.4 Authentication

Registered-user authentication is handled by ASP.NET Core Identity, supporting email/password registration with email confirmation, Google OAuth login, and optional two-factor authentication. The domain layer never depends on Identity types directly — `IUserRepository` in Core exposes a Core-owned `AuthResult` value object, and the Infrastructure layer translates ASP.NET Identity's `IdentityResult`/`SignInResult` into it.

### 9.5 Guest Support

Because SkyScan doesn't require an account to search, "booking" intent for anonymous visitors is tracked via a browser cookie (`HttpOnly`, `Secure`) rather than a database row, so guests retain a personal "my bookings" list across visits without creating an account.

## 10. Testing

Unit tests cover the flight-search request validator and controller-level logic (e.g., account registration/authentication flows) using xUnit and Moq to isolate controllers from their repository and Identity dependencies. Testing focuses on request validation correctness and controller branching logic; end-to-end/integration testing against a real database and live Amadeus sandbox is performed manually rather than automated at this stage — this is called out explicitly as an area for future investment (Section 13).

## 11. Challenges Faced and Solutions

**Amadeus over-persistence.** An early version of the search pipeline wrote a `Flight` and `Ticket` row to the database on every single search result returned, regardless of whether the user ever interacted with it. This was identified as unsustainable and redesigned so that only static reference data (airlines, aircraft) is persisted from search, with Flight/Trip rows materialized on-demand only when a user actually favorites or books a result.

**Round-trip search gap.** Round-trip date input was initially accepted by the UI but silently dropped before reaching the flight-data provider, so only one-way results were ever returned for round-trip searches. This was fixed by threading the return date through the full pipeline and using Amadeus's native round-trip search parameter, parsing both itineraries from a single API response.

**Live production database constraint.** SkyScan's database is live and in active use, which meant that several architectural improvements (e.g., a proposed schema redesign of the price-alert model) had to be evaluated not just for correctness but for migration risk. Where a schema-changing design would have been required, the team instead found ways to achieve the same functional outcome within the existing schema, and treated any unavoidable schema change as requiring explicit, separate sign-off rather than being bundled into routine refactors.

**Domain/Identity purity.** The original `User` entity extended ASP.NET Core Identity's `IdentityUser<Guid>` directly, meaning the domain layer depended on the Identity framework and repository methods returned framework-specific result types (`IdentityResult`, `SignInResult`). This was refactored into a plain domain `User` POCO plus an Infrastructure-only `ApplicationUser`, bridged by a Core-owned `AuthResult` value object — achieved without requiring a destructive schema change, since the underlying database table and column names were preserved.

## 12. Current Limitations and Known Issues

A full, itemized technical audit — covering dependency-injection consistency, EF Core migration/schema drift, resilience of the external API integration, and code-hygiene items — is maintained separately in `SkyScan_Current_Issues_Fix_Plan.md`. Headline items include: the Amadeus integration currently points at Amadeus's test/sandbox environment rather than production, meaning displayed prices are not yet fully live/accurate; the redirect destination after a search is currently a generic search-engine link rather than a true booking-partner deep link, so it cannot guarantee the destination shows the same fare found on SkyScan; and the external flight-data provider call lacks retry/timeout resilience.

## 13. Future Work

Planned and discussed directions — detailed in `SkyScan_Future_Roadmap_Plan.md` — include: modeling multi-leg/layover itineraries natively rather than as flat flight lists; introducing a CQRS pattern (e.g., via MediatR) to formally separate read and write operations with cross-cutting pipeline behaviors for validation, logging, and caching; moving from in-process caching to a distributed cache (Redis) to support horizontal scaling; extending the price-alert system with in-app (SignalR) notifications alongside email; and — the most time-sensitive item — migrating off the Amadeus Self-Service tier ahead of its announced sunset, likely onto a provider whose search results can be paired with a true affiliate booking deep link (e.g., Kiwi.com's Tequila API), so the price shown to the user and the price they see at checkout come from the same inventory rather than two independent systems.

## 14. Conclusion

SkyScan demonstrates a complete, cleanly-layered flight search engine: live search against a real external provider, a meaningful account-based feature (price alerts) beyond basic search, and a deliberate, well-reasoned scope boundary around not handling payments or bookings directly. The project's architecture was iteratively hardened over the course of development — removing framework leakage from the domain layer, correcting dependency-injection anti-patterns, and fixing functional gaps (round-trip search, price alerts) — all while operating under the real-world constraint of a live production database, which shaped several engineering decisions toward safer, additive changes over convenient rewrites.

## 15. References

- Amadeus for Developers — Self-Service APIs documentation, https://developers.amadeus.com/self-service/apis-docs
- Microsoft — ASP.NET Core Identity documentation, https://learn.microsoft.com/aspnet/core/security/authentication/identity
- Microsoft — Entity Framework Core documentation, https://learn.microsoft.com/ef/core
- Kiwi.com — Tequila API documentation, https://tequila.kiwi.com/
- Companion documents: `SkyScan_Design_Document.md`, `SkyScan_User_Manual.md`, `SkyScan_Current_Issues_Fix_Plan.md`, `SkyScan_Future_Roadmap_Plan.md`
