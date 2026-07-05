"""
seed_db.py
----------
Seeds the SkyScan MSSQL database from:
  - airports.csv  → Cities    (CityID, Name, CountryCode, CityIataCode, SearchCount)
  - airports.csv  → Airports  (Name, Code, IataCode, IcaoCode, Type, CityId)
  - countries.csv → Countries (CountryCode, Name, Continent)

The Cities table is populated from OurAirports' airports.csv:
  - Name           <- municipality
  - CountryCode    <- iso_country
  - CityIataCode   <- iata_code
  - SearchCount    <- 0 (default for newly seeded rows)
Rows without a usable municipality, country code, or IATA code are skipped,
and duplicate IATA codes are collapsed to a single row.

The Airports table is populated from the same airports.csv, restricted to
commercial airports only:
  - type in {large_airport, medium_airport}
  - scheduled_service == "yes"
  - a non-empty iata_code
Fields map as:
  - Name        <- name
  - Code        <- ident
  - IataCode    <- iata_code
  - IcaoCode    <- icao_code
  - Type        <- type
  - CityId      <- looked up from the Cities table by matching CityIataCode
                    to the airport's iata_code (no recomputation/assumption
                    about how CityId was generated there)

Usage:
  pip install pyodbc pandas
  python seed_db.py

  # Optional overrides:
  python seed_db.py --cities path/to/airports.csv --airports path/to/airports.csv --countries path/to/countries.csv
"""

import argparse
import sys
import uuid
import pandas as pd
import pyodbc

# ── Connection ────────────────────────────────────────────────────────────────
CONNECTION_STRING = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "Server=SQL6030.site4now.net;"
    "Database=db_acb2c5_skyscan;"
    "UID=db_acb2c5_skyscan_admin;"
    "PWD=SkyScan@123;"
    "Encrypt=yes;"
    "TrustServerCertificate=yes;"
)

# ── Continent name → 2-letter code map ───────────────────────────────────────
CONTINENT_MAP = {
    "africa":        "AF",
    "antarctica":    "AN",
    "asia":          "AS",
    "europe":        "EU",
    "north america": "NA",
    "oceania":       "OC",
    "south america": "SA",
}

BATCH_SIZE = 500   # rows per INSERT batch


# ── Helpers ───────────────────────────────────────────────────────────────────

def continent_code(name: str) -> str:
    """Convert a continent name to its 2-letter code; unknown → 'XX'."""
    if not isinstance(name, str):
        return "XX"
    return CONTINENT_MAP.get(name.strip().lower(), "XX")


def chunked(lst, size):
    for i in range(0, len(lst), size):
        yield lst[i : i + size]


def clean_str_col(series: pd.Series) -> pd.Series:
    """Strip whitespace and normalise NaN-as-string to empty string."""
    s = series.astype(str).str.strip()
    s = s.where(s.str.lower() != "nan", "")
    return s


# ── Seed Countries ─────────────────────────────────────────────────────────────

def seed_countries(conn, csv_path: str) -> set:
    """Insert countries; returns the set of inserted CountryCodes."""
    print(f"\n[1/2] Loading countries from: {csv_path}")
    df = pd.read_csv(csv_path, encoding="utf-8", on_bad_lines="skip")

    # Normalise column names
    df.columns = df.columns.str.strip().str.lower()

    required = {"country", "iso2", "continent"}
    missing = required - set(df.columns)
    if missing:
        sys.exit(f"  [ERROR] countries.csv is missing columns: {missing}")

    # Build clean records
    df = df[["country", "iso2", "continent"]].copy()
    df.rename(columns={"country": "name", "iso2": "code"}, inplace=True)
    df["code"] = df["code"].astype(str).str.strip().str.upper()
    df["name"] = df["name"].astype(str).str.strip()
    df["continent_code"] = df["continent"].apply(continent_code)

    # Drop rows with bad ISO2
    df = df[df["code"].str.len() == 2].drop_duplicates(subset="code")

    cursor = conn.cursor()
    inserted = 0

    for batch in chunked(df.to_dict("records"), BATCH_SIZE):
        for row in batch:
            cursor.execute(
                """
                MERGE Countries AS target
                USING (VALUES (?, ?, ?)) AS src (CountryCode, Name, Continent)
                ON target.CountryCode = src.CountryCode
                WHEN NOT MATCHED THEN
                    INSERT (CountryCode, Name, Continent)
                    VALUES (src.CountryCode, src.Name, src.Continent);
                """,
                row["code"], row["name"], row["continent_code"],
            )
            inserted += 1

    conn.commit()
    print(f"  [OK] Upserted {inserted} countries.")
    return set(df["code"].tolist())


