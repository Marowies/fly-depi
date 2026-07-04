# SkyScan — DEPI Final Project Presentation Content

Matches the official **DEPI Final Project PPT Template** structure (10 slides) you provided. Content is presentation-ready (short, on-slide phrasing) with fuller speaker notes underneath each slide. Fill in the bracketed placeholders — those are the only things I don't know (your name, team, dates, links).

**Mandatory DEPI color scheme** (apply when the actual `.pptx` is built):

| Role in deck | Color | Hex |
|---|---|---|
| Primary / dominant (title slides, section headers) | Green | `#047954` |
| Secondary (supporting backgrounds, icon fills) | Light Green | `#0FAB7D` |
| Complementary accent (charts, secondary icons) | Blue | `#336EA8` |
| Sharp accent (stat callouts, highlights, CTAs) | Gold | `#D7B119` |

---

## Slide 1 — Cover

**On-slide:**
- **Project Title:** SkyScan — A Flight Search Engine Powered by the Amadeus API
- **Presenter's Name:** [Your Name / Team Names]
- **Date:** [Presentation Date]

---

## Slide 2 — Project Idea

**On-slide:**
- **Problem:** Flight pricing is fragmented across airlines, GDS systems, and OTAs — no single tool lets a traveler compare live fares without creating an account, and there's no easy way to be notified when a price on a route they like drops later.
- **Solution:** SkyScan aggregates live flight search results from the Amadeus API, lets anyone search and compare instantly with no login, and lets registered users save routes and get automatic email alerts when prices drop — then redirects the user to a real booking partner to complete the purchase.
- **Unique Value Proposition:**
  - No-login search *and* passive price-watching, in one product — most tools force you to pick one.
  - Honest scope: SkyScan doesn't pretend to process your booking — it focuses on finding and monitoring the best fare, and hands you off cleanly when you're ready to buy.
  - Built on a clean, layered architecture designed to extend (new data providers, new features) without rewrites.

**Speaker notes:** Lead with the two-sided pain point (comparison friction + no passive monitoring), then land on the UVP being the *combination*, not either piece alone.

---

## Slide 3 — Project Wireframe *(Software Project)*

**On-slide — main screens (insert real screenshots/mockups here once available):**
- **Homepage:** search form (one-way / round-trip / multi-city, city autocomplete, cabin class) + trending routes carousel
- **Results page:** flight cards (airline, times, duration, stops, price, amenities), price/airline filters, outbound + return sections for round trips
- **Flight Details view:** full itinerary breakdown, amenities (Wi-Fi, meals, entertainment)
- **Profile page:** saved price alerts (route, target price)
- **My Bookings:** booking-intent history (works for guests too, via cookie)
- **Login / Register:** email/password or Google OAuth

**On-slide — user journey:**
1. **Onboarding:** land on homepage, search immediately — no account required
2. **Main workflow:** search → compare results → view details → redirect to book (or favorite to watch the price first)
3. **Core UX principle:** never block the primary action (searching) behind a login wall — account is opt-in, only needed for price alerts

**Speaker notes:** Emphasize that the entire search-to-redirect path works for anonymous users; the account layer is additive, not a gate.

---

## Slide 4 — End Users + Features

**On-slide — End Users:**
- **Leisure travelers** comparing options for an upcoming trip
- **Price-conscious / budget travelers** waiting for a fare to drop
- **Frequent flyers** who repeatedly check the same routes
- **Casual/undecided travelers** browsing trending destinations

**On-slide — Features (problem → benefit):**

| Feature | Problem it solves | Who benefits |
|---|---|---|
| Live flight search (one-way / round-trip / multi-city) | Comparing fares across sources is slow and fragmented | All travelers |
| No-login search | Signup friction turns users away before they even see results | Casual browsers |
| Price alerts (favorite + auto email) | Manually re-checking fares repeatedly is tedious | Price-conscious & frequent travelers |
| Trending routes | Decision paralysis when a destination isn't decided yet | Undecided travelers |
| Guest booking history (cookie-based) | Forced account creation just to track intent | Casual/one-time users |
| Currency conversion | Comparing prices across currencies is error-prone | International travelers |

---

## Slide 5 — Data Structure

