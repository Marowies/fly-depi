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
        Task<IEnumerable<(Guid CityId, string CityName)>> GetCityDropdownItemsAsync();
    }
}
