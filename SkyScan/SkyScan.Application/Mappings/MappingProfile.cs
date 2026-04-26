using AutoMapper;
using SkyScan.Core.Entities.AirLine;
using SkyScan.Application.DTOs;

namespace SkyScan.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Maps the Flight Entity from the database to the FlightDto we use in our views
            CreateMap<Flight, FlightDto>()
                .ForMember(dest => dest.AirlineName, opt => opt.MapFrom(src => src.Airline != null ? src.Airline.Name : "Unknown"))
                .ForMember(dest => dest.OriginAirport, opt => opt.MapFrom(src => src.DepartureAirport != null ? src.DepartureAirport.Code : "Unknown"))
                .ForMember(dest => dest.DestinationAirport, opt => opt.MapFrom(src => src.ArrivalAirport != null ? src.ArrivalAirport.Code : "Unknown"))
                .ForMember(dest => dest.Price, opt => opt.Ignore()) // Prices are usually dynamic
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Scheduled")); // Mock status or compute it
        }
    }
}
