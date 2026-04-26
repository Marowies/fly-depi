#!/usr/bin/env python3
"""
Dataset cleaning script for SkyScan.
Cleans raw aviation datasets to make them usable with application entities.
"""

import csv
import os
import uuid
from datetime import datetime
from pathlib import Path

# Paths
RAW_DATA_DIR = Path("Datasets/Datasets")
CLEAN_DATA_DIR = Path("Datasets/Cleaned")

def ensure_clean_dir():
    """Create cleaned data directory if it doesn't exist."""
    CLEAN_DATA_DIR.mkdir(parents=True, exist_ok=True)

def clean_airports():
    """
    Clean airports.csv:
    - Extract valid airports with IATA or ICAO codes
    - Normalize text fields
    - Filter out closed/heliports without codes
    """
    input_file = RAW_DATA_DIR / "airports.csv"
    output_file = CLEAN_DATA_DIR / "airports_cleaned.csv"
    cities_output = CLEAN_DATA_DIR / "cities_cleaned.csv"
    
    airports = []
    cities_map = {}  # Use (name, country) as key
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            # Skip if no valid codes
            iata = row.get('iata_code', '').strip()
            icao = row.get('icao_code', '').strip()
            local_code = row.get('local_code', '').strip()
            gps_code = row.get('gps_code', '').strip()
            
            # Use available code (prefer IATA, then ICAO, then GPS, then local)
            code = iata or icao or gps_code or local_code
            if not code:
                continue
            
            name = row.get('name', '').strip()
            municipality = row.get('municipality', '').strip()
            country_code = row.get('iso_country', '').strip()
            region = row.get('iso_region', '').strip()
            
            # Skip if essential data missing
            if not name or not municipality:
                continue
            
            airport = {
                'AirportId': str(uuid.uuid4()),
                'Name': name,
                'Code': code,
                'IataCode': iata,
                'IcaoCode': icao,
                'Type': row.get('type', '').strip(),
                'Municipality': municipality,
                'CountryCode': country_code,
                'Region': region,
                'Latitude': row.get('latitude_deg', '').strip(),
                'Longitude': row.get('longitude_deg', '').strip(),
                'ElevationFt': row.get('elevation_ft', '').strip(),
            }
            airports.append(airport)
            
            # Build cities map
            city_key = (municipality, country_code)
            if city_key not in cities_map:
                cities_map[city_key] = {
                    'CityId': str(uuid.uuid4()),
                    'Name': municipality,
                    'Country': country_code,
                    'Region': region
                }
    
    # Write cleaned airports
    if airports:
        with open(output_file, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=airports[0].keys())
            writer.writeheader()
            writer.writerows(airports)
        print(f"✓ Cleaned {len(airports)} airports -> {output_file}")
    
    # Write cities
    cities = list(cities_map.values())
    if cities:
        with open(cities_output, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=cities[0].keys())
            writer.writeheader()
            writer.writerows(cities)
        print(f"✓ Extracted {len(cities)} cities -> {cities_output}")
    
    return airports, cities_map