**On-slide — Database:**
- **Type:** Relational (SQL Server, via Entity Framework Core)
- **Main entities:** Country, City, Airport, Airline, Airplane, Flight, Ticket, Trip, PriceAlert, Booking, Search, ApplicationUser (Identity)
- **Key relationships:** Country → City → Airport; Flight → Airline/Airplane/Airport; Trip groups Flights; PriceAlert watches a Trip on behalf of a User; Booking references a Flight
- **Data flow:**
  - **Collection:** live flight offers pulled from the Amadeus API at search time
  - **Storage:** only static reference data (airlines, aircraft types) persisted automatically; Flight/Trip records materialized on demand only when a user favorites or proceeds to book — not on every search
  - **Access:** repository pattern (one interface per entity) — no direct database access from controllers

*(No CSV dataset — SkyScan consumes a live external API rather than a static dataset.)*

**Speaker notes:** The lazy-persistence policy is worth calling out explicitly — it's a deliberate design choice to avoid flooding the database with one-time search data.

---

## Slide 6 — Programming Languages + Frameworks

**On-slide — Programming Languages:**
- C#
- JavaScript
- HTML / CSS (Razor views)

**On-slide — Frameworks & Libraries:**
- ASP.NET Core MVC (.NET 8)
- Entity Framework Core
- ASP.NET Core Identity
- AutoMapper

**On-slide — Supporting Technologies:**
- Amadeus Self-Service API (flight search & pricing)
- Google OAuth (social login)
- SMTP (price-alert emails)
- CurrencyFreaks API (currency conversion)
- SQL Server
- GitHub (version control)

---

## Slide 7 — Live Application + Testing

**On-slide — Application Status:**
- **Beta** — core features functional against a live database; a few known integration gaps remain (flight-data provider currently on a test/sandbox environment rather than production; redirect destination not yet a matched booking-partner deep link)

**On-slide — Testing:**
- **Unit testing:** xUnit + Moq, covering request validators and controller logic
- **Integration testing:** manual verification against the live database and Amadeus sandbox
- **User testing:** manual walkthroughs of the search → favorite → alert → redirect flow

**On-slide — Feedback & Improvements:**
- Iterative architecture audits surfaced and fixed: over-persistence of search data, a silently-dropped round-trip search parameter, direct-database-access anti-patterns in controllers, and a framework dependency leaking into the domain layer — each fixed without disrupting the live database.

---

## Slide 8 — Deliverables

**On-slide — Documentation:**
- Technical documentation (architecture, design decisions)
- Design document (ER diagram, class diagram, sequence diagrams)
- User manual
- Known-issues / fix-plan report and future roadmap report

**On-slide — Milestones:**
1. Core architecture & Amadeus integration
2. Flight search (one-way, round-trip, multi-city)
3. Price alert system + background monitoring worker
4. Domain/Identity architecture refactor
5. Full architecture audit & documentation pass

**On-slide — Final Deliverables:**
- Working application (SkyScan)
- Source code repository
- Full documentation set (project report, design document, user manual)
- This presentation

---

## Slide 9 — Project Team + Roles

**On-slide (fill in per team member):**

| Name | Role | Responsibilities |
|---|---|---|
| [Name] | Backend / .NET Developer | Core, Application, and Infrastructure layers; Amadeus integration |
| [Name] | Frontend / UI Developer | Razor views, search & results UX |
| [Name] | Database Designer | Schema design, EF Core configuration |
| [Name] | QA / Documentation | Testing, architecture audit, documentation |

**On-slide — Collaboration:**
- [GitHub / Trello / Jira / Discord / Teams — fill in what your team actually used]
- [Agile/Scrum or your actual working process]

---

## Slide 10 — Thank You

**On-slide:**
- Contact: [Email]
- GitHub: [Link]
- LinkedIn: [Link]
- **Thank You!**
- **Questions?**

---

## Build notes for when the actual .pptx is created

- Apply the DEPI palette per the table at the top — green as the dominant color (title/section slides), light green and blue as supporting tones, gold reserved for callouts/highlights (e.g., the UVP bullets on Slide 2, the stat-style milestones on Slide 8).
- Slide 3 (wireframe) and Slide 9 (team) are the two slides most in need of your input before finalizing — one needs real screenshots/mockups, the other needs actual names and tools.
- Slides 5–8 can pull directly from `SkyScan_Design_Document.md` (ER diagram) and `SkyScan_Project_Report.md` (milestones, testing section) once those are converted to visuals.
