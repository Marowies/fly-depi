using System;

namespace SkyScan.Application.DTOs
{
    public class NearestCityDto
    {
        public Guid CityId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
