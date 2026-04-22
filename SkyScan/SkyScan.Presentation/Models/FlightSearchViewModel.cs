using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SkyScan.Presentation.Models
{
    public class FlightSearchViewModel
    {
        [Required(ErrorMessage = "Please select an origin city")]
        [Display(Name = "From")]
        public string OriginCity { get; set; }

        [Required(ErrorMessage = "Please select a destination city")]
        [Display(Name = "To")]
        public string DestinationCity { get; set; }

        [Required(ErrorMessage = "Departure date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Departure Date")]
        public DateTime DepartureDate { get; set; } = DateTime.Today.AddDays(1);

        public List<SelectListItem>? CitiesWithAirports { get; set; }
    }
}
