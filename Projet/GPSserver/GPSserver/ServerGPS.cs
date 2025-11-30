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
using GPSserver.Models; 
using Newtonsoft.Json;  
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
            AddCorsHeaders(); 
            return new WebFaultException<ErrorResponse>(
                new ErrorResponse { error = message }, HttpStatusCode.BadRequest);
        }

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
                var originPos = new Position { Latitude = double.Parse(fromLat, _culture), Longitude = double.Parse(fromLon, _culture) };
                var destPos = new Position { Latitude = double.Parse(toLat, _culture), Longitude = double.Parse(toLon, _culture) };
                var response = new ItineraryResponse
                {
                    Type = "MIXED_ROUTE",
                    Segments = new List<RouteSegment>(),
                    Polyline = new List<Position>(),
                    TotalDuration = 0,
                    TotalDistance = 0
                };

                string originCity = GetCityFromGps(originPos);
                string destCity = GetCityFromGps(destPos);
                string originContract = (!string.IsNullOrEmpty(originCity)) ? FindContractForCity(originCity) : null;
                string destContract = (!string.IsNullOrEmpty(destCity)) ? FindContractForCity(destCity) : null;

                Console.WriteLine($"[LOGIC] Départ: {originCity} ({originContract}) -> Arrivée: {destCity} ({destContract})");

                Position currentPos = originPos;

                if (originContract != null)
                {
                    var stations = GetJcDecauxStations(originContract);
                    var startStation = FindClosestStationWithBikes(stations, currentPos);
                    var dropOffStation = FindClosestStationWithSpots(stations, destPos);
                    if (startStation != null && dropOffStation != null && startStation.Number != dropOffStation.Number)
                    {
                        Console.WriteLine($"[ETAPE 1] Vélo local à {originCity} : {startStation.Name} -> {dropOffStation.Name}");
                        AddSegmentToResponse(response, "walk", currentPos, startStation.Position, $"Marcher vers {startStation.Name}");
                        AddSegmentToResponse(response, "bike", startStation.Position, dropOffStation.Position, $"Vélo vers {dropOffStation.Name}");
                        currentPos = dropOffStation.Position;
                    }
                }

                Position lyonEntryPos = destPos; 
                Station lyonPickUpStation = null;
                Station lyonDropOffStation = null;
                bool doBikeAtArrival = false;

                if (destContract != null)
                {
                    var stations = GetJcDecauxStations(destContract);
                    lyonPickUpStation = FindClosestStationWithBikes(stations, currentPos);
                    lyonDropOffStation = FindClosestStationWithSpots(stations, destPos);

                    if (lyonPickUpStation != null && lyonDropOffStation != null && lyonPickUpStation.Number != lyonDropOffStation.Number)
                    {
                        doBikeAtArrival = true;
                        lyonEntryPos = lyonPickUpStation.Position;
                    }
                }

                double distGap = CalculateDistance(currentPos, lyonEntryPos);
                if (distGap > 20)
                {
                    Console.WriteLine($"[ETAPE 2] Transition (Marche) : Distance {Math.Round(distGap)}m");
                    AddSegmentToResponse(response, "walk", currentPos, lyonEntryPos, "Trajet de transition (Marche)");
                    currentPos = lyonEntryPos;
                }
                if (doBikeAtArrival)
                {
                    Console.WriteLine($"[ETAPE 3] Vélo local à {destCity} : {lyonPickUpStation.Name} -> {lyonDropOffStation.Name}");

                    AddSegmentToResponse(response, "bike", lyonPickUpStation.Position, lyonDropOffStation.Position, $"Vélo vers {lyonDropOffStation.Name}");
                    currentPos = lyonDropOffStation.Position;
                }

                AddSegmentToResponse(response, "walk", currentPos, destPos, "Marcher jusqu'à la destination finale");
                AddCorsHeaders();
                return response;
            }
            catch (Exception ex)
            {
                var real = (ex is AggregateException ae && ae.InnerException != null) ? ae.InnerException : ex;
                Console.WriteLine("ERREUR : \n" + real);
                throw Error(real.Message);
            }
        }

        private void AddSegmentToResponse(ItineraryResponse response, string mode, Position start, Position end, string instructionBase)
        {
            string profile = (mode == "walk") ? "foot-walking" : "cycling-regular";
            var orsData = GetOrsRoute(profile, start, end);
            var summary = orsData.Routes.First().Summary;
            var segmentGeometry = DecodePolyline(orsData.Routes.First().Geometry);
            response.TotalDuration += summary.Duration;
            response.TotalDistance += summary.Distance;
            string details = string.Join(" → ", orsData.Routes.First().Segments
                                .SelectMany(s => s.Steps)
                                .Select(st => st.Instruction));
            if (string.IsNullOrEmpty(details)) details = instructionBase;
            response.Segments.Add(new RouteSegment
            {
                Mode = mode,
                Instructions = details,
                Duration = Math.Round(summary.Duration),
                Distance = Math.Round(summary.Distance),
                Start = start,
                End = end,
                Geometry = segmentGeometry
            });

            response.Polyline.AddRange(segmentGeometry);
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
                Console.WriteLine($" City from ORS: {cityName}");
                Console.WriteLine($" Matched contract: {foundContract?.Name ?? "AUCUN"}");
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

            string body = $@"
    {{
        ""coordinates"": [
            [{start.Longitude.ToString(_culture)}, {start.Latitude.ToString(_culture)}],
            [{end.Longitude.ToString(_culture)}, {end.Latitude.ToString(_culture)}]
        ],
        ""language"": ""fr""
    }}";

            using (var proxy = CreateProxyClient())
            {
                string jsonResponse = proxy.PostWithTtl(url, body, 600);
                Console.WriteLine("[DEBUG ORS RESPONSE RAW]: " + jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 200)));

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
            AddCorsHeaders();
            return JsonConvert.SerializeObject(new { error = message });
        }

        public void HandleCorsPreflight()
        {
            AddCorsHeaders();
        }

        private static void AddCorsHeaders()
        {
            if (WebOperationContext.Current != null)
            {
                var headers = WebOperationContext.Current.OutgoingResponse.Headers;
                headers.Add("Access-Control-Allow-Origin", "*");
                headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
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