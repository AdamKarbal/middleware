using ProxyCache;
using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;


// TEST CLIENT DONNée PAR GEMINI PRO POUR TESTER NOTRE PROXY
class Program
{
    // 1. On change la signature de Main pour qu'elle devienne asynchrone
    static async Task Main(string[] args)
    {
        // Crée une instance du client en lui fournissant la configuration manuellement
        var binding = new BasicHttpBinding();
        var address = new EndpointAddress("http://localhost:8090/ProxyCache");
        ProxyClient client = new ProxyClient(binding, address);

        // L'URL d'une API REST que nous allons tester (liste des contrats JCDecaux)
        // N'OUBLIEZ PAS DE METTRE VOTRE VRAIE CLÉ API ICI
        string testUrl = "https://api.jcdecaux.com/vls/v1/stations/2010?contract=Lyon&apiKey=4b1c39db1fe00b51f8e78714ac95c871e1115c5b";

        Console.WriteLine("--- TEST DU CACHE ---");
        Console.WriteLine("URL cible : " + testUrl);
        Console.WriteLine("\nAppuyez sur Entrée pour commencer...");
        Console.ReadLine();

        // --- TEST 1 : Premier appel (doit être un CACHE MISS) ---
        Console.WriteLine("1. Premier appel (devrait déclencher un appel externe)...");
        // 2. On utilise "await" et la méthode se termine par "Async"
        string result1 = await client.GetWithTtlAsync(testUrl, 5);
        Console.WriteLine("   -> Reçu : " + (result1.Length > 80 ? result1.Substring(0, 80) : result1) + "...\n");

        // --- TEST 2 : Deuxième appel immédiat (doit être un CACHE HIT) ---
        Console.WriteLine("2. Deuxième appel, 2 secondes plus tard (devrait utiliser le cache)...");
        Thread.Sleep(2000);
        // 3. On fait de même pour la méthode Get
        string result2 = await client.GetAsync(testUrl);
        Console.WriteLine("   -> Reçu : " + (result2.Length > 80 ? result2.Substring(0, 80) : result2) + "...\n");

        // --- TEST 3 : Troisième appel après expiration (doit être un CACHE MISS) ---
        Console.WriteLine("3. Troisième appel, 6 secondes plus tard (le cache de 5s a dû expirer)...");
        Thread.Sleep(6000);
        string result3 = await client.GetAsync(testUrl);
        Console.WriteLine("   -> Reçu : " + (result3.Length > 80 ? result3.Substring(0, 80) : result3) + "...\n");

        Console.WriteLine("--- Test terminé. Appuyez sur Entrée pour quitter. ---");
        Console.ReadLine();
    }
}