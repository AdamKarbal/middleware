using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization; // Pour le formatage des nombres (points vs virgules)
using System.Linq;
using System.ServiceModel; // Pour le ProxyClient
using System.Text;
using System.Threading.Tasks;
using GPSserver.Models; // NÉCESSITE LES MODÈLES (voir ci-dessous)
using Newtonsoft.Json;  // NÉCESSITE LE PACKAGE NUGGET Newtonsoft.Json
using System.Net;
using System.ServiceModel.Web;
using GPSserver.Models;


namespace GPSserver
{
    internal class ServerGPS : IServerGPS
    {
        private readonly string _jcKey = Program.JC_KEY;

        // !!! AJOUTEZ VOTRE CLÉ API OPENROUTESERVICE ICI !!!
        private readonly string _orsKey = Program.OS_KEY;

        private static WebFaultException<ErrorResponse> Error(string message) =>
            new WebFaultException<ErrorResponse>(
                new ErrorResponse { error = message }, HttpStatusCode.BadRequest);



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

        // ==================================================================
        // MÉTHODE PRINCIPALE POUR L'ITINÉRAIRE
        // ==================================================================
        public ItineraryResponse FindItinerary(string fromLat, string fromLon, string toLat, string toLon)
        {
            try
            {
                // 1) Parse coordonnées
                double originLat = double.Parse(fromLat, _culture);
                double originLon = double.Parse(fromLon, _culture);
                double destLat = double.Parse(toLat, _culture);
                double destLon = double.Parse(toLon, _culture);

                var originPos = new Position { Latitude = originLat, Longitude = originLon };
                var destPos = new Position { Latitude = destLat, Longitude = destLon };

                // 2) Contrat JCDecaux pour la ville d'origine
                string originCity = GetCityFromGps(originPos);
                if (string.IsNullOrEmpty(originCity))
                    throw Error("Impossible de déterminer la ville de départ.");

                string contractName = FindContractForCity(originCity);
                if (string.IsNullOrEmpty(contractName))
                    throw Error($"Aucun contrat JCDecaux n'a été trouvé pour la ville : {originCity}");

                // 3) Itinéraire 100% à pied (référence)
                var walkOnlyRoute = GetOrsRoute("foot-walking", originPos, destPos);
                double walkOnlyDuration = walkOnlyRoute.Features.First().Properties.Summary.Duration;
                double walkOnlyDistance = walkOnlyRoute.Features.First().Properties.Summary.Distance;

                // 4) Stations JCDecaux
                var stations = GetJcDecauxStations(contractName);
                if (stations == null || !stations.Any())
                    throw Error($"Aucune station JCDecaux trouvée pour le contrat {contractName}.");

                var startStation = FindClosestStationWithBikes(stations, originPos);
                var endStation = FindClosestStationWithSpots(stations, destPos);

                if (startStation == null)
                    throw Error("Aucune station avec vélos disponibles près du départ.");
                if (endStation == null)
                    throw Error("Aucune station avec places libres près de la destination.");
                if (startStation.Number == endStation.Number)
                    throw Error("La station de départ et d'arrivée est la même. Marcher est plus simple.");

                // 5) Les 3 segments du trajet "vélo"
                var walkToStart = GetOrsRoute("foot-walking", originPos, startStation.Position);
                var bikeRoute = GetOrsRoute("cycling-regular", startStation.Position, endStation.Position);
                var walkToEnd = GetOrsRoute("foot-walking", endStation.Position, destPos);

                var s1 = walkToStart.Features.First().Properties.Summary;
                var s2 = bikeRoute.Features.First().Properties.Summary;
                var s3 = walkToEnd.Features.First().Properties.Summary;

                double totalBikeDuration = s1.Duration + s2.Duration + s3.Duration;
                double totalBikeDistance = s1.Distance + s2.Distance + s3.Distance;

                ItineraryResponse response;

                if (totalBikeDuration < walkOnlyDuration)
                {
                    // vélo plus rapide
                    response = new ItineraryResponse
                    {
                        Type = "BIKE_ROUTE",
                        TotalDuration = Math.Round(totalBikeDuration),
                        TotalDistance = Math.Round(totalBikeDistance),
                        Segments = new List<RouteSegment>
                {
                    new RouteSegment {
                        Mode = "walk",
                        Instructions = $"Marcher jusqu'à la station {startStation.Name} ({startStation.Address})",
                        Duration = Math.Round(s1.Duration),
                        Distance = Math.Round(s1.Distance),
                        Start = originPos,
                        End   = startStation.Position
                    },
                    new RouteSegment {
                        Mode = "bike",
                        Instructions = $"Prendre un vélo jusqu'à la station {endStation.Name} ({endStation.Address})",
                        Duration = Math.Round(s2.Duration),
                        Distance = Math.Round(s2.Distance),
                        Start = startStation.Position,
                        End   = endStation.Position
                    },
                    new RouteSegment {
                        Mode = "walk",
                        Instructions = "Marcher jusqu'à votre destination finale",
                        Duration = Math.Round(s3.Duration),
                        Distance = Math.Round(s3.Distance),
                        Start = endStation.Position,
                        End   = destPos
                    }
                }
                    };
                }
                else
                {
                    // marche plus rapide (ou équivalente)
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
                        End   = destPos
                    }
                }
                    };
                }

