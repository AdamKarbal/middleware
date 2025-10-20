using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;


namespace GPSserver
{
    internal class ServerGPS : IServerGPS
    {

        private readonly string _jcKey = Program.JC_KEY;
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
                    return "Erreur";
                }
            }
        }
    }
}