def clean_aircraft():
    """
    Clean aircraftDatabase.csv:
    - Extract aircraft with valid registration and model
    - Parse dates properly
    - Normalize manufacturer/operator names
    """
    input_file = RAW_DATA_DIR / "aircraftDatabase.csv"
    output_file = CLEAN_DATA_DIR / "aircraft_cleaned.csv"
    airlines_output = CLEAN_DATA_DIR / "airlines_cleaned.csv"
    
    aircrafts = []
    airlines_map = {}
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            icao24 = row.get('icao24', '').strip()
            registration = row.get('registration', '').strip()
            model = row.get('model', '').strip()
            manufacturer = row.get('manufacturername', '').strip() or row.get('manufacturericao', '').strip()
            
            # Skip if no basic identification
            if not icao24 and not registration:
                continue
            
            # Skip if no model info
            if not model:
                continue
            
            # Parse built date
            built_date = row.get('built', '').strip()
            first_flight = row.get('firstflightdate', '').strip()
            
            # Normalize dates
            manufacture_date = parse_date(built_date) or parse_date(first_flight) or "1900-01-01"
            
            # Owner and operator info
            owner = row.get('owner', '').strip()
            operator = row.get('operator', '').strip()
            operator_icao = row.get('operatoricao', '').strip()
            
            # Determine owner company (prefer owner, fallback to operator)
            owner_company = owner or operator or "Private"
            
            # Build aircraft record
            aircraft = {
                'AirplaneId': str(uuid.uuid4()),
                'Icao24': icao24,
                'Registration': registration,
                'Model': model,
                'ManufactureCompany': manufacturer or "Unknown",
                'OwnerCompany': owner_company,
                'ManufactureDate': manufacture_date,
                'StartDate': manufacture_date,  # Assuming start date same as manufacture
                'EndDate': "2099-12-31",  # Default future end date
                'PlaneId': registration or icao24,
                'Seats': estimate_seats(model, row.get('seatconfiguration', '')),
                'SerialNumber': row.get('serialnumber', '').strip(),
                'EngineType': row.get('engines', '').strip(),
                'Operator': operator,
                'OperatorIcao': operator_icao,
                'Status': row.get('status', '').strip(),
            }
            aircrafts.append(aircraft)
            
            # Extract airline from operator
            if operator and operator_icao:
                airline_key = operator_icao
                if airline_key not in airlines_map:
                    airlines_map[airline_key] = {
                        'AirlineId': str(uuid.uuid4()),
                        'Name': operator,
                        'HotlineNumber': "",  # Not available in dataset
                        'IcaoCode': operator_icao,
                        'Callsign': row.get('operatorcallsign', '').strip(),
                    }
    
    # Write cleaned aircraft
    if aircrafts:
        with open(output_file, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=aircrafts[0].keys())
            writer.writeheader()
            writer.writerows(aircrafts)
        print(f"✓ Cleaned {len(aircrafts)} aircraft -> {output_file}")
    
    # Write airlines
    airlines = list(airlines_map.values())
    if airlines:
        with open(airlines_output, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=airlines[0].keys())
            writer.writeheader()
            writer.writerows(airlines)
        print(f"✓ Extracted {len(airlines)} airlines -> {airlines_output}")
    
    return aircrafts, airlines_map


def parse_date(date_str):
    """Parse various date formats to ISO format."""
    if not date_str:
        return None
    
    date_str = date_str.strip()
    
    # Try different formats
    formats = [
        '%Y-%m-%d',
        '%Y-%m',
        '%Y',
        '%d/%m/%Y',
        '%m/%d/%Y',
    ]
    
    for fmt in formats:
        try:
            dt = datetime.strptime(date_str, fmt)
            return dt.strftime('%Y-%m-%d')
        except ValueError:
            continue
    
    # Handle year-only dates
    if len(date_str) == 4 and date_str.isdigit():
        return f"{date_str}-01-01"
    
    return None


def estimate_seats(model, seat_config):
    """Estimate seat count based on aircraft model or configuration."""
    # Try to parse from seat config
    if seat_config:
        # Look for numbers in seat config
        import re
        numbers = re.findall(r'\d+', seat_config)
        if numbers:
            return sum(int(n) for n in numbers[:3])  # Sum first few numbers
    
    # Estimate based on model type
    model_upper = model.upper()
    
    # Small GA aircraft
    if any(x in model_upper for x in ['C150', 'C152', 'PA-28', 'C172', 'C182']):
        return 4
    if any(x in model_upper for x in ['PA-31', 'BE36', 'C210']):
        return 6
    
    # Business jets
    if any(x in model_upper for x in ['CITATION', 'LEARJET', 'GULFSTREAM']):
        return 12
    
    # Regional airliners
    if any(x in model_upper for x in ['CRJ', 'ERJ', 'ATR42', 'ATR72', 'DHC8']):
        return 70
    
    # Narrow body
    if any(x in model_upper for x in ['B737', 'A320', 'A319', 'A321']):
        return 150
    
    # Wide body
    if any(x in model_upper for x in ['B747', 'B777', 'B787', 'A330', 'A340', 'A350', 'A380']):
        return 300
    
    # Helicopters
    if any(x in model_upper for x in ['R44', 'BELL', 'AS350']):
        return 4
    
    return 50  # Default


