/*
  SkyScan High-Volume Database Seed Script
  Target: SQL Server
  Entities: 100 Countries, 2000 Cities, 8000 Airports, 100 Airlines, 200 Airplane Models, 2000 Flights, 10 Users
*/

BEGIN TRANSACTION;
SET NOCOUNT ON;

-----------------------------------------------------------------------
-- 1. SEED COUNTRIES (100)
-----------------------------------------------------------------------
-- Using a CTE to generate 100 country codes and names
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 AS id FROM Digits d1 CROSS JOIN Digits d2)
INSERT INTO [Countries] ([CountryCode], [Name], [Continent])
SELECT 
    CHAR(65 + (id / 26)) + CHAR(65 + (id % 26)), -- Generates AA, AB, AC...
    'Country_' + CAST(id AS VARCHAR(3)),
    CASE WHEN id % 5 = 0 THEN 'Europe' WHEN id % 5 = 1 THEN 'Asia' WHEN id % 5 = 2 THEN 'NorthAm' WHEN id % 5 = 3 THEN 'Africa' ELSE 'SouthAm' END
FROM Nums WHERE id < 100;

-----------------------------------------------------------------------
-- 2. SEED CITIES (2000) - 20 per country
-----------------------------------------------------------------------
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 + d3.n * 100 + d4.n * 1000 AS id FROM Digits d1 CROSS JOIN Digits d2 CROSS JOIN Digits d3 CROSS JOIN Digits d4),
CountryRefs AS (SELECT CountryCode, ROW_NUMBER() OVER(ORDER BY CountryCode) as row_num FROM Countries)
INSERT INTO [Cities] ([CityId], [Name], [CountryCode])
SELECT 
    NEWID(),
    'City_' + CAST(n.id AS VARCHAR(5)),
    c.CountryCode
FROM Nums n
JOIN CountryRefs c ON (n.id % 100) + 1 = c.row_num
WHERE n.id < 2000;

