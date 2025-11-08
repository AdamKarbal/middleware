using System.Collections.Generic;
using Newtonsoft.Json;

namespace GPSserver.Models
{
    public class OrsResponse
    {
        [JsonProperty("features")]
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("properties")]
        public Properties Properties { get; set; }
    }

    public class Properties
    {
        [JsonProperty("summary")]
        public Summary Summary { get; set; }
    }

    public class Summary
    {
        [JsonProperty("distance")]
        public double Distance { get; set; } // en mètres

        [JsonProperty("duration")]
        public double Duration { get; set; } // en secondes
    }

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