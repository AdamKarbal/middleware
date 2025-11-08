using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace GPSserver
{
    [ServiceContract()]
    internal interface IServerGPS
    {

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "echoGPS?lat={lat}&lon={lon}",
           BodyStyle = WebMessageBodyStyle.Wrapped, ResponseFormat = WebMessageFormat.Json)]
        String echoGPS(String lat, String lon);

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "health",
           BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        string Health();

        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "testContracts",
                   BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json)]
        string TestContracts();

        [OperationContract]
        [WebInvoke(Method = "GET",
                   UriTemplate = "itinerary?fromLat={fromLat}&fromLon={fromLon}&toLat={toLat}&toLon={toLon}",
                   BodyStyle = WebMessageBodyStyle.Bare,
                   ResponseFormat = WebMessageFormat.Json)]
        string FindItinerary(string fromLat, string fromLon, string toLat, string toLon);

        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "*")]
        void HandleCorsPreflight();
    }
}
