using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http; 
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GpsSoapService;

namespace HeavyClient
{
    internal class Program
    {
        private static readonly string ORS_KEY = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImU3NTFkMjk3MjQ4MjQwZjJiNDA2ZDI4ZjVhMjIzMmY2IiwiaCI6Im11cm11cjY0In0=";

        static void Main(string[] args)
            
        {
            while (true)
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.WriteLine("      CLIENT LOURD           ");
                Console.WriteLine("Tapez stop dans l'adresse de départ pour arrêter le programme !");
                Console.Write("\nEntrez l'adresse de DÉPART : ");
                string startAddress = Console.ReadLine();
                if(startAddress.ToLower() == "stop")
                {
                    break;
                }

                Console.Write("Entrez l'adresse d'ARRIVÉE : ");
                string endAddress = Console.ReadLine();

                try
                {

                    var startCoord = GeocodeAddress(startAddress).Result;
                    var endCoord = GeocodeAddress(endAddress).Result;

                    using (var client = new ServerGPSClient())
                    {

                        var itinerary = client.FindItineraryAsync(
                            startCoord.lat.ToString().Replace(',', '.'),
                            startCoord.lon.ToString().Replace(',', '.'),
                            endCoord.lat.ToString().Replace(',', '.'),
                            endCoord.lon.ToString().Replace(',', '.')
                        ).Result;

                        Console.WriteLine("\n Information sur le trajet :");
                        Console.WriteLine("---------------------------------------------------");
                        Console.WriteLine($"Type de trajet : {itinerary.Type}");
                        Console.WriteLine($" Durée      : {FormatDuration(itinerary.TotalDuration)}");
                        Console.WriteLine($" Distance   : {FormatDistance(itinerary.TotalDistance)}");
                        Console.WriteLine("---------------------------------------------------");
                        Console.WriteLine("Étapes :");

                        foreach (var segment in itinerary.Segments)
                        {
                            string icon = segment.Mode == "bike" ? "[VELO]" : "[PIED]";
                            Console.Write($"{icon} ");
                            string instructions = segment.Instructions.Replace("→", "\n      ->");

                            Console.WriteLine(instructions);
                            Console.WriteLine($"      ({FormatDistance(segment.Distance)} - {FormatDuration(segment.Duration)})");
                            Console.WriteLine(); 
                        }
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nERREUR : {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\nAppuyez sur une touche pour quitter...");
            Console.ReadKey();
        }

        private static async Task<(double lat, double lon)> GeocodeAddress(string address)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "HeavyClient/1.0");
                string url = $"https://api.openrouteservice.org/geocode/search?api_key={ORS_KEY}&text={Uri.EscapeDataString(address)}&size=1";

                var response = await httpClient.GetStringAsync(url);
                dynamic json = JsonConvert.DeserializeObject(response);

                if (json.features.Count > 0)
                {
                    double lon = json.features[0].geometry.coordinates[0];
                    double lat = json.features[0].geometry.coordinates[1];
                    return (lat, lon);
                }
                else
                {
                    throw new Exception($"Adresse introuvable : {address}");
                }
            }
        }
        private static string FormatDuration(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalMinutes < 1) return $"{t.Seconds} sec";
            if (t.TotalHours < 1) return $"{Math.Ceiling(t.TotalMinutes)} min";
            return $"{(int)t.TotalHours}h {t.Minutes}min";
        }
        private static string FormatDistance(double meters)
        {
            if (meters < 1000) return $"{Math.Round(meters)} m";
            return $"{Math.Round(meters / 1000, 1)} km";
        }
    }
}