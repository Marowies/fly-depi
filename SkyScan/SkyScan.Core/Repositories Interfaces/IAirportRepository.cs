using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkyScan.Core.Entities;

namespace SkyScan.Core.Repositories_Interfaces
{
    public interface IAirportRepository : IGenericRepository<Airport>
    {
        /// <summary>Loads all airports with City and Country — use only when full entity data is required.</summary>
        Task<IEnumerable<Airport>> GetAllWithDetailsAsync();


        /// <summary>Indexed lookup by IATA code — O(log n) at DB level. Use instead of loading all airports.</summary>
        Task<Airport?> GetByIataAsync(string iataCode);

        Task<IEnumerable<(Guid CityId, string CityName)>> GetCityDropdownItemsAsync();
    }
}