def clean_countries():
    """Clean and normalize countries data."""
    input_file = RAW_DATA_DIR / "countries.csv"
    output_file = CLEAN_DATA_DIR / "countries_cleaned.csv"
    
    countries = []
    
    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            code = row.get('code', '').strip()
            name = row.get('name', '').strip()
            
            if not code or not name:
                continue
            
            country = {
                'CountryId': str(uuid.uuid4()),
                'Code': code,
                'Name': name,
                'Continent': row.get('continent', '').strip(),
            }
            countries.append(country)
    
    if countries:
        with open(output_file, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=countries[0].keys())
            writer.writeheader()
            writer.writerows(countries)
        print(f"✓ Cleaned {len(countries)} countries -> {output_file}")
    
    return countries


def create_import_sql(airports, cities_map, aircrafts, airlines_map):
    """Generate SQL import statements for reference."""
    sql_file = CLEAN_DATA_DIR / "import_data.sql"
    
    lines = []
    lines.append("-- SkyScan Data Import Script")
    lines.append("-- Generated automatically from cleaned datasets")
    lines.append("")
    
    # Countries insert (reference data)
    lines.append("-- Note: Import countries manually or use existing reference data")
    lines.append("")
    
    # Cities insert template
    lines.append("-- Cities Import")
    lines.append("INSERT INTO Cities (CityId, Name, Country) VALUES")
    city_values = []
    for city in list(cities_map.values())[:100]:  # Limit for sample
        city_values.append(f"    ('{city['CityId']}', '{escape_sql(city['Name'])}', '{escape_sql(city['Country'])}')")
    lines.append(",\n".join(city_values) + ";")
    lines.append("")
    
    # Airports insert template
    lines.append("-- Airports Import (requires CityId mapping)")
    lines.append("-- Map municipality to CityId before import")
    lines.append("")
    
    # Airlines insert template
    lines.append("-- Airlines Import")
    lines.append("INSERT INTO Airlines (AirlineId, Name, HotlineNumber) VALUES")
    airline_values = []
    for airline in list(airlines_map.values())[:50]:  # Limit for sample
        phone = airline.get('HotlineNumber', '') or '000-000-0000'
        airline_values.append(f"    ('{airline['AirlineId']}', '{escape_sql(airline['Name'])}', '{phone}')")
    lines.append(",\n".join(airline_values) + ";")
    lines.append("")
    
    # Aircraft insert template
    lines.append("-- Aircraft Import")
    lines.append("-- Note: Foreign key to AirlineId if applicable")
    lines.append("")
    
    with open(sql_file, 'w', encoding='utf-8') as f:
        f.write("\n".join(lines))
    
    print(f"✓ Generated SQL import template -> {sql_file}")


def escape_sql(value):
    """Escape string for SQL."""
    if not value:
        return ""
    return value.replace("'", "''")


def main():
    print("=" * 60)
    print("SkyScan Dataset Cleaner")
    print("=" * 60)
    print()
    
    # Check if raw data exists
    if not RAW_DATA_DIR.exists():
        print(f"Error: Raw data directory not found: {RAW_DATA_DIR}")
        print("Please ensure the Datasets/Datasets folder exists with raw CSV files.")
        return
    
    ensure_clean_dir()
    
    print("Cleaning datasets...")
    print()
    
    # Clean countries first (reference data)
    countries = clean_countries()
    
    # Clean airports and extract cities
    airports, cities_map = clean_airports()
    
    # Clean aircraft and extract airlines
    aircrafts, airlines_map = clean_aircraft()
    
    # Generate SQL templates
    create_import_sql(airports, cities_map, aircrafts, airlines_map)
    
    print()
    print("=" * 60)
    print("Cleaning Complete!")
    print("=" * 60)
    print(f"\nCleaned files location: {CLEAN_DATA_DIR.absolute()}")
    print("\nFiles generated:")
    print("  - airports_cleaned.csv")
    print("  - cities_cleaned.csv")
    print("  - aircraft_cleaned.csv")
    print("  - airlines_cleaned.csv")
    print("  - countries_cleaned.csv")
    print("  - import_data.sql (SQL templates)")
    print()
    print("Next steps:")
    print("  1. Review cleaned CSV files")
    print("  2. Import into database using import_data.sql as reference")
    print("  3. Map foreign keys (e.g., Airport.CityId -> City.CityId)")


if __name__ == "__main__":
    main()
