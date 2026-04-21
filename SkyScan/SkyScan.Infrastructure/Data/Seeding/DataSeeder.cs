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
                await SeedCountriesAsync(context, Path.Combine(basePath, "countries_cleaned.csv"), logger);
                await SeedCitiesAsync(context, Path.Combine(basePath, "cities_cleaned.csv"), logger);
                await SeedAirportsAsync(context, Path.Combine(basePath, "airports_cleaned.csv"), logger);
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
            if (await context.Countries.AnyAsync()) return;

            logger.LogInformation("Seeding Countries...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<Country>().ToList();
            await context.Countries.AddRangeAsync(records);
            await context.SaveChangesAsync();
            logger.LogInformation($"Seeded {records.Count} Countries.");
        }

        private static async Task SeedCitiesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            if (await context.Cities.AnyAsync()) return;

            logger.LogInformation("Seeding Cities...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            csv.Context.RegisterClassMap<CityMap>();
            var records = csv.GetRecords<City>().ToList();
            
            // Check if country codes exist
            var countryCodes = (await context.Countries.Select(c => c.Code).ToListAsync()).ToHashSet();
            var validCities = records.Where(c => countryCodes.Contains(c.CountryCode)).ToList();
            
            await context.Cities.AddRangeAsync(validCities);
            await context.SaveChangesAsync();
            logger.LogInformation($"Seeded {validCities.Count} Cities.");
        }

        private static async Task SeedAirportsAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            if (await context.Airports.AnyAsync()) return;

            logger.LogInformation("Seeding Airports...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<AirportCsvDto>().ToList();
            
            var cities = await context.Cities.Select(c => new { c.CityId, c.Name, c.CountryCode }).ToListAsync();
            // Create a dictionary for super fast lookups
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
                        Name = r.Name,
                        Code = r.Code,
                        IataCode = r.IataCode,
                        IcaoCode = r.IcaoCode,
                        Type = r.Type,
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

            await context.Airports.AddRangeAsync(airportEntities);
            await context.SaveChangesAsync();
            logger.LogInformation($"Seeded {airportEntities.Count} Airports. Skipped {missingCities} due to unmapped cities.");
        }

        private static async Task SeedAirlinesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            if (await context.Airlines.AnyAsync()) return;

            logger.LogInformation("Seeding Airlines...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<Airline>().ToList();
            foreach(var r in records)
            {
                if (r.IcaoCode != null && r.IcaoCode.Length > 10) r.IcaoCode = r.IcaoCode.Substring(0, 10);
                if (r.Callsign != null && r.Callsign.Length > 50) r.Callsign = r.Callsign.Substring(0, 50);
                if (r.Name != null && r.Name.Length > 100) r.Name = r.Name.Substring(0, 100);
            }

            await context.Airlines.AddRangeAsync(records);
            await context.SaveChangesAsync();
            logger.LogInformation($"Seeded {records.Count} Airlines.");
        }

        private static async Task SeedAirplanesAsync(SkyScanDbContext context, string filePath, Microsoft.Extensions.Logging.ILogger logger)
        {
            if (await context.Airplanes.AnyAsync()) return;

            logger.LogInformation("Seeding Airplanes...");
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            csv.Context.RegisterClassMap<AirplaneMap>();
            
            const int batchSize = 10000;
            var batch = new List<Airplane>();
            int total = 0;

            foreach (var r in csv.GetRecords<Airplane>())
            {
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
                    await context.Airplanes.AddRangeAsync(batch);
                    await context.SaveChangesAsync();
                    context.ChangeTracker.Clear();
                    total += batch.Count;
                    batch.Clear();
                    logger.LogInformation($"Seeded {total} Airplanes...");
                }
            }

            if (batch.Any())
            {
                await context.Airplanes.AddRangeAsync(batch);
                await context.SaveChangesAsync();
                total += batch.Count;
            }

            logger.LogInformation($"Successfully seeded total {total} Airplanes.");
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
