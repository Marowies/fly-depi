using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using SkyScan.Core.Entities;
using SkyScan.Core.Entities.AirLine;
using Microsoft.EntityFrameworkCore;
using SkyScan.Infrastructure.Data.Data_Sources;

namespace SkyScan.Infrastructure.Services
{
    public class AmadeusFlightService : IFlightProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly SkyScanDbContext _dbContext;
        private readonly ILogger<AmadeusFlightService> _logger;
        private string? _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public AmadeusFlightService(HttpClient httpClient, IConfiguration configuration, SkyScanDbContext dbContext, ILogger<AmadeusFlightService> logger)
        {
            _httpClient = httpClient;
            _clientId = configuration["Amadeus:ClientId"] ?? "";
            _clientSecret = configuration["Amadeus:ClientSecret"] ?? "";
            _dbContext = dbContext;
            _logger = logger;

            var baseUrl = configuration["Amadeus:BaseUrl"] ?? "https://test.api.amadeus.com/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        private async Task EnsureAccessTokenAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiration)
            {
                return;
            }

            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret)
            });

            var response = await _httpClient.PostAsync("v1/security/oauth2/token", requestContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve Amadeus access token.");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 10);
        }

        public async Task<string?> GetAirlineNameAsync(string iataCode)
        {
            await EnsureAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var url = $"v1/reference-data/airlines?airlineCodes={iataCode}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in dataArray.EnumerateArray())
                    {
                        var responseIata = element.TryGetProperty("iataCode", out var iataEl) ? iataEl.GetString() : null;
                        if (string.Equals(responseIata, iataCode, StringComparison.OrdinalIgnoreCase))
                        {
                            if (element.TryGetProperty("commonName", out var commonNameEl) && !string.IsNullOrWhiteSpace(commonNameEl.GetString()))
                            {
                                return commonNameEl.GetString();
                            }
                            if (element.TryGetProperty("businessName", out var businessNameEl) && !string.IsNullOrWhiteSpace(businessNameEl.GetString()))
                            {
                                return businessNameEl.GetString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving airline name for IATA code {IataCode}", iataCode);
            }

            return null;
        }

        public async Task<IEnumerable<FlightDto>> SearchFlightsAsync(
            IEnumerable<string> originIatas, 
            IEnumerable<string> destinationIatas, 
            DateTime departureDate, 
            DateTime? returnDate = null)
        {
            var flightDtos = new List<FlightDto>();
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            // In-memory cache to prevent redundant DB/API lookup overhead for recurring carriers in the same query
            var localAirlineCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var origin in originIatas)
            {
                foreach (var destination in destinationIatas)
                {
                    var dateStr = departureDate.ToString("yyyy-MM-dd");
                    var url = $"v2/shopping/flight-offers?originLocationCode={origin}&destinationLocationCode={destination}&departureDate={dateStr}";
                    
                    if (returnDate.HasValue)
                    {
                        url += $"&returnDate={returnDate.Value.ToString("yyyy-MM-dd")}";
                    }
                    
                    url += "&adults=1&max=10";

                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode) continue;

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (!doc.RootElement.TryGetProperty("data", out var dataArray)) continue;

                        foreach (var offer in dataArray.EnumerateArray())
                        {
                            var price = decimal.Parse(offer.GetProperty("price").GetProperty("grandTotal").GetString() ?? "0.00");
                            
                            if (!offer.TryGetProperty("itineraries", out var itinerariesEl) || itinerariesEl.ValueKind != JsonValueKind.Array)
                            {
                                continue;
                            }

                            var itineraries = itinerariesEl.EnumerateArray().ToList();
                            if (!itineraries.Any()) continue;

                            // 1. Outbound Leg
                            var outboundItinerary = itineraries[0];
                            var outboundSegments = outboundItinerary.GetProperty("segments").EnumerateArray().ToList();
                            if (!outboundSegments.Any()) continue;

                            var outboundFirstSegment = outboundSegments.First();
                            var outboundLastSegment = outboundSegments.Last();

                            var outboundCarrierCode = outboundFirstSegment.GetProperty("carrierCode").GetString() ?? "XX";
                            var outboundFlightNum = outboundFirstSegment.GetProperty("number").GetString() ?? "000";
                            var outboundDepartureTime = DateTime.Parse(outboundFirstSegment.GetProperty("departure").GetProperty("at").GetString() ?? DateTime.Now.ToString());
                            var outboundArrivalTime = DateTime.Parse(outboundLastSegment.GetProperty("arrival").GetProperty("at").GetString() ?? DateTime.Now.ToString());

                            var outboundAirlineName = await ResolveAirlineNameWithCacheAsync(outboundCarrierCode, localAirlineCache);

                            var outboundAircraftCode = outboundFirstSegment.TryGetProperty("aircraft", out var outboundAcEl)
                                && outboundAcEl.TryGetProperty("code", out var outboundAcCodeEl)
                                ? outboundAcCodeEl.GetString() ?? "UNK"
                                : "UNK";
                            
                            var outboundAirplane = await ResolveAirplaneAsync(outboundAircraftCode);
                            var outboundDepAirport = await ResolveAirportAsync(origin);
                            var outboundArrAirport = await ResolveAirportAsync(destination);

                            // 2. Return Leg (Native Round-Trip)
                            FlightDto? returnFlightDto = null;
                            if (itineraries.Count > 1 && returnDate.HasValue)
                            {
                                var returnItinerary = itineraries[1];
                                var returnSegments = returnItinerary.GetProperty("segments").EnumerateArray().ToList();
                                if (returnSegments.Any())
                                {
                                    var returnFirstSegment = returnSegments.First();
                                    var returnLastSegment = returnSegments.Last();

                                    var returnCarrierCode = returnFirstSegment.GetProperty("carrierCode").GetString() ?? "XX";
                                    var returnFlightNum = returnFirstSegment.GetProperty("number").GetString() ?? "000";
                                    var returnDepartureTime = DateTime.Parse(returnFirstSegment.GetProperty("departure").GetProperty("at").GetString() ?? DateTime.Now.ToString());
                                    var returnArrivalTime = DateTime.Parse(returnLastSegment.GetProperty("arrival").GetProperty("at").GetString() ?? DateTime.Now.ToString());

                                    var returnAirlineName = await ResolveAirlineNameWithCacheAsync(returnCarrierCode, localAirlineCache);

                                    var returnAircraftCode = returnFirstSegment.TryGetProperty("aircraft", out var returnAcEl)
                                        && returnAcEl.TryGetProperty("code", out var returnAcCodeEl)
                                        ? returnAcCodeEl.GetString() ?? "UNK"
                                        : "UNK";

                                    var returnAirplane = await ResolveAirplaneAsync(returnAircraftCode);
                                    var returnDepAirport = await ResolveAirportAsync(destination);
                                    var returnArrAirport = await ResolveAirportAsync(origin);

                                    if (returnDepAirport != null && returnArrAirport != null)
                                    {
                                        var returnRedirectUrl = $"https://www.google.com/travel/flights?q=Flights%20to%20{origin}%20from%20{destination}%20on%20{returnDate.Value:yyyy-MM-dd}";

                                        var returnAmenities = GetAmenities(returnCarrierCode, returnAircraftCode);

                                        returnFlightDto = new FlightDto
                                        {
                                            AirlineName = returnAirlineName,
                                            FlightNumber = $"{returnCarrierCode} {returnFlightNum}",
                                            OriginAirport = $"{returnDepAirport.Name} ({returnDepAirport.IataCode})",
                                            DestinationAirport = $"{returnArrAirport.Name} ({returnArrAirport.IataCode})",
                                            DepartureTime = returnDepartureTime,
                                            ArrivalTime = returnArrivalTime,
                                            Price = 0.00M, // Indicated as "Included" in the view as price is bundled
                                            Stops = Math.Max(0, returnSegments.Count - 1),
                                            Status = "Active",
                                            RedirectURL = returnRedirectUrl,
                                            HasWifi = returnAmenities.Contains("WiFi"),
                                            HasFood = returnAmenities.Contains("Meals"),
                                            HasEntertainment = returnAmenities.Contains("Entertainment"),
                                            HasPower = returnAmenities.Contains("Power")
                                        };
                                    }
                                }
                            }

                            if (outboundDepAirport != null && outboundArrAirport != null)
                            {
                                var outboundRedirectUrl = $"https://www.google.com/travel/flights?q=Flights%20to%20{destination}%20from%20{origin}%20on%20{dateStr}";

                                var outboundAmenities = GetAmenities(outboundCarrierCode, outboundAircraftCode);

                                flightDtos.Add(new FlightDto
                                {
                                    AirlineName = outboundAirlineName,
                                    FlightNumber = $"{outboundCarrierCode} {outboundFlightNum}",
                                    OriginAirport = $"{outboundDepAirport.Name} ({outboundDepAirport.IataCode})",
                                    DestinationAirport = $"{outboundArrAirport.Name} ({outboundArrAirport.IataCode})",
                                    DepartureTime = outboundDepartureTime,
                                    ArrivalTime = outboundArrivalTime,
                                    Price = price, // Show total combined fare on outbound
                                    Stops = Math.Max(0, outboundSegments.Count - 1),
                                    Status = "Active",
                                    RedirectURL = outboundRedirectUrl,
                                    ReturnLeg = returnFlightDto,
                                    HasWifi = outboundAmenities.Contains("WiFi"),
                                    HasFood = outboundAmenities.Contains("Meals"),
                                    HasEntertainment = outboundAmenities.Contains("Entertainment"),
                                    HasPower = outboundAmenities.Contains("Power")
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error querying flight from {Origin} to {Destination}", origin, destination);
                    }
                }
            }

            return flightDtos;
        }

        private async Task<string> ResolveAirlineNameWithCacheAsync(string carrierCode, Dictionary<string, string> localCache)
        {
            if (localCache.TryGetValue(carrierCode, out var cachedName))
            {
                return cachedName;
            }

            // 1. Check DB first (Low Cost)
            var airline = await _dbContext.Airlines.FirstOrDefaultAsync(a => a.IataCode == carrierCode);
            if (airline != null)
            {
                localCache[carrierCode] = airline.Name;
                return airline.Name;
            }

            // 2. Query Amadeus Airline Code Lookup API
            string resolvedName = carrierCode + " Airlines"; // fallback
            var apiName = await GetAirlineNameAsync(carrierCode);
            if (!string.IsNullOrEmpty(apiName))
            {
                resolvedName = apiName;
            }

            // 3. Store in DB
            try
            {
                airline = new Airline
                {
                    AirlineId = Guid.NewGuid(),
                    Name = resolvedName,
                    IataCode = carrierCode
                };
                _dbContext.Airlines.Add(airline);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving airline {CarrierCode} ({ResolvedName}) to DB", carrierCode, resolvedName);
            }

            localCache[carrierCode] = resolvedName;
            return resolvedName;
        }

        private async Task<Airplane> ResolveAirplaneAsync(string aircraftCode)
        {
            var airplane = await _dbContext.Airplanes.FirstOrDefaultAsync(a => a.AircraftCode == aircraftCode);
            if (airplane == null)
            {
                try
                {
                    airplane = new Airplane
                    {
                        AirplaneId = Guid.NewGuid(),
                        AircraftCode = aircraftCode,
                        AircraftName = AircraftNameLookup.GetName(aircraftCode)
                    };
                    _dbContext.Airplanes.Add(airplane);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving airplane {AircraftCode} to DB", aircraftCode);
                }
            }
            return airplane ?? new Airplane { AircraftCode = aircraftCode, AircraftName = AircraftNameLookup.GetName(aircraftCode) };
        }

        private async Task<Airport?> ResolveAirportAsync(string iataCode)
        {
            return await _dbContext.Airports
                .Include(a => a.City)
                    .ThenInclude(c => c.Country)
                .FirstOrDefaultAsync(a => a.IataCode == iataCode);
        }

        private List<string> GetAmenities(string carrierCode, string aircraftCode)
        {
            var amenities = new List<string>();
            var budgetCarriers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "FR", "W6", "U2", "NK", "F9", "VY", "TO", "HV", "PC", "W3", "DY", "D8", "D7"
            };
            var isBudget = budgetCarriers.Contains(carrierCode);

            var hasWiFi = !isBudget;
            if (hasWiFi) amenities.Add("WiFi");

            if (!isBudget)
            {
                amenities.Add("Meals");
            }

            var hasPower = !isBudget;
            if (hasPower) amenities.Add("Power");

            var hasEntertainment = !isBudget;
            if (hasEntertainment) amenities.Add("Entertainment");

            if (amenities.Count == 0)
            {
                amenities.Add("Power");
            }
            return amenities;
        }
    }

    /// <summary>
    /// Resolves ICAO aircraft type codes (as returned by Amadeus) to human-readable names.
    /// </summary>
    public static class AircraftNameLookup
    {
        private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            // Boeing
            { "73H", "Boeing 737-800" }, { "738", "Boeing 737-800" }, { "737", "Boeing 737" },
            { "739", "Boeing 737-900" }, { "73W", "Boeing 737-700" }, { "73X", "Boeing 737-900ER" },
            { "7M8", "Boeing 737 MAX 8" }, { "7M9", "Boeing 737 MAX 9" },
            { "744", "Boeing 747-400" }, { "74H", "Boeing 747-8" }, { "747", "Boeing 747" },
            { "757", "Boeing 757" }, { "75W", "Boeing 757-200" },
            { "763", "Boeing 767-300" }, { "764", "Boeing 767-400" }, { "767", "Boeing 767" },
            { "772", "Boeing 777-200" }, { "77W", "Boeing 777-300ER" }, { "773", "Boeing 777-300" },
            { "778", "Boeing 777X-8" }, { "779", "Boeing 777X-9" },
            { "788", "Boeing 787-8 Dreamliner" }, { "789", "Boeing 787-9 Dreamliner" }, { "78X", "Boeing 787-10 Dreamliner" },
            // Airbus
            { "319", "Airbus A319" }, { "320", "Airbus A320" }, { "321", "Airbus A321" },
            { "32A", "Airbus A320neo" }, { "32B", "Airbus A321neo" }, { "32Q", "Airbus A321XLR" },
            { "330", "Airbus A330" }, { "332", "Airbus A330-200" }, { "333", "Airbus A330-300" },
            { "338", "Airbus A330-800neo" }, { "339", "Airbus A330-900neo" },
            { "340", "Airbus A340" }, { "342", "Airbus A340-200" }, { "343", "Airbus A340-300" },
            { "380", "Airbus A380" }, { "388", "Airbus A380-800" },
            { "351", "Airbus A350-900" }, { "359", "Airbus A350-900" }, { "35K", "Airbus A350-1000" },
            // Embraer
            { "E70", "Embraer E170" }, { "E75", "Embraer E175" }, { "E90", "Embraer E190" }, { "E95", "Embraer E195" },
            { "290", "Embraer E290" }, { "295", "Embraer E295" },
            // Bombardier
            { "CR2", "Bombardier CRJ-200" }, { "CR7", "Bombardier CRJ-700" }, { "CR9", "Bombardier CRJ-900" },
            { "CRK", "Bombardier CRJ-1000" }, { "DH4", "De Havilland Canada Q400" },
            // ATR
            { "AT4", "ATR 42" }, { "AT7", "ATR 72" },
        };

        public static string GetName(string code) =>
            _map.TryGetValue(code, out var name) ? name : $"Aircraft ({code})";
    }
}
