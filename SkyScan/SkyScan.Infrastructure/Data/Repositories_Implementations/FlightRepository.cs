using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class FlightRepository : GenericRepository<Flight>, IFlightRepository
    {
        public FlightRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Flight>> SearchFlightsAsync(IEnumerable<string> originIatas, IEnumerable<string> destinationIatas, DateTime departureDate)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport)
                .Include(f => f.ArrivalAirport)
                .Include(f => f.Tickets)
                .Where(f => originIatas.Contains(f.DepartureAirport.IataCode) 
                         && destinationIatas.Contains(f.ArrivalAirport.IataCode) 
                         && f.DepartureTime.Date == departureDate.Date)
                .AsNoTracking()
                .ToListAsync();
        }

        // Retrieves the flights with the lowest ticket prices, limited to a specified count.
        // Probably not refrenced in the current codebase, but could be useful for a "best deals" feature.
        // Could be deleted if not used, but might be useful for future features.
        public async Task<IEnumerable<Flight>> GetLowestPriceFlightsAsync(int count = 5)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .Include(f => f.Tickets)
                .OrderBy(f => f.Tickets.Min(t => t.Price))
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        // Finds flights that are randomly selected from the database, simulating a "flights around the world" feature.
        // probably not referenced in the current codebase, but could be useful for a "travel inspiration" feature.
        // Could be deleted if not used, but might be useful for future features.
        public async Task<IEnumerable<Flight>> GetFlightsAroundTheWorldAsync(int count = 5)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .OrderBy(x => Guid.NewGuid()) // Random selection
                .Take(count)
                .ToListAsync();
        }

        public async Task<Flight?> GetByFlightNumberAndDepartureAsync(string flightNumber, DateTime departureTime)
        {
            return await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber && f.DepartureTime == departureTime);
        }

        public async Task<Flight?> EnsureFlightExistsAsync(
            string flightNumber, 
            DateTime departureTime, 
            string originIata, 
            string destinationIata, 
            string airlineName, 
            DateTime arrivalTime, 
            string redirectUrl,
            decimal price = 0.00M,
            bool hasWifi = false,
            bool hasFood = false,
            bool hasEntertainment = false)
        {
            var existingFlight = await _dbSet
                .Include(f => f.Airline)
                .Include(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .Include(f => f.Tickets)
                .FirstOrDefaultAsync(f => f.FlightNumber == flightNumber && f.DepartureTime == departureTime);

            if (existingFlight != null)
            {
                return existingFlight;
            }

            var depAirport = await _context.Airports.Include(a => a.City).FirstOrDefaultAsync(a => a.IataCode == originIata);
            var arrAirport = await _context.Airports.Include(a => a.City).FirstOrDefaultAsync(a => a.IataCode == destinationIata);
            if (depAirport == null || arrAirport == null) return null;

            var airline = await _context.Airlines.FirstOrDefaultAsync(a => a.Name == airlineName);
            if (airline == null)
            {
                airline = new Airline 
                { 
                    AirlineId = Guid.NewGuid(), 
                    Name = airlineName, 
                    IataCode = flightNumber.Substring(0, Math.Min(2, flightNumber.Length)) 
                };
                await _context.Airlines.AddAsync(airline);
            }

            var airplane = await _context.Airplanes.FirstOrDefaultAsync(a => a.AircraftCode == "UNK");
            if (airplane == null)
            {
                airplane = new Airplane 
                { 
                    AirplaneId = Guid.NewGuid(), 
                    AircraftCode = "UNK", 
                    AircraftName = "Unspecified" 
                };
                await _context.Airplanes.AddAsync(airplane);
            }

            var flight = new Flight
            {
                FlightId = Guid.NewGuid(),
                AirlineId = airline.AirlineId,
                AirplaneId = airplane.AirplaneId,
                FlightNumber = flightNumber,
                DepartureAirportId = depAirport.AirportId,
                ArrivalAirportId = arrAirport.AirportId,
                DepartureTime = departureTime,
                ArrivalTime = arrivalTime,
                RedirectURL = string.IsNullOrWhiteSpace(redirectUrl) ? "https://www.google.com/travel/flights" : redirectUrl
            };

            await _dbSet.AddAsync(flight);
            await _context.SaveChangesAsync();

            // Create Ticket associated with the Flight to save amenities
            var ticketPrice = price > 0 ? price : 150.00M;
            var ticket = new Ticket
            {
                TicketId = Guid.NewGuid(),
                FlightId = flight.FlightId,
                Price = ticketPrice,
                Currency = "USD",
                CabinClass = Core.Constants.CabinType.Economy,
                HasWifi = hasWifi,
                HasFood = hasFood,
                HasEntertainment = hasEntertainment
            };
            await _context.Tickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            // Populate navigation properties for controller use
            flight.Airline = airline;
            flight.DepartureAirport = depAirport;
            flight.ArrivalAirport = arrAirport;
            flight.Tickets = new List<Ticket> { ticket };

            return flight;
        }
    }
}
