import sys
import os
import time

# Force UTF-8 encoding for standard output on Windows to prevent console encoding crashes
if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except AttributeError:
        pass

# Auto-install dependencies if not present
try:
    import pyodbc
except ImportError:
    print("Installing pyodbc...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pyodbc"])
    import pyodbc

try:
    from googletrans import Translator
except ImportError:
    print("Installing googletrans (version 4.0.0-rc1)...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "googletrans==4.0.0-rc1"])
    from googletrans import Translator

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

def add_columns_if_missing(cursor):
    print("Checking database columns...")
    
    # Check and add Cities.NameAr
    try:
        cursor.execute("""
            IF NOT EXISTS (
                SELECT * FROM sys.columns 
                WHERE object_id = OBJECT_ID('Cities') AND name = 'NameAr'
            )
            BEGIN
                ALTER TABLE Cities ADD NameAr NVARCHAR(100) NULL;
            END
        """)
        print("Cities.NameAr column verified/added successfully.")
    except Exception as e:
        print(f"Error checking/adding Cities.NameAr: {e}")

    # Check and add Countries.NameAr
    try:
        cursor.execute("""
            IF NOT EXISTS (
                SELECT * FROM sys.columns 
                WHERE object_id = OBJECT_ID('Countries') AND name = 'NameAr'
            )
            BEGIN
                ALTER TABLE Countries ADD NameAr NVARCHAR(100) NULL;
            END
        """)
        print("Countries.NameAr column verified/added successfully.")
    except Exception as e:
        print(f"Error checking/adding Countries.NameAr: {e}")

def main():
    print("Connecting to SQL Server...")
    try:
        conn = pyodbc.connect(CONNECTION_STRING, timeout=30)
        cursor = conn.cursor()
        print("Successfully connected to the database!")
    except Exception as e:
        print(f"Error: Could not connect to database: {e}")
        return

    # 1. Add columns to database if they don't exist
    add_columns_if_missing(cursor)
    conn.commit()

    # 2. Fetch cities that need translation
    cursor.execute("SELECT CityId, Name FROM Cities WHERE NameAr IS NULL")
    cities = cursor.fetchall()
    
    # 3. Fetch countries that need translation
    cursor.execute("SELECT CountryCode, Name FROM Countries WHERE NameAr IS NULL")
    countries = cursor.fetchall()

    print(f"Found {len(cities)} cities and {len(countries)} countries to translate.")

    if not cities and not countries:
        print("All records are already translated. Nothing to do!")
        conn.close()
        return

    translator = Translator()

    # Translate & Update Countries
    if countries:
        print("\nTranslating Countries...")
        updated_countries = 0
        for code, name in countries:
            if not name or name.strip() == "":
                continue
            try:
                translation = translator.translate(name, src='en', dest='ar').text
                if translation:
                    cursor.execute(
                        "UPDATE Countries SET NameAr = ? WHERE CountryCode = ?",
                        (translation.strip(), code)
                    )
                    updated_countries += 1
                    print(f"Translated Country: {name} -> {translation}")
                time.sleep(0.3)  # Rate limiting safety delay
            except Exception as e:
                print(f"Error translating Country {name}: {e}")
                time.sleep(1.0) # Longer sleep on error
        
        conn.commit()
        print(f"Successfully translated and updated {updated_countries} countries in the DB.")

    # Translate & Update Cities
    if cities:
        BATCH_SIZE = 50
        print(f"\nTranslating {len(cities)} Cities in batches of {BATCH_SIZE}...")
        updated_cities = 0
        
        for i in range(0, len(cities), BATCH_SIZE):
            batch = cities[i:i + BATCH_SIZE]
            print(f"\n--- Processing batch {i//BATCH_SIZE + 1} of {(len(cities) + BATCH_SIZE - 1)//BATCH_SIZE} ---")
            
            for city_id, name in batch:
                if not name or name.strip() == "":
                    continue
                try:
                    translation = translator.translate(name, src='en', dest='ar').text
                    if translation:
                        cursor.execute(
                            "UPDATE Cities SET NameAr = ? WHERE CityId = ?",
                            (translation.strip(), city_id)
                        )
                        updated_cities += 1
                        print(f"Translated City: {name} -> {translation}")
                    time.sleep(0.3)  # Rate limiting safety delay
                except Exception as e:
                    print(f"Error translating City {name}: {e}")
                    time.sleep(2.0) # Longer sleep on error
            
            conn.commit()
            print(f"*** Committed batch {i//BATCH_SIZE + 1}. Total cities updated so far: {updated_cities} ***")
            time.sleep(1.0) # Small pause between batches
        
        print(f"\nSuccessfully translated and updated {updated_cities} cities in the DB.")

    cursor.close()
    conn.close()
    print("\nDatabase translation processing completed successfully!")

if __name__ == "__main__":
    main()
