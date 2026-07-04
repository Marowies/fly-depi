# SkyScan — Design Document

**Companion to:** `SkyScan_Project_Report.md`
**Status:** Draft — content-complete, pending conversion to formal .docx template
**Diagram notation:** Mermaid (renders natively in GitHub, VS Code, Obsidian; convert to images when embedding in Word)

---

## Table of Contents

1. Purpose of This Document
2. Architectural Overview
3. Use Case Diagram
4. Database Design (ER Diagram + Data Dictionary)
5. Domain Class Diagram
6. Sequence Diagrams
7. Design Decisions & Rationale
8. Security Design
9. Glossary

---

## 1. Purpose of This Document

This document provides the structural and behavioral design views that support the Project Report: the layered architecture, the use-case model, the database schema, the domain class model, and the interaction sequences for SkyScan's three core flows (searching, favoriting/redirect, and automated price checking).

## 2. Architectural Overview

SkyScan follows Clean Architecture with strict inward dependency direction — outer layers depend on inner layers, never the reverse.

```mermaid
graph TD
    subgraph Presentation["SkyScan.Presentation"]
        A1[MVC Controllers]
        A2[Razor Views]
        A3[Middleware]
    end
    subgraph Application["SkyScan.Application"]
        B1[DTOs]
        B2[Service Interfaces]
        B3[Validators]
        B4[AutoMapper Profiles]
    end
    subgraph Infrastructure["SkyScan.Infrastructure"]
        C1[EF Core Repositories]
        C2[Amadeus Integration]
        C3[ASP.NET Identity]
        C4[SMTP Email Service]
        C5[PriceAlertCheckWorker]
    end
    subgraph Core["SkyScan.Core"]
        D1[Domain Entities]
        D2[Repository Interfaces]
        D3[Value Objects - AuthResult]
    end

    Presentation --> Application
    Presentation --> Core
    Application --> Core
    Infrastructure --> Core
    Infrastructure --> Application
    Presentation -. DI composition root .-> Infrastructure
```

**Reading this diagram:** Core has no outgoing dependencies on any other layer — it is pure C# with no EF Core, no ASP.NET, no HTTP concerns. Infrastructure implements the interfaces Core defines. Presentation only ever talks to Core/Application abstractions directly in code; the concrete Infrastructure implementations are wired in once, at startup, via dependency injection (the dashed line).

## 3. Use Case Diagram

Mermaid has no native UML use-case notation, so actors and use cases are modeled as a bipartite graph (actor → use case).

```mermaid
graph LR
    Guest((Guest))
    User((Registered User))
    Amadeus[[Amadeus API]]
    SMTP[[SMTP Provider]]
    Google[[Google OAuth]]

    Guest --> UC1([Search Flights])
    Guest --> UC2([View Flight Details])
    Guest --> UC3([Redirect to Booking Partner])
    Guest --> UC4([View Guest Booking List])

    User --> UC1
    User --> UC2
    User --> UC3
    User --> UC5([Register / Log In])
    User --> UC6([Favorite Route incl. Price Alert])
    User --> UC7([Receive Price-Drop Email])
    User --> UC8([Manage Profile])
    User --> UC9([Enable / Disable Two-Factor Auth])

    UC1 --> Amadeus
    UC7 --> SMTP
    UC5 --> Google
```

## 4. Database Design

### 4.1 Entity-Relationship Diagram

```mermaid
erDiagram
    COUNTRY ||--o{ CITY : contains
    CITY ||--o{ AIRPORT : contains
    AIRLINE ||--o{ FLIGHT : operates
    AIRPLANE ||--o{ FLIGHT : "used on"
    AIRPORT ||--o{ FLIGHT : "departs/arrives"
    FLIGHT ||--o{ TICKET : has
    FLIGHT }o--o{ TRIP : "grouped into"
    TRIP ||--o{ PRICEALERT : "watched by"
    APPLICATIONUSER ||--o{ PRICEALERT : creates
    APPLICATIONUSER ||--o{ SEARCH : performs
    APPLICATIONUSER ||--o{ BOOKING : creates
    FLIGHT ||--o{ BOOKING : "referenced by"
    CITY ||--o{ SEARCH : "origin or destination"

    COUNTRY {
        string CountryCode PK
        string Name
        string Continent
    }
    CITY {
        guid CityId PK
        string Name
        string CountryCode FK
        string IataCode
        int SearchCount
    }
    AIRPORT {
        guid AirportId PK
        string Name
        string Code
        string IataCode
        string IcaoCode
        string Type
        guid CityId FK
    }
    AIRLINE {
        guid AirlineId PK
        string Name
        string IataCode
    }
    AIRPLANE {
        guid AirplaneId PK
        string AircraftCode
        string AircraftName
    }
    FLIGHT {
        guid FlightId PK
        guid AirlineId FK
        guid AirplaneId FK
        string FlightNumber
        guid DepartureAirportId FK
        guid ArrivalAirportId FK
        datetime DepartureTime
        datetime ArrivalTime
        string RedirectURL
    }
    TICKET {
        guid TicketId PK
        decimal Price
        string Currency
        int CabinClass
        bool HasFood
        bool HasWifi
        bool HasEntertainment
        guid FlightId FK
    }
    TRIP {
        guid TripId PK
        double TotalPrice
        int Stops
    }
    PRICEALERT {
        guid Id PK
        guid UserId FK
        guid TripId FK
        decimal TargetPrice
    }
    BOOKING {
        guid BookingId PK
        guid UserId FK
        guid FlightId FK
        datetime BookingDate
    }
    SEARCH {
        guid UserId FK
        guid OriginCityId FK
        guid DestinationCityId FK
        datetime TimeStamp
        datetime DepartureDate
        int Type
    }
    APPLICATIONUSER {
        guid Id PK
        string Name
        string Email
        string PasswordHash
    }
```

