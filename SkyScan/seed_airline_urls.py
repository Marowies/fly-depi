import os
import re
import json
import sys

# Try importing pyodbc
try:
    import pyodbc
except ImportError:
    print("Error: 'pyodbc' is not installed. Installing it via pip...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pyodbc"])
    import pyodbc

# Hardcoded connection string matching seed_db.py
CONNECTION_STRING = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "Server=SQL6030.site4now.net;"
    "Database=db_acb2c5_skyscan;"
    "UID=db_acb2c5_skyscan_admin;"
    "PWD=SkyScan@123;"
    "Encrypt=yes;"
    "TrustServerCertificate=yes;"
)

def parse_js_json(filepath):
    if not os.path.exists(filepath):
        print(f"Warning: File {filepath} not found.")
        return []
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Strip JS comments (single-line and multi-line) safely without touching strings/URLs
    content_clean = re.sub(
        r'("[^"\\]*(?:\\.[^"\\]*)*")|//[^\r\n]*|/\*.*?\*/',
        lambda m: m.group(1) or '',
        content,
        flags=re.DOTALL
    )
    
    # Find array bounds
    start_idx = content_clean.find('[')
    end_idx = content_clean.rfind(']')
    if start_idx == -1 or end_idx == -1:
        print(f"Error: Could not parse array in {filepath}")
        return []
    
    json_str = content_clean[start_idx:end_idx+1]
    try:
        return json.loads(json_str, strict=False)
    except Exception as e:
        print(f"Error parsing JSON from {filepath}: {e}")
        return []

def parse_standard_json(filepath):
    if not os.path.exists(filepath):
        print(f"Warning: File {filepath} not found.")
        return {}
    with open(filepath, 'r', encoding='utf-8') as f:
        try:
            return json.load(f)
        except Exception as e:
            print(f"Error parsing JSON from {filepath}: {e}")
            return {}

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    airlines_path = os.path.join(script_dir, "airlines.json")
    airlines1_path = os.path.join(script_dir, "airlines (1).json")

    # Connect to the database
    print("Connecting to SQL Server...")
    try:
        conn = pyodbc.connect(CONNECTION_STRING, timeout=30)
        print("Successfully connected to the database!")
    except Exception as e:
        print(f"Error: Could not connect to database: {e}")
        return

    # 2. Parse airline json files
    # Dictionary to map IATA -> Url
    iata_urls = {}
    # Dictionary to map Name -> Url
    name_urls = {}

    # Read airlines.json (dictionary structure)
    airlines_data = parse_standard_json(airlines_path)
    for key, val in airlines_data.items():
        name = val.get("name")
        iata = val.get("IATA")
        website = val.get("website")
        if website:
            if iata and iata != "-":
                iata_urls[iata.strip().upper()] = website
            if name:
                name_urls[name.strip().lower()] = website

    # Read airlines (1).json (javascript declaration of array)
    airlines_list = parse_js_json(airlines1_path)
    for item in airlines_list:
        name = item.get("name")
        iata = item.get("iata")
        url = item.get("url")
        if url:
            if iata and iata != "-":
                iata_urls[iata.strip().upper()] = url
            if name:
                name_urls[name.strip().lower()] = url

    print(f"Loaded {len(iata_urls)} unique IATA website mapping and {len(name_urls)} name mappings.")

    # 3. Read Airlines from database
    cursor = conn.cursor()
    try:
        cursor.execute("SELECT AirlineId, Name, IataCode FROM Airlines")
        db_airlines = cursor.fetchall()
    except Exception as e:
        print(f"Error reading Airlines table. Make sure migrations are run: {e}")
        conn.close()
        return

    print(f"Found {len(db_airlines)} airlines in the database.")

    # 4. Perform match and updates
    updated_count = 0
    for airline_id, name, db_iata in db_airlines:
        url_to_save = None
        
        # Match by IATA first
        if db_iata:
            iata_upper = db_iata.strip().upper()
            if iata_upper in iata_urls:
                url_to_save = iata_urls[iata_upper]

        # Match by Name fallback
        if not url_to_save and name:
            name_lower = name.strip().lower()
            if name_lower in name_urls:
                url_to_save = name_urls[name_lower]
            else:
                # Partial match fallback
                for key_name, website in name_urls.items():
                    if key_name in name_lower or name_lower in key_name:
                        url_to_save = website
                        break

        # Perform update if url found
        if url_to_save:
            try:
                cursor.execute(
                    "UPDATE Airlines SET Url = ? WHERE AirlineId = ?",
                    (url_to_save, airline_id)
                )
                updated_count += 1
            except Exception as e:
                print(f"Failed to update airline {name} (ID: {airline_id}): {e}")

    conn.commit()
    cursor.close()
    conn.close()

    print(f"Successfully updated {updated_count} airlines with official website URLs in the database!")

if __name__ == "__main__":
    main()