-----------------------------------------------------------------------
-- 3. SEED AIRPORTS (8000) - 4 per city
-----------------------------------------------------------------------
-- We insert in batches to handle the large cross-join logic
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 + d3.n * 100 + d4.n * 1000 AS id FROM Digits d1 CROSS JOIN Digits d2 CROSS JOIN Digits d3 CROSS JOIN Digits d4),
CityRefs AS (SELECT CityId, ROW_NUMBER() OVER(ORDER BY CityId) as row_num FROM Cities)
INSERT INTO [Airports] ([AirportId], [CityId], [Name], [Code], [IataCode], [IcaoCode], [Type])
SELECT 
    NEWID(),
    c.CityId,
    'Airport_' + CAST(n.id AS VARCHAR(5)),
    LEFT(UPPER(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', '')), 3), -- Random 3-letter Code
    LEFT(UPPER(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', '')), 3),
    LEFT(UPPER(REPLACE(CAST(NEWID() AS VARCHAR(36)), '-', '')), 4),
    'International'
FROM Nums n
JOIN CityRefs c ON (n.id / 4) + 1 = c.row_num
WHERE n.id < 8000;

-----------------------------------------------------------------------
-- 4. SEED AIRLINES (100)
-----------------------------------------------------------------------
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 AS id FROM Digits d1 CROSS JOIN Digits d2)
INSERT INTO [Airlines] ([AirlineId], [Name], [HotlineNumber], [IataCode], [IcaoCode])
SELECT 
    NEWID(),
    'Airline_' + CAST(id AS VARCHAR(3)),
    '+1-800-' + CAST(1000000 + id AS VARCHAR(10)),
    CHAR(65 + (id / 26)) + CHAR(65 + (id % 26)),
    'AL' + CAST(id AS VARCHAR(3))
FROM Nums WHERE id < 100;

-----------------------------------------------------------------------
-- 5. SEED AIRPLANE MODELS (200) - 2 per airline
-----------------------------------------------------------------------
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 + d3.n * 100 AS id FROM Digits d1 CROSS JOIN Digits d2 CROSS JOIN Digits d3),
AirlineRefs AS (SELECT AirlineId, ROW_NUMBER() OVER(ORDER BY AirlineId) as row_num FROM Airlines)
INSERT INTO [Airplanes] ([AirplaneId], [AirlineId], [Model], [PlaneId], [OwnerCompany], [ManufactureDate], [Seats], [CabinClasses], [Status])
SELECT 
    NEWID(),
    a.AirlineId,
    CASE WHEN n.id % 2 = 0 THEN 'Boeing ' ELSE 'Airbus ' END + CAST(700 + n.id AS VARCHAR(5)),
    'REG-' + CAST(1000 + n.id AS VARCHAR(5)),
    'Owner_' + CAST(n.id AS VARCHAR(3)),
    DATEADD(day, - (n.id * 10), GETDATE()),
    150 + (n.id % 50),
    'Economy,Business,First',
    'Active'
FROM Nums n
JOIN AirlineRefs a ON (n.id / 2) + 1 = a.row_num
WHERE n.id < 200;

-----------------------------------------------------------------------
-- 6. SEED USERS (10)
-----------------------------------------------------------------------
;WITH Nums AS (SELECT n FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) v(n))
INSERT INTO [AspNetUsers] ([Id], [UserName], [Email], [NormalizedUserName], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [Name], [AccessFailedCount], [TwoFactorEnabled], [PhoneNumberConfirmed], [LockoutEnabled])
SELECT 
    NEWID(),
    'user' + CAST(n AS VARCHAR(2)),
    'user' + CAST(n AS VARCHAR(2)) + '@skyscan.com',
    'USER' + CAST(n AS VARCHAR(2)),
    'USER' + CAST(n AS VARCHAR(2)) + '@SKYSCAN.COM',
    1, 'AQAAAAIAAYagAAAAE...', NEWID(), 'Sky User ' + CAST(n AS VARCHAR(2)), 0, 0, 0, 1
FROM Nums;

-----------------------------------------------------------------------
-- 7. SEED FLIGHTS (2000)
-----------------------------------------------------------------------
-- Link flights to random Airlines, Airplanes, and Airports
;WITH Digits AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) v(n)),
Nums AS (SELECT d1.n + d2.n * 10 + d3.n * 100 + d4.n * 1000 AS id FROM Digits d1 CROSS JOIN Digits d2 CROSS JOIN Digits d3 CROSS JOIN Digits d4),
AirportsList AS (SELECT AirportId, ROW_NUMBER() OVER(ORDER BY AirportId) as row_num FROM Airports),
AirlinesList AS (SELECT AirlineId, ROW_NUMBER() OVER(ORDER BY AirlineId) as row_num FROM Airlines),
PlanesList AS (SELECT AirplaneId, AirlineId, ROW_NUMBER() OVER(ORDER BY AirplaneId) as row_num FROM Airplanes)
INSERT INTO [Flights] ([FlightId], [AirlineId], [AirplaneId], [DepartureAirportId], [ArrivalAirportId], [DepartureTime], [ArrivalTime], [FlightNumber], [RedirectURL])
SELECT 
    NEWID(),
    p.AirlineId,
    p.AirplaneId,
    dep.AirportId,
    arr.AirportId,
    DATEADD(hour, n.id, GETDATE()),
    DATEADD(hour, n.id + 3, GETDATE()),
    'SK' + CAST(1000 + n.id AS VARCHAR(5)),
    'https://skyscan.com/book/' + CAST(n.id AS VARCHAR(5))
FROM Nums n
JOIN PlanesList p ON (n.id % 200) + 1 = p.row_num
JOIN AirportsList dep ON (n.id % 8000) + 1 = dep.row_num
JOIN AirportsList arr ON ((n.id + 50) % 8000) + 1 = arr.row_num
WHERE n.id < 2000;

-----------------------------------------------------------------------
-- 8. SEED TICKETS (6000) - 3 classes per flight
-----------------------------------------------------------------------
INSERT INTO [Tickets] ([TicketId], [FlightId], [Price], [CabinClass], [HasFood], [HasWifi], [HasEntertainment])
SELECT 
    NEWID(),
    FlightId,
    100.00 + (ABS(CHECKSUM(NEWID())) % 500),
    1, -- Economy
    1, 0, 1
FROM Flights;

INSERT INTO [Tickets] ([TicketId], [FlightId], [Price], [CabinClass], [HasFood], [HasWifi], [HasEntertainment])
SELECT 
    NEWID(),
    FlightId,
    600.00 + (ABS(CHECKSUM(NEWID())) % 1000),
    2, -- Business
    1, 1, 1
FROM Flights;

COMMIT TRANSACTION;
PRINT 'Successfully seeded high-volume relational data.';