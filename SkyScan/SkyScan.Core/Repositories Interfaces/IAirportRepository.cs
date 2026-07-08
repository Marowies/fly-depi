using SkyScan.Core.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IAirportRepository : IGenericRepository<Airport>
    {
        Task<IEnumerable<Airport>> GetAllWithDetailsAsync();
        Task<Airport?> GetByIataAsync(string iataCode);
        Task<IEnumerable<(Guid CityId, string CityName, string CountryName, string? CityNameAr, string? CountryNameAr)>> GetCityDropdownItemsAsync();
        Task<IEnumerable<Airport>> GetAirportsByCityIdAsync(Guid cityId);
        Task<City?> GetCityByNameAsync(string cityName);
        Task<City?> GetCityByIdAsync(Guid cityId);
        Task IncrementCitySearchCountAsync(Guid cityId);
        Task<IEnumerable<City>> GetTopCitiesBySearchCountAsync(int count = 20);
    }
}
