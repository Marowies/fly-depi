using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider;
        private string? _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public AmadeusFlightService(HttpClient httpClient, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _clientId = configuration["Amadeus:ClientId"] ?? "";
            _clientSecret = configuration["Amadeus:ClientSecret"] ?? "";
            _serviceProvider = serviceProvider;
            
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

        public async Task<IEnumerable<FlightDto>> SearchFlightsAsync(IEnumerable<string> originIatas, IEnumerable<string> destinationIatas, DateTime departureDate)
        {
            var flightDtos = new List<FlightDto>();
            await EnsureAccessTokenAsync();

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            foreach (var origin in originIatas)
            {
                foreach (var destination in destinationIatas)
                {
                    var dateStr = departureDate.ToString("yyyy-MM-dd");
                    var url = $"v2/shopping/flight-offers?originLocationCode={origin}&destinationLocationCode={destination}&departureDate={dateStr}&adults=1&max=10";

                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode) continue;

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (!doc.RootElement.TryGetProperty("data", out var dataArray)) continue;

                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<SkyScanDbContext>();

                        foreach (var offer in dataArray.EnumerateArray())
                        {
                            var price = decimal.Parse(offer.GetProperty("price").GetProperty("grandTotal").GetString() ?? "0.00");
                            var firstItinerary = offer.GetProperty("itineraries").EnumerateArray().First();
                            var segments = firstItinerary.GetProperty("segments").EnumerateArray().ToList();

                            if (!segments.Any()) continue;

                            var firstSegment = segments.First();
                            var lastSegment = segments.Last();

                            var carrierCode = firstSegment.GetProperty("carrierCode").GetString() ?? "XX";
                            var flightNum = firstSegment.GetProperty("number").GetString() ?? "000";
                            var departureTime = DateTime.Parse(firstSegment.GetProperty("departure").GetProperty("at").GetString() ?? DateTime.Now.ToString());
                            var arrivalTime = DateTime.Parse(lastSegment.GetProperty("arrival").GetProperty("at").GetString() ?? DateTime.Now.ToString());

                            // Find or Create Airline in local DB
                            var airline = await dbContext.Airlines.FirstOrDefaultAsync(a => a.IataCode == carrierCode);
                            if (airline == null)
                            {
                                airline = new Airline 
                                { 
                                    AirlineId = Guid.NewGuid(), 
                                    Name = carrierCode + " Airlines", 
                                    IataCode = carrierCode
                                };
                                dbContext.Airlines.Add(airline);
                                await dbContext.SaveChangesAsync();
                            }

                            // Find or Create Airplane in local DB using aircraft type code from Amadeus
                            var aircraftCode = firstSegment.TryGetProperty("aircraft", out var acEl)
                                && acEl.TryGetProperty("code", out var acCodeEl)
                                ? acCodeEl.GetString() ?? "UNK"
                                : "UNK";
                            var airplane = await dbContext.Airplanes
                                .FirstOrDefaultAsync(a => a.AircraftCode == aircraftCode);
                            if (airplane == null)
                            {
                                airplane = new Airplane 
                                { 
                                    AirplaneId = Guid.NewGuid(),
                                    AircraftCode = aircraftCode,
                                    AircraftName = AircraftNameLookup.GetName(aircraftCode)
                                };
                                dbContext.Airplanes.Add(airplane);
                                await dbContext.SaveChangesAsync();
                            }

                            // Find departure and arrival airport
                            var depAirport = await dbContext.Airports
                                .Include(a => a.City)
                                    .ThenInclude(c => c.Country)
                                .FirstOrDefaultAsync(a => a.IataCode == origin);
                            var arrAirport = await dbContext.Airports
                                .Include(a => a.City)
                                    .ThenInclude(c => c.Country)
                                .FirstOrDefaultAsync(a => a.IataCode == destination);

                            if (depAirport == null || arrAirport == null) continue;

                            // Dynamic Google Flights redirection link
                            var redirectUrl = $"https://www.google.com/travel/flights?q=Flights%20to%20{destination}%20from%20{origin}%20on%20{dateStr}";

                            // Persist to local DB if not exists
                            var flight = await dbContext.Flights
                                .Include(f => f.Tickets)
                                .FirstOrDefaultAsync(f => f.FlightNumber == $"{carrierCode} {flightNum}" && f.DepartureTime == departureTime);

                            if (flight == null)
                            {
                                flight = new Flight
                                {
                                    FlightId = Guid.NewGuid(),
                                    AirlineId = airline.AirlineId,
                                    AirplaneId = airplane.AirplaneId,
                                    FlightNumber = $"{carrierCode} {flightNum}",
                                    DepartureAirportId = depAirport.AirportId,
                                    ArrivalAirportId = arrAirport.AirportId,
                                    DepartureTime = departureTime,
                                    ArrivalTime = arrivalTime,
                                    RedirectURL = redirectUrl
                                };

                                dbContext.Flights.Add(flight);
                                await dbContext.SaveChangesAsync();

                                // Add a Ticket
                                var ticket = new Ticket
                                {
                                    TicketId = Guid.NewGuid(),
                                    Price = price,
                                    CabinClass = Core.Constants.CabinType.Economy,
                                    FlightId = flight.FlightId,
                                    HasFood = true,
                                    HasWifi = true
                                };
                                dbContext.Tickets.Add(ticket);
                                await dbContext.SaveChangesAsync();
                            }

                            flightDtos.Add(new FlightDto
                            {
                                AirlineName = airline.Name,
                                FlightNumber = flight.FlightNumber,
                                OriginAirport = depAirport != null ? $"{depAirport.Name} ({depAirport.IataCode})" : origin,
                                DestinationAirport = arrAirport != null ? $"{arrAirport.Name} ({arrAirport.IataCode})" : destination,
                                DepartureTime = departureTime,
                                ArrivalTime = arrivalTime,
                                Price = price,
                                Stops = Math.Max(0, segments.Count - 1),
                                Status = "Active",
                                RedirectURL = redirectUrl
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error querying flight from {origin} to {destination}: {ex.Message}");
                    }
                }
            }

            return flightDtos;
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
