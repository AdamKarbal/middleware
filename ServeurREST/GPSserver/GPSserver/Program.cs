using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace GPSserver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Uri baseUrl = new Uri("http://localhost:8000/GPSserver");
            ServiceHost host = new ServiceHost(typeof(ServerGPS), baseUrl);

            host.AddServiceEndpoint(typeof(IServerGPS), new WebHttpBinding(), "");

            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled= true;
            host.Description.Behaviors.Add(smb);

            host.Open();

            Console.WriteLine("Service is running at " + baseUrl);
            Console.WriteLine("Press Enter to terminate the service.");
            Console.ReadLine();
        }
    }
}
