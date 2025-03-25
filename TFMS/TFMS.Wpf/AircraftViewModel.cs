using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFMS.Wpf
{
    public class AircraftViewModel
    {
        public string Callsign { get; set; }

        public DateTimeOffset LandingTime { get; set; }

        public DateTimeOffset FeederFixTime { get; set; }

        public string? Runway { get; set; }

        public TimeSpan TotalDelay { get; set; }

        public TimeSpan RemainingDelay { get; set; }

        public bool MaintainProfileSpeed { get; set; }
    }
}
