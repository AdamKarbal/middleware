using System.Collections.Generic;

namespace GPSserver.Models
{
    public class ItineraryResponse
    {
        public string Type { get; set; } // "WALK" ou "BIKE_ROUTE"
        public double TotalDuration { get; set; }
        public double TotalDistance { get; set; }
        public List<RouteSegment> Segments { get; set; }
        public List<Position> Polyline { get; set; }
    }

    public class RouteSegment

    {
        public string Mode { get; set; } // "walk", "bike"
        public string Instructions { get; set; }
        public double Duration { get; set; }
        public double Distance { get; set; }
        public Position Start { get; set; }
        public Position End { get; set; }
    }
}