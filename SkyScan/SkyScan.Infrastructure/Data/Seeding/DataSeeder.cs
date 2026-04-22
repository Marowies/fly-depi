using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Data.Seeding
{
    public class DataSeeder
    {
        public static async Task SeedDataAsync(IServiceProvider serviceProvider, string basePath)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SkyScanDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataSeeder>>();

            context.ChangeTracker.AutoDetectChangesEnabled = false;

            try
            {
                // await SeedCountriesAsync(context, Path.Combine(basePath, "countries_cleaned.csv"), logger);
                // await SeedCitiesAsync(context, Path.Combine(basePath, "cities_cleaned.csv"), logger);
                // await SeedAirportsAsync(context, Path.Combine(basePath, "airports_cleaned.csv"), logger);
                await SeedAirlinesAsync(context, Path.Combine(basePath, "airlines_cleaned.csv"), logger);
                
                // Seed airplanes in batches because the file is huge
                await SeedAirplanesAsync(context, Path.Combine(basePath, "aircraft_cleaned.csv"), logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
            finally
            {
                context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        private static async Task SeedCountriesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Checking Countries...");
            var existingCodes = await context.Countries.Select(c => c.CountryCode).ToListAsync();
            var existingSet = new HashSet<string>(existingCodes);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<CountryMap>();
            
            var records = csv.GetRecords<Country>()
                .Where(r => !existingSet.Contains(r.CountryCode))
                .ToList();

            if (records.Any())
            {
                foreach (var r in records)
                {
                    if (string.IsNullOrWhiteSpace(r.Name)) r.Name = "Unknown Country";
                    if (string.IsNullOrWhiteSpace(r.Continent)) r.Continent = "Unknown";
                }
                await context.Countries.AddRangeAsync(records);
                await context.SaveChangesAsync();
                logger.LogInformation($"Seeded {records.Count} new Countries.");
            }
            else
            {
                logger.LogInformation("No new Countries to seed.");
            }
        }

        private static async Task SeedCitiesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Checking Cities...");
            var existingCities = await context.Cities.Select(c => new { c.Name, c.CountryCode }).ToListAsync();
            var existingSet = existingCities.Select(c => $"{c.Name.ToLower()}_{c.CountryCode.ToLower()}").ToHashSet();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            csv.Context.RegisterClassMap<CityMap>();
            var records = csv.GetRecords<City>().ToList();
            
            var countryCodes = (await context.Countries.Select(c => c.CountryCode).ToListAsync()).ToHashSet();
            
            var newCities = records
                .Where(c => countryCodes.Contains(c.CountryCode))
                .Where(c => !existingSet.Contains($"{c.Name.ToLower()}_{c.CountryCode.ToLower()}"))
                .ToList();
            
            if (newCities.Any())
            {
                foreach (var c in newCities)
                {
                    if (string.IsNullOrWhiteSpace(c.Name)) c.Name = "Unknown City";
                }
                await context.Cities.AddRangeAsync(newCities);
                await context.SaveChangesAsync();
                logger.LogInformation($"Seeded {newCities.Count} new Cities.");
            }
            else
            {
                logger.LogInformation("No new Cities to seed.");
            }
        }

        private static async Task SeedAirportsAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Checking Airports...");
            var existingIds = await context.Airports.Select(a => a.AirportId).ToListAsync();
            var existingSet = new HashSet<Guid>(existingIds);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<AirportCsvDto>().Where(r => !existingSet.Contains(r.AirportId)).ToList();
            
            if (!records.Any())
            {
                logger.LogInformation("No new Airports to seed.");
                return;
            }

            var cities = await context.Cities.Select(c => new { c.CityId, c.Name, c.CountryCode }).ToListAsync();
            var cityDict = cities.GroupBy(c => $"{c.Name.ToLower()}_{c.CountryCode.ToLower()}")
                                 .ToDictionary(g => g.Key, g => g.First().CityId);

            var airportEntities = new List<Airport>();
            int missingCities = 0;

            foreach (var r in records)
            {
                var dictKey = $"{r.Municipality?.ToLower()}_{r.CountryCode?.ToLower()}";
                if (cityDict.TryGetValue(dictKey, out Guid cityId))
                {
                    airportEntities.Add(new Airport
                    {
                        AirportId = r.AirportId,
                        Name = string.IsNullOrWhiteSpace(r.Name) ? "Unknown Airport" : r.Name,
                        Code = r.Code,
                        IataCode = r.IataCode,
                        IcaoCode = r.IcaoCode,
                        Type = string.IsNullOrWhiteSpace(r.Type) ? "Unknown" : r.Type,
                        Latitude = r.Latitude,
                        Longitude = r.Longitude,
                        ElevationFt = r.ElevationFt,
                        CityId = cityId
                    });
                }
                else
                {
                    missingCities++;
                }
            }

            if (airportEntities.Any())
            {
                await context.Airports.AddRangeAsync(airportEntities);
                await context.SaveChangesAsync();
                logger.LogInformation($"Seeded {airportEntities.Count} new Airports. Skipped {missingCities} due to unmapped cities.");
            }
        }

        private static async Task SeedAirlinesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Checking Airlines...");
            var existingNames = await context.Airlines.Select(a => a.Name).ToListAsync();
            var existingSet = new HashSet<string>(existingNames.Select(n => n.ToLower()));

            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<Airline>().ToList();
            var newAirlines = new List<Airline>();

            foreach(var r in records)
            {
                if (existingSet.Contains(r.Name.ToLower())) continue;

                if (string.IsNullOrWhiteSpace(r.HotlineNumber)) r.HotlineNumber = "N/A";
                if (r.IcaoCode != null && r.IcaoCode.Length > 10) r.IcaoCode = r.IcaoCode.Substring(0, 10);
                if (r.Callsign != null && r.Callsign.Length > 50) r.Callsign = r.Callsign.Substring(0, 50);
                if (r.Name != null && r.Name.Length > 100) r.Name = r.Name.Substring(0, 100);

                newAirlines.Add(r);
                existingSet.Add(r.Name.ToLower());
            }

            if (newAirlines.Any())
            {
                await context.Airlines.AddRangeAsync(newAirlines);
                await context.SaveChangesAsync();
                logger.LogInformation($"Seeded {newAirlines.Count} new Airlines.");
            }
            else
            {
                logger.LogInformation("No new Airlines to seed.");
            }
        }

        private static async Task SeedAirplanesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            logger.LogInformation("Checking Airplanes...");
            // For large datasets, we check existence batch by batch to avoid loading millions of IDs into memory
            
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                HasHeaderRecord = true,
                HeaderValidated = null,
                MissingFieldFound = null,
                PrepareHeaderForMatch = args => args.Header.Trim()
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            csv.Context.RegisterClassMap<AirplaneMap>();
            
            const int batchSize = 10000;
            var batch = new List<Airplane>();
            int total = 0;

            foreach (var r in csv.GetRecords<Airplane>())
            {
                // Basic cleanup
                if (r.Icao24 != null && r.Icao24.Length > 20) r.Icao24 = r.Icao24.Substring(0, 20);
                if (r.Registration != null && r.Registration.Length > 20) r.Registration = r.Registration.Substring(0, 20);
                if (r.Model != null && r.Model.Length > 100) r.Model = r.Model.Substring(0, 100);
                if (r.ManufactureCompany != null && r.ManufactureCompany.Length > 100) r.ManufactureCompany = r.ManufactureCompany.Substring(0, 100);
                if (r.OwnerCompany != null && r.OwnerCompany.Length > 100) r.OwnerCompany = r.OwnerCompany.Substring(0, 100);
                if (r.PlaneId != null && r.PlaneId.Length > 50) r.PlaneId = r.PlaneId.Substring(0, 50);
                if (r.SerialNumber != null && r.SerialNumber.Length > 100) r.SerialNumber = r.SerialNumber.Substring(0, 100);
                if (r.EngineType != null && r.EngineType.Length > 50) r.EngineType = r.EngineType.Substring(0, 50);
                if (r.Status != null && r.Status.Length > 20) r.Status = r.Status.Substring(0, 20);

                batch.Add(r);

                if (batch.Count >= batchSize)
                {
                    // Check which ones in this batch already exist
                    var batchIds = batch.Select(b => b.AirplaneId).ToList();
                    var existingIds = await context.Airplanes.Where(a => batchIds.Contains(a.AirplaneId)).Select(a => a.AirplaneId).ToListAsync();
                    var existingSet = new HashSet<Guid>(existingIds);

                    var newInBatch = batch.Where(b => !existingSet.Contains(b.AirplaneId)).ToList();

                    if (newInBatch.Any())
                    {
                        await context.Airplanes.AddRangeAsync(newInBatch);
                        await context.SaveChangesAsync();
                        total += newInBatch.Count;
                    }
                    
                    context.ChangeTracker.Clear();
                    batch.Clear();
                    logger.LogInformation($"Processed a batch. Seeded {total} new Airplanes so far...");
                }
            }

            if (batch.Any())
            {
                var batchIds = batch.Select(b => b.AirplaneId).ToList();
                var existingIds = await context.Airplanes.Where(a => batchIds.Contains(a.AirplaneId)).Select(a => a.AirplaneId).ToListAsync();
                var existingSet = new HashSet<Guid>(existingIds);
                var newInBatch = batch.Where(b => !existingSet.Contains(b.AirplaneId)).ToList();

                if (newInBatch.Any())
                {
                    await context.Airplanes.AddRangeAsync(newInBatch);
                    await context.SaveChangesAsync();
                    total += newInBatch.Count;
                }
            }

            logger.LogInformation($"Successfully seeded total {total} new Airplanes.");
        }
    }

    public sealed class CountryMap : ClassMap<Country>
    {
        public CountryMap()
        {
            Map(m => m.CountryCode).Name("Code");
            Map(m => m.Name).Name("Name");
            Map(m => m.Continent).Name("Continent");
        }
    }

    public sealed class CityMap : ClassMap<City>
    {
        public CityMap()
        {
            Map(m => m.CityId).Name("CityId");
            Map(m => m.Name).Name("Name");
            Map(m => m.CountryCode).Name("Country");
        }
    }

    public sealed class AirplaneMap : ClassMap<Airplane>
    {
        public AirplaneMap()
        {
            Map(m => m.AirplaneId).Name("AirplaneId");
            Map(m => m.Icao24).Name("Icao24");
            Map(m => m.Registration).Name("Registration");
            Map(m => m.Model).Name("Model");
            Map(m => m.ManufactureCompany).Name("ManufactureCompany");
            Map(m => m.OwnerCompany).Name("OwnerCompany");
            
            Map(m => m.ManufactureDate).Name("ManufactureDate").TypeConverterOption.Format("yyyy-MM-dd").TypeConverterOption.NullValues("");
            Map(m => m.StartDate).Name("StartDate").TypeConverterOption.Format("yyyy-MM-dd").TypeConverterOption.NullValues("");
            Map(m => m.EndDate).Name("EndDate").TypeConverterOption.Format("yyyy-MM-dd").TypeConverterOption.NullValues("");
            
            Map(m => m.PlaneId).Name("PlaneId");
            
            Map(m => m.Seats).Name("Seats");
            Map(m => m.SerialNumber).Name("SerialNumber");
            Map(m => m.EngineType).Name("EngineType");
            Map(m => m.Status).Name("Status");
        }
    }

    public class AirportCsvDto
    {
        public Guid AirportId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string IataCode { get; set; }
        public string IcaoCode { get; set; }
        public string Type { get; set; }
        public string Municipality { get; set; }
        public string CountryCode { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? ElevationFt { get; set; }
    }
}