Note: `APPLICATIONUSER` is the Identity-backed table (`AspNetUsers`), mapped in code by `Infrastructure.Identity.ApplicationUser`. `Search`, `PriceAlert`, and `Booking` reference it by `UserId` (a foreign key only) rather than holding a full navigation property in the Core-layer entity, which keeps Core free of any Identity dependency.

### 4.2 Data Dictionary

**Country**

| Field | Type | Constraints |
|---|---|---|
| CountryCode | string | PK, exactly 2 chars (ISO alpha-2) |
| Name | string | Required, max 100 |
| Continent | string | Max 10 |

**City**

| Field | Type | Constraints |
|---|---|---|
| CityId | Guid | PK |
| Name | string | Required, max 100 |
| CountryCode | string | Required, FK → Country |
| IataCode | string? | Max 3 |
| SearchCount | int | Default 0; incremented per search, drives trending routes |

**Airport**

| Field | Type | Constraints |
|---|---|---|
| AirportId | Guid | PK |
| Name | string | Required, max 100 |
| Code | string | Required, max 10 |
| IataCode | string? | Exactly 3 chars |
| IcaoCode | string? | Exactly 4 chars |
| Type | string? | Max 50 |
| CityId | Guid | Required, FK → City |

**Airline** *(static reference data — lazily populated)*

| Field | Type | Constraints |
|---|---|---|
| AirlineId | Guid | PK |
| Name | string | Required, max 100 |
| IataCode | string? | 2–3 chars |

**Airplane** *(static reference data — lazily populated)*

| Field | Type | Constraints |
|---|---|---|
| AirplaneId | Guid | PK |
| AircraftCode | string | Required, max 10 |
| AircraftName | string? | Max 150 |

**Flight**

| Field | Type | Constraints |
|---|---|---|
| FlightId | Guid | PK |
| AirlineId | Guid | Required, FK → Airline |
| AirplaneId | Guid | Required, FK → Airplane |
| FlightNumber | string | Required, max 20 |
| DepartureAirportId | Guid | Required, FK → Airport |
| ArrivalAirportId | Guid | Required, FK → Airport |
| DepartureTime | DateTime | Required |
| ArrivalTime | DateTime | Required |
| RedirectURL | string | Valid URL; destination the user is sent to for booking |

`Duration` is a computed property (`ArrivalTime − DepartureTime`), not a stored column.

**Ticket**

| Field | Type | Constraints |
|---|---|---|
| TicketId | Guid | PK |
| Price | decimal(18,2) | Required, 0.01–1,000,000 |
| Currency | string? | Max 3 (ISO currency code) |
| CabinClass | enum (CabinType) | Required |
| HasFood / HasWifi / HasEntertainment | bool | Default false |
| FlightId | Guid | Required, FK → Flight |

**Trip**

| Field | Type | Constraints |
|---|---|---|
| TripId | Guid | PK |
| TotalPrice | double | Default 0 *(flagged for a future decimal conversion — see fix plan)* |
| Stops | int | |
| Flights | collection | One or more Flight entities grouped as this trip |

**PriceAlert**

| Field | Type | Constraints |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | Required, FK → ApplicationUser (FK-only, no Core nav property) |
| TripId | Guid | Required, FK → Trip |
| TargetPrice | decimal(18,2) | Required, 0–1,000,000 |

**Booking**

