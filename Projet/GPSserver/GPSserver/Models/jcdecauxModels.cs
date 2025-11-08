using System.Collections.Generic;
using Newtonsoft.Json;

namespace GPSserver.Models
{
    public class Station
    {
        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("contractName")] // CORRECT
        public string ContractName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("totalStands")] // CORRECT
        public TotalStands TotalStands { get; set; }
    }

    public class Position
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }

    public class TotalStands
    {
        [JsonProperty("availabilities")]
        public Availabilities Availabilities { get; set; }
    }

    public class Availabilities
    {
        [JsonProperty("bikes")]
        public int Bikes { get; set; }

        [JsonProperty("stands")]
        public int Stands { get; set; } // Les places libres
    }
    public class JcdecauxContract
    {
        [JsonProperty("name")]
        public string Name { get; set; } // C'est le nom à utiliser, ex: "Lyon"

        [JsonProperty("cities")]
        public List<string> Cities { get; set; } // Villes couvertes, ex: ["Lyon", "Villeurbanne"]
    }
}