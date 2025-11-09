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
using GPSserver.Models; // NÉCESSITE LES MODÈLES (voir ci-dessous)
using Newtonsoft.Json;  // NÉCESSITE LE PACKAGE NUGGET Newtonsoft.Json
using System.Net;
using System.ServiceModel.Web;
using System.ServiceModel.Channels;


namespace GPSserver
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    internal class ServerGPS : IServerGPS
    {
        private readonly string _jcKey = Program.JC_KEY;
        private readonly string _orsKey = Program.OS_KEY;

        private static WebFaultException<ErrorResponse> Error(string message)
        {
            AddCorsHeaders(); // <- LA SOLUTION
            return new WebFaultException<ErrorResponse>(
                new ErrorResponse { error = message }, HttpStatusCode.BadRequest);
        }


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
                double walkOnlyDuration = walkOnlyRoute.Routes.First().Summary.Duration;
                double walkOnlyDistance = walkOnlyRoute.Routes.First().Summary.Distance;
                // 4) Stations JCDecaux
                var stations = GetJcDecauxStations(contractName);
                if (stations == null || !stations.Any())
                    throw Error($"Aucune station JCDecaux trouvée pour le contrat {contractName}.");
                var startStation = FindClosestStationWithBikes(stations, originPos);
                var endStation = FindClosestStationWithSpots(stations, destPos);
                Console.WriteLine($"[STATIONS DEBUG] Start: {startStation?.Name ?? "AUCUNE"} - Bikes dispo: {startStation?.TotalStands?.Availabilities?.Bikes}");
                Console.WriteLine($"[STATIONS DEBUG] End:   {endStation?.Name ?? "AUCUNE"} - Places dispo: {endStation?.TotalStands?.Availabilities?.Stands}");
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
                // --- Décodage des tracés ORS (geometry) ---
                var walk1Path = DecodePolyline(walkToStart.Routes.First().Geometry);
                var bikePath = DecodePolyline(bikeRoute.Routes.First().Geometry);
                var walk2Path = DecodePolyline(walkToEnd.Routes.First().Geometry);

                var s1 = walkToStart.Routes.First().Summary;
                var s2 = bikeRoute.Routes.First().Summary;
                var s3 = walkToEnd.Routes.First().Summary;
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
                Instructions = string.Join(" → ", bikeRoute.Routes
                    .SelectMany(r => r.Segments)
                    .SelectMany(s => s.Steps)
                    .Select(st => st.Instruction))
                    ?? $"Prendre un vélo jusqu'à la station {endStation.Name} ({endStation.Address})",
                Duration = Math.Round(s2.Duration),
                Distance = Math.Round(s2.Distance),
                Start = startStation.Position,
                End   = endStation.Position
            },
            new RouteSegment {
                Mode = "walk",
                Instructions = string.Join(" → ", walkToEnd.Routes
                    .SelectMany(r => r.Segments)
                    .SelectMany(s => s.Steps)
                    .Select(st => st.Instruction))
                    ?? "Marcher jusqu'à votre destination finale",

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
                response.Polyline = new List<Position>();
                response.Polyline.AddRange(walk1Path);
                response.Polyline.AddRange(bikePath);
                response.Polyline.AddRange(walk2Path);
                AddCorsHeaders();
                Console.WriteLine("=== DEBUG JSON RENVOYÉ ===");
                Console.WriteLine(JsonConvert.SerializeObject(response, Formatting.Indented));

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
                Console.WriteLine($"[JCDECAUX DEBUG] City from ORS: {cityName}");
                Console.WriteLine($"[JCDECAUX DEBUG] Matched contract: {foundContract?.Name ?? "AUCUN"}");
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
            string url = $"https://api.openrouteservice.org/v2/directions/{profile}";
            string body = $"{{\"coordinates\":[[{start.Longitude.ToString(_culture)},{start.Latitude.ToString(_culture)}],[{end.Longitude.ToString(_culture)},{end.Latitude.ToString(_culture)}]]}}";

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers.Add("Authorization", $"Bearer {_orsKey}");

                try
                {
                    Console.WriteLine($"[ORS DEBUG] POST {url}");
                    Console.WriteLine($"[ORS DEBUG] Body: {body}");

                    string jsonResponse = client.UploadString(url, "POST", body);

                    // Log juste un bout du résultat pour vérifier que c’est du JSON
                    Console.WriteLine($"[ORS DEBUG] Response: {jsonResponse.Substring(0, Math.Min(120, jsonResponse.Length))}...");

                    return JsonConvert.DeserializeObject<OrsResponse>(jsonResponse);
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                    {
                        using (var reader = new System.IO.StreamReader(ex.Response.GetResponseStream()))
                        {
                            string errorResponse = reader.ReadToEnd();
                            Console.WriteLine("[ORS ERROR] " + errorResponse);
                            throw Error("Erreur ORS: " + errorResponse);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ORS ERROR] " + ex.Message);
                        throw Error("Erreur ORS: " + ex.Message);
                    }
                }
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
        private static void AddCorsHeaders()
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
        private List<Position> DecodePolyline(string encoded)
        {
            var poly = new List<Position>();
            int index = 0, len = encoded.Length;
            int lat = 0, lon = 0;

            while (index < len)
            {
                int b, shift = 0, result = 0;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;
                do
                {
                    b = encoded[index++] - 63;
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                } while (b >= 0x20);
                int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
                lon += dlng;

                Position p = new Position
                {
                    Latitude = lat / 1E5,
                    Longitude = lon / 1E5
                };
                poly.Add(p);
            }

            return poly;
        }

    }
}