| Field | Type | Constraints |
|---|---|---|
| BookingId | Guid | PK |
| UserId | Guid | Required |
| FlightId | Guid | Required, FK → Flight |
| BookingDate | DateTime | Defaults to creation time (UTC) |

**Search**

| Field | Type | Constraints |
|---|---|---|
| TimeStamp | DateTime | |
| Type | enum (TripType) | OneWay / RoundTrip / MultiWay |
| DepartureDate | DateTime | |
| OriginCityId / DestinationCityId | Guid | FK → City |
| UserId | Guid | |

## 5. Domain Class Diagram

```mermaid
classDiagram
    class User {
        +Guid Id
        +string Name
        +string Email
        +bool EmailConfirmed
    }
    class ApplicationUser {
        +Guid Id
        +string Name
        +string Email
        +List~Search~ Searches
        +List~PriceAlert~ PriceAlerts
    }
    class Trip {
        +Guid TripId
        +double TotalPrice
        +int Stops
        +List~Flight~ Flights
    }
    class Flight {
        +Guid FlightId
        +string FlightNumber
        +DateTime DepartureTime
        +DateTime ArrivalTime
        +string RedirectURL
        +TimeSpan Duration
    }
    class Ticket {
        +Guid TicketId
        +decimal Price
        +string Currency
        +CabinType CabinClass
    }
    class Airline {
        +Guid AirlineId
        +string Name
        +string IataCode
    }
    class Airplane {
        +Guid AirplaneId
        +string AircraftCode
        +string AircraftName
    }
    class Airport {
        +Guid AirportId
        +string Name
        +string IataCode
        +string IcaoCode
    }
    class City {
        +Guid CityId
        +string Name
        +int SearchCount
    }
    class Country {
        +string CountryCode
        +string Name
        +string Continent
    }
    class PriceAlert {
        +Guid Id
        +Guid UserId
        +Guid TripId
        +decimal TargetPrice
    }
    class Booking {
        +Guid BookingId
        +Guid UserId
        +Guid FlightId
        +DateTime BookingDate
    }
    class AuthResult {
        +bool Succeeded
        +bool IsLockedOut
        +bool RequiresTwoFactor
        +IEnumerable~string~ Errors
    }

    Trip "1" --> "*" Flight : contains
    Flight "1" --> "*" Ticket : has
    Flight "1" --> "1" Airline : operated by
    Flight "1" --> "1" Airplane : uses
    Flight "1" --> "2" Airport : departure/arrival
    Airport "*" --> "1" City : located in
    City "*" --> "1" Country : located in
    PriceAlert "*" --> "1" Trip : watches
    Booking "*" --> "1" Flight : references
    ApplicationUser "1" --> "*" PriceAlert : owns
```

`User` and `ApplicationUser` are intentionally not linked by inheritance in the class model — they're bridged by extension methods (`ToDomain()`, `ToAuthResult()`), not a shared base class, to keep `User` free of any Identity dependency.

## 6. Sequence Diagrams

### 6.1 Flight Search

```mermaid
sequenceDiagram
    actor Guest
    participant FC as FlightController
    participant Cache as IMemoryCache
    participant AFS as AmadeusFlightService
    participant Amadeus as Amadeus API
    participant DB as SkyScanDbContext

    Guest->>FC: GET /Flight/Results (origin, destination, date)
    FC->>DB: Resolve city -> airport IATA codes
    FC->>Cache: Check cached results for this leg
    alt Cache hit
        Cache-->>FC: Cached FlightDto list
    else Cache miss
        FC->>AFS: SearchFlightsAsync(origins, destinations, date)
        AFS->>Amadeus: GET /v2/shopping/flight-offers
        Amadeus-->>AFS: Offer data (JSON)
        AFS->>DB: Resolve/create Airline, Airplane, Airport references
        AFS-->>FC: List of FlightDto
        FC->>Cache: Store results (15 minutes)
    end
    FC-->>Guest: Render Results view
```

### 6.2 Favorite a Flight (Price Alert Creation)

```mermaid
sequenceDiagram
    actor User
    participant FC as FlightController
    participant FlightRepo as IFlightRepository
    participant AlertRepo as IPriceAlertRepository

    User->>FC: POST /Flight/ToggleFavorite (flight details)
    FC->>FlightRepo: EnsureFlightExistsAsync(...)
    FlightRepo-->>FC: Flight (materialized on demand)
    FC->>AlertRepo: EnsureTripExistsForFlightAsync(flightId, price)
    AlertRepo-->>FC: Trip
    FC->>AlertRepo: FindByUserAndTripAsync(userId, tripId)
    alt Already favorited
        FC->>AlertRepo: DeleteAsync(existingAlert)
        FC-->>User: JSON { favorited: false }
    else Not yet favorited
        FC->>AlertRepo: AddAsync(new PriceAlert)
        FC-->>User: JSON { favorited: true }
    end
```

