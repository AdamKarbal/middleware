using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization; 
using System.Linq;
using System.ServiceModel; 
using System.Text;
using System.Threading.Tasks;
using GPSserver.Models; 
using Newtonsoft.Json;
using System.ServiceModel.Web;

namespace GPSserver
{
    internal class ServerGPS : IServerGPS
    {
        private readonly string _jcKey = Program.JC_KEY;
        private readonly string _orsKey = Program.OS_KEY;

        // Culture invariable pour s'assurer que les "." sont utilisés pour les décimales
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        public String echoGPS(String lat, String lon)
        {
            return "Latitude: " + lat + " Longitude: " + lon;
        }

        public string Health() => "OK";

        public string TestContracts()
        {
            var binding = new System.ServiceModel.BasicHttpBinding();
            var address = new System.ServiceModel.EndpointAddress("http://localhost:8090/ProxyCache");
            using (var proxy = new ProxyClient(binding, address))
            {
                Console.WriteLine("GPSServer is calling ProxyCache...");
                try
                {
                    var url = $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={_jcKey}";
                    return proxy.GetWithTtlAsync(url, 10).Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erreur : " + ex.ToString());
                    return CreateErrorResponse(ex.Message);
                }
            }
        }

        public string FindItinerary(string fromLat, string fromLon, string toLat, string toLon)
        {
            try
            {
                // 1. Conversion des coordonnées
                double originLat = double.Parse(fromLat, _culture);
                double originLon = double.Parse(fromLon, _culture);
                double destLat = double.Parse(toLat, _culture);
                double destLon = double.Parse(toLon, _culture);

                Position originPos = new Position { Latitude = originLat, Longitude = originLon };
                Position destPos = new Position { Latitude = destLat, Longitude = destLon };

                // 2. Trouver la ville et le contrat JCDecaux
                string originCity = GetCityFromGps(originPos);
                if (string.IsNullOrEmpty(originCity))
                {
                    return CreateErrorResponse("Impossible de déterminer la ville de départ.");
                }

                string contractName = FindContractForCity(originCity);
                if (string.IsNullOrEmpty(contractName))
                {
                    return CreateErrorResponse($"Aucun contrat JCDecaux n'a été trouvé pour la ville : {originCity}");
                }
                Console.WriteLine($"Contrat trouvé : {contractName}");

                // 3. Obtenir le trajet à pied (pour comparer)
                OrsResponse walkOnlyRoute = GetOrsRoute("foot-walking", originPos, destPos);
                double walkOnlyDuration = walkOnlyRoute.Features.First().Properties.Summary.Duration;
                double walkOnlyDistance = walkOnlyRoute.Features.First().Properties.Summary.Distance;

                // 4. Obtenir les stations JCDecaux (en utilisant le contrat trouvé)
                List<Station> stations = GetJcDecauxStations(contractName);
                if (stations == null || !stations.Any())
                {
                    return CreateErrorResponse($"Aucune station JCDecaux n'a été trouvée pour le contrat {contractName}.");
                }

                // 5. Trouver les stations pertinentes
                Station startStation = FindClosestStationWithBikes(stations, originPos);
                Station endStation = FindClosestStationWithSpots(stations, destPos);

                if (startStation == null)
                {
                    return CreateErrorResponse("Aucune station avec des vélos disponibles n'a été trouvée à proximité du départ.");
                }
                if (endStation == null)
                {
                    return CreateErrorResponse("Aucune station avec des places libres n'a été trouvée à proximité de la destination.");
                }
                if (startStation.Number == endStation.Number)
                {
                    return CreateErrorResponse("La station de départ et d'arrivée est la même. Marcher est plus simple.");
                }

                // 6. Obtenir les 3 segments du trajet vélo
                OrsResponse walkToStartStation = GetOrsRoute("foot-walking", originPos, startStation.Position);
                OrsResponse bikeRoute = GetOrsRoute("cycling-regular", startStation.Position, endStation.Position);
                OrsResponse walkToEnd = GetOrsRoute("foot-walking", endStation.Position, destPos);

                // 7. Calculer la durée et distance totales du trajet vélo
                Summary walkToStartSummary = walkToStartStation.Features.First().Properties.Summary;
                Summary bikeSummary = bikeRoute.Features.First().Properties.Summary;
                Summary walkToEndSummary = walkToEnd.Features.First().Properties.Summary;

                double totalBikeRouteDuration = walkToStartSummary.Duration + bikeSummary.Duration + walkToEndSummary.Duration;
                double totalBikeRouteDistance = walkToStartSummary.Distance + bikeSummary.Distance + walkToEndSummary.Distance;

                // 8. Comparer et construire la réponse
                ItineraryResponse response;
                if (totalBikeRouteDuration < walkOnlyDuration)
                {
                    // Le vélo EST plus rapide
                    response = new ItineraryResponse
                    {
                        Type = "BIKE_ROUTE",
                        TotalDuration = Math.Round(totalBikeRouteDuration),
                        TotalDistance = Math.Round(totalBikeRouteDistance),
                        Segments = new List<RouteSegment>
                        {
                            new RouteSegment {
                                Mode = "walk",
                                Instructions = $"Marcher jusqu'à la station {startStation.Name} ({startStation.Address})",
                                Duration = Math.Round(walkToStartSummary.Duration),
                                Distance = Math.Round(walkToStartSummary.Distance),
                                Start = originPos,
                                End = startStation.Position
                            },
                            new RouteSegment {
                                Mode = "bike",
                                Instructions = $"Prendre un vélo jusqu'à la station {endStation.Name} ({endStation.Address})",
                                Duration = Math.Round(bikeSummary.Duration),
                                Distance = Math.Round(bikeSummary.Distance),
                                Start = startStation.Position,
                                End = endStation.Position
                            },
                            new RouteSegment {
                                Mode = "walk",
                                Instructions = "Marcher jusqu'à votre destination finale",
                                Duration = Math.Round(walkToEndSummary.Duration),
                                Distance = Math.Round(walkToEndSummary.Distance),
                                Start = endStation.Position,
                                End = destPos
                            }
                        }
                    };
                }
                else
                {
                    // La marche EST plus rapide (ou équivalente)
                    response = new ItineraryResponse
                    {
                        Type = "WALK",
                        TotalDuration = Math.Round(walkOnlyDuration),
                        TotalDistance = Math.Round(walkOnlyDistance),
                        Segments = new List<RouteSegment>
                        {
                            new RouteSegment {
                                Mode = "walk",
                                Instructions = "Marcher jusqu'à votre destination",
                                Duration = Math.Round(walkOnlyDuration),
                                Distance = Math.Round(walkOnlyDistance),
                                Start = originPos,
                                End = destPos
                            }
                        }
                    };
                }
                string jsonResponse = JsonConvert.SerializeObject(response, Formatting.Indented);
                AddCorsHeaders(); 
                return jsonResponse;
            }
            catch (Exception ex)
            {
     
                Exception realError = ex;
                if (ex is AggregateException && ex.InnerException != null)
                {
                    realError = ex.InnerException;
                }
                Console.WriteLine("Erreur DÉTAILLÉE dans FindItinerary : \n" + realError.ToString());
                return CreateErrorResponse(realError.Message);
            }
        }