                return response; // <-- IMPORTANT : on retourne l'objet (pas une string)
            }
            catch (WebFaultException<ErrorResponse>)
            {
                // nos erreurs "métier" (400) → on les laisse passer
                throw;
            }
            catch (Exception ex)
            {
                var real = (ex is AggregateException ae && ae.InnerException != null) ? ae.InnerException : ex;
                Console.WriteLine("Erreur DÉTAILLÉE dans FindItinerary : \n" + real);
                throw Error(real.Message); // renvoie { "error": "..."} avec HTTP 400
            }
        }

        // ==================================================================
        // MÉTHODES HELPER
        // ==================================================================

        // --- Helpers JCDecaux ---

        private string GetCityFromGps(Position pos)
        {
            string lon = pos.Longitude.ToString(_culture);
            string lat = pos.Latitude.ToString(_culture);
            string url = $"https://api.openrouteservice.org/geocode/reverse?api_key={_orsKey}&point.lon={lon}&point.lat={lat}&layers=locality";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 3600).Result; // Cache 1h
                var response = JsonConvert.DeserializeObject<OrsReverseGeocodeResponse>(jsonResponse);

                return response?.Features?.FirstOrDefault()?.Properties?.Locality;
            }
        }

        private string FindContractForCity(string cityName)
        {
            string url = $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={_jcKey}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 86400).Result; // Cache 24h
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
                string jsonResponse = proxy.GetWithTtlAsync(url, 1).Result;

                Console.WriteLine($"DEBUG: Appel de GetJcDecauxStations avec le contrat : {contractQuery}");
                Console.WriteLine($"DEBUG: Réponse JSON des stations (taille) : {jsonResponse.Length}");

                // --- DÉBUT DE LA MODIFICATION ---
                List<Station> stations = JsonConvert.DeserializeObject<List<Station>>(jsonResponse);

                // LA LIGNE DE DEBUG LA PLUS IMPORTANTE :
                Console.WriteLine($"DEBUG: Nombre de stations désérialisées : {stations.Count}");

                return stations;
                // --- FIN DE LA MODIFICATION ---
            }
        }
        // --- Helpers OpenRouteService ---

        private OrsResponse GetOrsRoute(string profile, Position start, Position end)
        {
            string startCoord = $"{start.Longitude.ToString(_culture)},{start.Latitude.ToString(_culture)}";
            string endCoord = $"{end.Longitude.ToString(_culture)},{end.Latitude.ToString(_culture)}";

            string url = $"https://api.openrouteservice.org/v2/directions/{profile}?api_key={_orsKey}&start={startCoord}&end={endCoord}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.GetWithTtlAsync(url, 3600).Result; // Cache 1h
                return JsonConvert.DeserializeObject<OrsResponse>(jsonResponse);
            }
        }

        // --- Helpers de Logique/Calcul ---

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
            var R = 6371e3; // Rayon de la Terre en mètres
            var lat1Rad = ToRadians(pos1.Latitude);
            var lat2Rad = ToRadians(pos2.Latitude);
            var dLat = ToRadians(pos2.Latitude - pos1.Latitude);
            var dLon = ToRadians(pos2.Longitude - pos1.Longitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // en mètres
        }

        private double ToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }

        // --- Helpers Utilitaires ---

        private ProxyClient CreateProxyClient()
        {
            // Centralise la création du client proxy
            var binding = new System.ServiceModel.BasicHttpBinding();
            var address = new System.ServiceModel.EndpointAddress("http://localhost:8090/ProxyCache");

            // --- LA SOLUTION EST ICI ---
            // On augmente la taille max des messages (ex: 2 Mo au lieu de 64 Ko)
            binding.MaxReceivedMessageSize = 2097152; // 2 * 1024 * 1024

            // Il peut aussi être utile d'augmenter la taille du buffer
            binding.MaxBufferSize = 2097152;

            return new ProxyClient(binding, address);
        }

        private string CreateErrorResponse(string message)
        {
            // Standardise les messages d'erreur en JSON
            return JsonConvert.SerializeObject(new { error = message });
        }
    }
}