### 6.3 Automated Price-Alert Check (Background Worker)

```mermaid
sequenceDiagram
    participant Worker as PriceAlertCheckWorker
    participant Repo as IPriceAlertRepository
    participant Provider as IFlightProviderService
    participant Email as IEmailService
    participant UserMgr as UserManager

    loop Every 6 hours
        Worker->>Repo: GetAllWithDetailsAsync()
        Repo-->>Worker: Active alerts (future departures only)
        loop For each alert
            Worker->>Provider: SearchFlightsAsync(route, date)
            Provider-->>Worker: Current FlightDto results
            alt Cheaper match found
                Worker->>UserMgr: FindByIdAsync(alert.UserId)
                UserMgr-->>Worker: ApplicationUser
                Worker->>Email: SendEmailAsync(user.Email, alertBody)
                Worker->>Repo: DeleteAsync(alert)
            else No match this cycle
                Worker->>Worker: Leave alert active, retry next cycle
            end
        end
    end
```

## 7. Design Decisions & Rationale

**Repository pattern over direct DbContext access.** Every entity is accessed through a Core-defined interface (e.g., `IFlightRepository`), implemented in Infrastructure. This keeps controllers and application logic decoupled from EF Core specifics and makes the persistence layer swappable/testable in isolation.

**Lazy, static-only persistence from search results.** Only Airline and Airplane reference data are written to the database as a byproduct of a search; Flight/Trip rows are materialized on demand, not on every search. This was a deliberate correction from an earlier design that wrote a Flight/Ticket row per search result, which would have caused unbounded database growth with no corresponding value (most search results are never revisited).

**Domain/Identity separation (`User` vs `ApplicationUser`).** Core must have zero framework dependencies, but authentication needs a production-grade Identity system. The two concerns are reconciled by keeping `Core.Entities.User` as a plain POCO and moving all Identity-specific behavior into `Infrastructure.Identity.ApplicationUser`, connected by mapping extension methods and a Core-owned `AuthResult` value object in place of `IdentityResult`/`SignInResult`.

**One-shot price alerts.** Rather than tracking "already notified" state, an alert is simply deleted once it successfully fires. This keeps the `PriceAlert` schema minimal and avoids an extra status column, at the cost of the user needing to re-favorite a route if they want to keep watching it after a notification.

**Trip as the price-alert unit, not Flight directly.** A `PriceAlert` targets a `Trip` (which groups one or more Flights) rather than a bare Flight, so the same modeling primitive supports both one-way and (future) multi-leg alerts without a schema change later.

## 8. Security Design

- **Password handling:** delegated entirely to ASP.NET Core Identity's built-in hashing (PBKDF2) — SkyScan code never handles or stores raw passwords.
- **Cookies:** the guest "my bookings" cookie is explicitly set `HttpOnly` and `Secure`. The main authentication cookie's global policy (`HttpOnly`/`Secure`/`SameSite`) is not yet explicitly configured at the application level — tracked as an open item in the current-issues fix plan.
- **Two-factor authentication:** supported for registered users via ASP.NET Core Identity's standard TOTP flow.
- **Third-party auth:** Google OAuth is used for social login rather than SkyScan handling third-party credentials directly.
- **Secrets management:** at present, API keys and credentials (Amadeus, SMTP, Google OAuth, database connection string) are stored in `appsettings.json`, which is a known hygiene gap — the fix plan recommends moving these to user secrets (development) or a secrets manager/environment variables (deployed) rather than committing them to source control.
- **External API resilience:** the Amadeus integration currently has no retry/timeout policy — a transient failure in the external API can surface as a failed search rather than being retried automatically; a resilience library (e.g., Polly) is recommended as a near-term hardening step.

## 9. Glossary

| Term | Meaning |
|---|---|
| GDS | Global Distribution System — the type of system Amadeus is; aggregates airline inventory for search/booking. |
| OTA | Online Travel Agency — a third-party booking site (e.g., Kiwi.com) SkyScan may redirect users to. |
| IATA code | 3-letter airport code (e.g., `JFK`) or 2-letter airline code, standardized by the International Air Transport Association. |
| ICAO code | 4-letter airport/aircraft code, standardized by the International Civil Aviation Organization. |
| PNR | Passenger Name Record — a confirmed airline/OTA booking reference. SkyScan does not issue these. |
| CQRS | Command Query Responsibility Segregation — an architectural pattern (planned, not yet implemented) separating read and write operations. |
| DTO | Data Transfer Object — a plain object shaping data between layers, distinct from a domain entity. |