# ── Seed Cities ───────────────────────────────────────────────────────────────

def seed_cities(conn, csv_path: str, valid_codes: set):
    """Insert cities (derived from airports) that reference a valid CountryCode
    and carry a usable IATA code."""
    import time
    print(f"\n[2/2] Loading airports from: {csv_path}")
    df = pd.read_csv(csv_path, encoding="utf-8", on_bad_lines="skip")

    df.columns = df.columns.str.strip().str.lower()

    required = {"municipality", "iso_country", "iata_code"}
    missing = required - set(df.columns)
    if missing:
        sys.exit(f"  [ERROR] airports.csv is missing columns: {missing}")

    df = df[["municipality", "iso_country", "iata_code"]].copy()
    df.rename(
        columns={"municipality": "name", "iso_country": "code", "iata_code": "iata"},
        inplace=True,
    )

    df["name"] = clean_str_col(df["name"])
    df["code"] = clean_str_col(df["code"]).str.upper()
    df["iata"] = clean_str_col(df["iata"]).str.upper()

    # Drop rows missing a city name or IATA code, or with a malformed country code
    before = len(df)
    df = df[(df["name"] != "") & (df["iata"] != "") & (df["code"].str.len() == 2)]
    skipped_incomplete = before - len(df)

    # Only cities whose country is in the DB
    before2 = len(df)
    df = df[df["code"].isin(valid_codes)]
    skipped_country = before2 - len(df)

    # One row per IATA code (each city/airport code should be unique)
    before3 = len(df)
    df = df.drop_duplicates(subset="iata")
    skipped_dupe = before3 - len(df)

    if skipped_incomplete:
        print(f"  [WARN] Skipped {skipped_incomplete} airports missing name/IATA/country.")
    if skipped_country:
        print(f"  [WARN] Skipped {skipped_country} airports with unknown CountryCode.")
    if skipped_dupe:
        print(f"  [WARN] Skipped {skipped_dupe} duplicate IATA codes.")

    # Deterministic UUID per IATA code, so re-runs are idempotent
    df["id"] = df["iata"].apply(lambda x: str(uuid.uuid5(uuid.NAMESPACE_DNS, x)))

    total = len(df)
    print(f"  Total cities to insert: {total:,}")

    # Use fast_executemany for bulk performance + simple INSERT (skip existing rows)
    conn.autocommit = False
    cursor = conn.cursor()
    cursor.fast_executemany = True

    inserted = 0
    t0 = time.time()

    for batch in chunked(df.to_dict("records"), BATCH_SIZE):
        rows = [(r["id"], r["name"], r["code"], r["iata"]) for r in batch]
        cursor.executemany(
            """
            INSERT INTO Cities (CityId, Name, CountryCode, CityIataCode, SearchCount)
            SELECT ?, ?, ?, ?, 0
            WHERE NOT EXISTS (SELECT 1 FROM Cities WHERE CityIataCode = ?);
            """,
            [(r[0], r[1], r[2], r[3], r[3]) for r in rows],
        )
        conn.commit()  # commit each batch so progress is saved
        inserted += len(rows)
        elapsed = time.time() - t0
        pct = inserted / total * 100
        rate = inserted / elapsed if elapsed > 0 else 0
        eta = (total - inserted) / rate if rate > 0 else 0
        print(
            f"  {inserted:,}/{total:,} ({pct:.1f}%) | {rate:.0f} rows/s | ETA {eta:.0f}s   ",
            end="\r",
        )

    print(f"\n  [OK] Upserted {inserted:,} cities.")


# ── Seed Airports ─────────────────────────────────────────────────────────────

COMMERCIAL_TYPES = {"large_airport", "medium_airport"}


