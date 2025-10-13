using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPSserver
{
    internal class ServerGPS : IServerGPS
    {
        public String echoGPS(String lat, String lon)
        {
            return "Latitude: " + lat + " Longitude: " + lon;
        }
    }
}
