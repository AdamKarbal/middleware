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
            using (var proxy = new ProxyServiceClient())  // nom exact selon ta ref
            {
                var url = $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={_jcKey}";
                return proxy.GetWithTtl(url, 10); // TTL court pour voir MISS puis HIT côté Proxy
            }
        }
    }
}