def seed_airports(conn, csv_path: str, valid_codes: set):
    """Insert commercial airports only:
       type in {large_airport, medium_airport}, scheduled_service == 'yes',
       and a usable IATA code. CityId is looked up directly from the Cities
       table by matching CityIataCode, so it's always a real, existing FK."""
    import time
    print(f"\n[3/3] Loading airports from: {csv_path}")
    df = pd.read_csv(csv_path, encoding="utf-8", on_bad_lines="skip")

    df.columns = df.columns.str.strip().str.lower()

    required = {
        "ident", "type", "name", "iso_country", "scheduled_service",
        "icao_code", "iata_code",
    }
    missing = required - set(df.columns)
    if missing:
        sys.exit(f"  [ERROR] airports.csv is missing columns: {missing}")

    df = df[[
        "ident", "type", "name", "iso_country", "scheduled_service",
        "icao_code", "iata_code",
    ]].copy()

    df.rename(columns={
        "ident": "code",
        "iso_country": "country",
        "icao_code": "icao",
        "iata_code": "iata",
    }, inplace=True)

    df["code"] = clean_str_col(df["code"])
    df["name"] = clean_str_col(df["name"])
    df["type"] = clean_str_col(df["type"]).str.lower()
    df["country"] = clean_str_col(df["country"]).str.upper()
    df["scheduled_service"] = clean_str_col(df["scheduled_service"]).str.lower()
    df["icao"] = clean_str_col(df["icao"]).str.upper()
    df["iata"] = clean_str_col(df["iata"]).str.upper()

    before = len(df)
    df = df[
        df["type"].isin(COMMERCIAL_TYPES)
        & (df["scheduled_service"] == "yes")
        & (df["iata"] != "")
        & (df["code"] != "")
    ]
    skipped_filter = before - len(df)

    before2 = len(df)
    df = df[df["country"].isin(valid_codes)]
    skipped_country = before2 - len(df)

    before3 = len(df)
    df = df.drop_duplicates(subset="code")
    skipped_dupe = before3 - len(df)

    if skipped_filter:
        print(f"  [WARN] Skipped {skipped_filter} non-commercial/incomplete airports.")
    if skipped_country:
        print(f"  [WARN] Skipped {skipped_country} airports with unknown CountryCode.")
    if skipped_dupe:
        print(f"  [WARN] Skipped {skipped_dupe} duplicate airport codes.")

    # Look up the real CityId for each airport by matching IATA codes against
    # the already-seeded Cities table, instead of assuming how CityId was
    # generated there.
    cursor = conn.cursor()
    cursor.execute("SELECT CityId, CityIataCode FROM Cities WHERE CityIataCode IS NOT NULL")
    city_by_iata = {row[1].strip().upper(): row[0] for row in cursor.fetchall()}

    df["city_id"] = df["iata"].map(city_by_iata)

    before4 = len(df)
    df = df.dropna(subset=["city_id"])
    skipped_no_city = before4 - len(df)
    if skipped_no_city:
        print(f"  [WARN] Skipped {skipped_no_city} airports with no matching city "
              f"in Cities (IATA code not found there).")

    total = len(df)
    print(f"  Total airports to insert: {total:,}")

    conn.autocommit = False
    cursor = conn.cursor()
    cursor.fast_executemany = True

    inserted = 0
    t0 = time.time()

    for batch in chunked(df.to_dict("records"), BATCH_SIZE):
        rows = [
            (
                str(uuid.uuid5(uuid.NAMESPACE_DNS, r["code"])),
                r["name"], r["code"], r["iata"], r["icao"] or None,
                r["type"], r["city_id"],
            )
            for r in batch
        ]
        cursor.executemany(
            """
            INSERT INTO Airports
                (AirportId, Name, Code, IataCode, IcaoCode, Type, CityId)
            SELECT ?, ?, ?, ?, ?, ?, ?
            WHERE NOT EXISTS (SELECT 1 FROM Airports WHERE Code = ?);
            """,
            [row + (row[2],) for row in rows],
        )
        conn.commit()
        inserted += len(rows)
        elapsed = time.time() - t0
        pct = inserted / total * 100
        rate = inserted / elapsed if elapsed > 0 else 0
        eta = (total - inserted) / rate if rate > 0 else 0
        print(
            f"  {inserted:,}/{total:,} ({pct:.1f}%) | {rate:.0f} rows/s | ETA {eta:.0f}s   ",
            end="\r",
        )

    print(f"\n  [OK] Upserted {inserted:,} airports.")

