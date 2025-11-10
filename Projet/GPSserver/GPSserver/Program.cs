using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace GPSserver
{
    internal class Program
    {
        internal static readonly string JC_KEY = "c2388aa3d552da1e50d5540b501ae4a278082058";
        internal static readonly string OS_KEY = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImU3NTFkMjk3MjQ4MjQwZjJiNDA2ZDI4ZjVhMjIzMmY2IiwiaCI6Im11cm11cjY0In0=";
        static void Main(string[] args)
        {
            Uri baseUrl = new Uri("http://localhost:8000/ServerGPS/");
            ServiceHost host = new ServiceHost(typeof(ServerGPS), baseUrl);
            ServiceEndpoint restEndpoint = host.AddServiceEndpoint(typeof(IServerGPS), new WebHttpBinding(), "");
            restEndpoint.Behaviors.Add(new WebHttpBehavior());
            host.AddServiceEndpoint(typeof(IServerGPS), new BasicHttpBinding(), "soap");
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            host.Description.Behaviors.Add(smb);
            host.Open();
            Console.WriteLine("Service is running.");
            Console.WriteLine("REST endpoint at: " + baseUrl);
            Console.WriteLine("SOAP endpoint at: " + baseUrl + "soap");
            Console.WriteLine("Press Enter to terminate the service.");
            Console.ReadLine();
            host.Close();
        }
    }
}