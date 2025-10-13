using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel.Web;

namespace GPSserver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Uri baseUrl = new Uri("http://localhost:8000/ServerGPS");
            ServiceHost host = new ServiceHost(typeof(ServerGPS), baseUrl);

            var JC_KEY = "c2388aa3d552da1e50d5540b501ae4a278082058"; // Clé API JCDecaux

            ServiceEndpoint ep = host.AddServiceEndpoint(typeof(IServerGPS), new WebHttpBinding(), "");
            ep.Behaviors.Add(new WebHttpBehavior());
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