def seed_airlines(conn):
    """Downloads global airline data from OpenFlights, filters for active 
    commercial airlines, and seeds the simplified 3-column Airlines table."""
    import time
    import uuid
    import pandas as pd

    print("\n[Bonus] Loading airlines from OpenFlights live stream...")
    url = "https://raw.githubusercontent.com/jpatokal/openflights/master/data/airlines.dat"
    
    # OpenFlights raw columns
    cols = ["id", "name", "alias", "iata", "icao", "callsign", "country", "active"]
    try:
        df = pd.read_csv(url, names=cols, header=None, encoding="utf-8")
    except Exception as e:
        print(f"  [ERROR] Failed to fetch airline data: {e}")
        return

    # 1. Keep only active airlines
    df = df[df["active"] == "Y"]
    
    # 2. Clean whitespace and normalize text
    df["name"] = df["name"].astype(str).str.strip()
    df["iata"] = df["iata"].astype(str).str.strip().str.upper()

    # 3. Filter for valid, unique 2-letter IATA codes (skipping 'NAN' artifacts)
    df = df[(df["iata"] != "NAN") & (df["iata"] != "") & (df["iata"].str.len() == 2)]
    df = df.drop_duplicates(subset=["iata"])

    # 4. Generate deterministic UUIDs based on the IATA code for consistency
    df["airline_id"] = df["iata"].apply(lambda x: str(uuid.uuid5(uuid.NAMESPACE_DNS, f"airline-{x}")))

    total = len(df)
    print(f"  Total airlines to insert: {total:,}")

    conn.autocommit = False
    cursor = conn.cursor()
    cursor.fast_executemany = True

    # Map directly to your 3 schema columns: (AirlineId, Name, IataCode)
    rows = [
        (
            r["airline_id"],
            r["name"][:100],  # Enforce database string length limits safely
            r["iata"]
        )
        for r in df.to_dict("records")
    ]

    # Batch insert with duplicate safety protection
    cursor.executemany(
        """
        INSERT INTO Airlines (AirlineId, Name, IataCode)
        SELECT ?, ?, ?
        WHERE NOT EXISTS (SELECT 1 FROM Airlines WHERE IataCode = ?);
        """,
        [row + (row[2],) for row in rows]  # row[2] maps to r["iata"] for the WHERE NOT EXISTS check
    )
    conn.commit()
    print(f"  [OK] Upserted {total:,} airlines successfully into [dbo].[Airlines].")


# ── Main ──────────────────────────────────────────────────────────────────────

def parse_args():
    p = argparse.ArgumentParser(description="Seed SkyScan MSSQL DB from CSV files.")
    p.add_argument("--cities",          default="airports.csv",
                   help="Path to airports.csv used for Cities (default: ./airports.csv)")
    p.add_argument("--airports",        default="airports.csv",
                   help="Path to airports.csv used for Airports (default: ./airports.csv)")
    p.add_argument("--countries",       default="countries.csv",
                   help="Path to countries.csv  (default: ./countries.csv)")
    p.add_argument("--skip-countries",  action="store_true",
                   help="Skip seeding countries (fetch existing codes from DB instead).")
    p.add_argument("--skip-cities",     action="store_true",
                   help="Skip seeding the Cities table.")
    p.add_argument("--skip-airports",   action="store_true",
                   help="Skip seeding the Airports table.")
    p.add_argument("--skip-airlines",   action="store_true",
                   help="Skip seeding the Airlines table.")
    return p.parse_args()


def main():
    args = parse_args()

    print("Connecting to SQL Server ...")
    try:
        conn = pyodbc.connect(CONNECTION_STRING, timeout=30)
        conn.autocommit = False
        print("  [OK] Connected.")
    except pyodbc.Error as e:
        sys.exit(f"  [ERROR] Connection failed: {e}")

    try:
        if args.skip_countries:
            print("\n[1/2] Skipping countries - fetching existing codes from DB ...")
            cursor = conn.cursor()
            cursor.execute("SELECT CountryCode FROM Countries")
            valid_codes = {row[0] for row in cursor.fetchall()}
            print(f"  [OK] Found {len(valid_codes)} existing country codes.")
        else:
            valid_codes = seed_countries(conn, args.countries)

        if args.skip_cities:
            print("\n[2/2] Skipping cities.")
        else:
            seed_cities(conn, args.cities, valid_codes)

        if args.skip_airports:
            print("\n[3/3] Skipping airports.")
        else:
            seed_airports(conn, args.airports, valid_codes)
            
        if args.skip_airlines:
            print("\n[4/4] Skipping airlines.")
        else:
            seed_airlines(conn)

        print("\n[OK] Seeding complete.")
    finally:
        conn.close()


if __name__ == "__main__":
    main()