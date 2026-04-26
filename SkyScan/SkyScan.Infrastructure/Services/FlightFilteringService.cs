using SkyScan.Application.DTOs;
using SkyScan.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyScan.Infrastructure.Services
{
    public class FlightFilteringService : IFlightFilteringService
    {
        public IEnumerable<FlightDto> FilterAndSort(IEnumerable<FlightDto> flights, FlightFilterCriteriaDto criteria)
        {
            var query = flights.AsQueryable();

            // 1. Filter by Stops
            if (criteria.Stops != null && criteria.Stops.Any())
            {
                query = query.Where(f => criteria.Stops.Contains(f.Stops >= 2 ? 2 : f.Stops));
            }

            // 2. Filter by Price Range
            if (criteria.MinPrice.HasValue)
                query = query.Where(f => f.Price >= criteria.MinPrice.Value);
            
            if (criteria.MaxPrice.HasValue)
                query = query.Where(f => f.Price <= criteria.MaxPrice.Value);

            // 3. Filter by Airlines
            if (criteria.Airlines != null && criteria.Airlines.Any())
            {
                query = query.Where(f => criteria.Airlines.Contains(f.AirlineName));
            }

            // 4. Filter by Time Windows
            if (criteria.DepartureWindows != null && criteria.DepartureWindows.Any())
            {
                query = query.Where(f => IsInTimeWindow(f.DepartureTime, criteria.DepartureWindows));
            }

            // 5. Sorting
            query = criteria.SortBy switch
            {
                FlightSortOption.Cheapest => query.OrderBy(f => f.Price),
                FlightSortOption.Fastest => query.OrderBy(f => f.Duration),
                FlightSortOption.Earliest => query.OrderBy(f => f.DepartureTime),
                _ => query.OrderBy(f => f.Price)
            };

            return query.ToList();
        }

        private bool IsInTimeWindow(DateTime time, List<TimeWindow> windows)
        {
            var hour = time.Hour;
            return windows.Any(w => w switch
            {
                TimeWindow.Morning => hour >= 6 && hour < 12,
                TimeWindow.Afternoon => hour >= 12 && hour < 18,
                TimeWindow.Evening => hour >= 18 && hour < 24,
                TimeWindow.Night => hour >= 0 && hour < 6,
                _ => false
            });
        }
    }
}