        private string GetCityFromGps(Position pos)
        {
            string lon = pos.Longitude.ToString(_culture);
            string lat = pos.Latitude.ToString(_culture);
            string url = $"https://api.openrouteservice.org/geocode/reverse?api_key={_orsKey}&point.lon={lon}&point.lat={lat}&layers=locality";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 3600).Result; 
                var response = JsonConvert.DeserializeObject<OrsReverseGeocodeResponse>(jsonResponse);

                return response?.Features?.FirstOrDefault()?.Properties?.Locality;
            }
        }

        private string FindContractForCity(string cityName)
        {
            string url = $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={_jcKey}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 86400).Result;
                var allContracts = JsonConvert.DeserializeObject<List<JcdecauxContract>>(jsonResponse);

                var foundContract = allContracts
                    .FirstOrDefault(c =>
                        string.Equals(c.Name, cityName, StringComparison.OrdinalIgnoreCase) ||
                        (c.Cities != null && c.Cities.Contains(cityName, StringComparer.OrdinalIgnoreCase))
                    );

                return foundContract?.Name;
            }
        }

        private List<Station> GetJcDecauxStations(string contractName)
        {
            string contractQuery = contractName.ToLower();
            string url = $"https://api.jcdecaux.com/vls/v3/stations?contract={contractQuery}&apiKey={_jcKey}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 300).Result;

                Console.WriteLine($"DEBUG: Appel de GetJcDecauxStations avec le contrat : {contractQuery}");
                Console.WriteLine($"DEBUG: Réponse JSON des stations (taille) : {jsonResponse.Length}");
                List<Station> stations = JsonConvert.DeserializeObject<List<Station>>(jsonResponse);
                Console.WriteLine($"DEBUG: Nombre de stations désérialisées : {stations.Count}");

                return stations;            }
        }

        private OrsResponse GetOrsRoute(string profile, Position start, Position end)
        {
            string startCoord = $"{start.Longitude.ToString(_culture)},{start.Latitude.ToString(_culture)}";
            string endCoord = $"{end.Longitude.ToString(_culture)},{end.Latitude.ToString(_culture)}";

            string url = $"https://api.openrouteservice.org/v2/directions/{profile}?api_key={_orsKey}&start={startCoord}&end={endCoord}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 3600).Result;
                return JsonConvert.DeserializeObject<OrsResponse>(jsonResponse);
            }
        }


        private Station FindClosestStationWithBikes(List<Station> stations, Position origin)
        {
            return stations
                .Where(s => s.Status == "OPEN" &&
                            s.TotalStands != null &&
                            s.TotalStands.Availabilities != null &&
                            s.TotalStands.Availabilities.Bikes > 0)
                .OrderBy(s => CalculateDistance(origin, s.Position))
                .FirstOrDefault();
        }

        private Station FindClosestStationWithSpots(List<Station> stations, Position destination)
        {
            return stations
                .Where(s => s.Status == "OPEN" &&
                            s.TotalStands != null &&
                            s.TotalStands.Availabilities != null &&
                            s.TotalStands.Availabilities.Stands > 0)
                .OrderBy(s => CalculateDistance(destination, s.Position))
                .FirstOrDefault();
        }

        private double CalculateDistance(Position pos1, Position pos2)
        {
            var R = 6371e3; 
            var lat1Rad = ToRadians(pos1.Latitude);
            var lat2Rad = ToRadians(pos2.Latitude);
            var dLat = ToRadians(pos2.Latitude - pos1.Latitude);
            var dLon = ToRadians(pos2.Longitude - pos1.Longitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; 
        }

        private double ToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }


        private ProxyClient CreateProxyClient()
        {
            var binding = new System.ServiceModel.BasicHttpBinding();
            var address = new System.ServiceModel.EndpointAddress("http://localhost:8090/ProxyCache");

            binding.MaxReceivedMessageSize = 2097152; 
            binding.MaxBufferSize = 2097152;

            return new ProxyClient(binding, address);
        }

        private string CreateErrorResponse(string message)
        {
            AddCorsHeaders(); // <--- AJOUTEZ CETTE LIGNE
            // Standardise les messages d'erreur en JSON
            return JsonConvert.SerializeObject(new { error = message });
        }

        public void HandleCorsPreflight()
        {
            // Cette méthode est juste là pour intercepter l'appel OPTIONS.
            // On ajoute les en-têtes nécessaires.
            AddCorsHeaders();
        }

        // --- 3. NOUVELLE MÉTHODE HELPER POUR AJOUTER LES EN-TÊTES ---
        private void AddCorsHeaders()
        {
            // Le "WebOperationContext" permet de manipuler la réponse HTTP
            if (WebOperationContext.Current != null)
            {
                var headers = WebOperationContext.Current.OutgoingResponse.Headers;
                // C'est l'en-tête MAGIQUE. Il dit "J'autorise tout le monde".
                headers.Add("Access-Control-Allow-Origin", "*");
                // Permet les méthodes GET (votre itinéraire) et OPTIONS (le pré-vol)
                headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                // Autorise certains en-têtes
                headers.Add("Access-Control-Allow-Headers", "Content-Type");
            }
        }
    }
}