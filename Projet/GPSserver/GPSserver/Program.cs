using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace GPSserver
{
    internal class Program
    {
        internal static readonly string JC_KEY = "c2388aa3d552da1e50d5540b501ae4a278082058";

        static void Main(string[] args)
        {
            Uri baseUrl = new Uri("http://localhost:8000/ServerGPS/");
            ServiceHost host = new ServiceHost(typeof(ServerGPS), baseUrl);

            // --- ENDPOINT #1 : Le service REST (votre code actuel) ---
            // Il sera accessible à l'adresse de base : http://localhost:8000/ServerGPS/
            ServiceEndpoint restEndpoint = host.AddServiceEndpoint(typeof(IServerGPS), new WebHttpBinding(), "");
            restEndpoint.Behaviors.Add(new WebHttpBehavior());

            // --- ENDPOINT #2 : Le service SOAP (pour l'outil "Ajouter une référence") ---
            // On lui donne une adresse relative différente pour ne pas créer de conflit.
            // Il sera accessible à : http://localhost:8000/ServerGPS/soap
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
            host.Close(); // Bonne pratique de fermer le host
        }
    }
}