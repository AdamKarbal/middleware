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
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped, UriTemplate = "echoGPS?lat={lat}&lon={lon}")]
        String echoGPS(String lat, String lon);
    }
}
