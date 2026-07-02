using Microsoft.EntityFrameworkCore;
using SkyScan.Core.Entities;
using SkyScan.Core.Repositories_Interfaces;
using SkyScan.Infrastructure.Data.Data_Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SkyScan.Infrastructure.Data.Repositories_Implementations
{
    public class BookingRepository : GenericRepository<Booking>, IBookingRepository
    {
        public BookingRepository(SkyScanDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Booking>> GetBookingsByUserIdAsync(Guid userId)
        {
            return await _dbSet
                .Include(b => b.Flight).ThenInclude(f => f.Airline)
                .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport).ThenInclude(a => a.City)
                .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport).ThenInclude(a => a.City)
                .Include(b => b.Flight).ThenInclude(f => f.Tickets)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
