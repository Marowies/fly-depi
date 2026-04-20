using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SkyScan.Core.Entities.AirLine
{
    public class Airplane
    {
        public Guid AirplaneId { get; set; }
        public string Model { get; set; }
        public string ManufactureCompany{ get; set; }
        public string OwnerCompany{ get; set; }
        public DateOnly ManufactureDate{ get; set; }
        public DateOnly StartDate{ get; set; }
        public DateOnly EndDate{ get; set; }
        public string PlaneId{ get; set; }
        public int Seats{ get; set; }
        public List<CabinClass> CabinClasses { get; set; }
    }
}
