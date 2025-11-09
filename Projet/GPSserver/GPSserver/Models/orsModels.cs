using System.Collections.Generic;
using Newtonsoft.Json;

namespace GPSserver.Models
{
    // === Itinéraires (Directions) ===
    public class OrsResponse
    {
        [JsonProperty("routes")]
        public List<Route> Routes { get; set; }
    }

    public class Route
    {
        [JsonProperty("summary")]
        public Summary Summary { get; set; }

        [JsonProperty("segments")]
        public List<Segment> Segments { get; set; }

        [JsonProperty("geometry")]
        public string Geometry { get; set; }
    }
    public class Step
    {
        [JsonProperty("instruction")]
        public string Instruction { get; set; }

        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }
    }

    public class Segment
    {
        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }
        [JsonProperty("steps")]
        public List<Step> Steps { get; set; }
    }

    public class Summary
    {
        [JsonProperty("distance")]
        public double Distance { get; set; } // en mètres

        [JsonProperty("duration")]
        public double Duration { get; set; } // en secondes
    }

    // === Géocodage inverse ===
    public class OrsReverseGeocodeResponse
    {
        [JsonProperty("features")]
        public List<GeocodeFeature> Features { get; set; }
    }

    public class GeocodeFeature
    {
        [JsonProperty("properties")]
        public GeocodeProperties Properties { get; set; }
    }

    public class GeocodeProperties
    {
        [JsonProperty("locality")]
        public string Locality { get; set; }
    }
}
