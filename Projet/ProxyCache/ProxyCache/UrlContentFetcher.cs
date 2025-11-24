using System;
using System.Net;
using System.Text;

namespace ProxyCache
{
    class UrlContentFetcher
    {
        public static string UrlToFetch { get; set; }
        public static string BodyToPost { get; set; } = null;
        public static string ApiKey { get; set; } = null;

        public string Content { get; private set; }

        public UrlContentFetcher()
        {
            if (string.IsNullOrEmpty(UrlToFetch))
            {
                Content = "Erreur : URL manquante";
                return;
            }

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;

                    // 🔹 Si c’est une requête ORS, ajoute la clé
                    if (UrlToFetch.Contains("openrouteservice.org") && !string.IsNullOrEmpty(ApiKey))
                        client.Headers.Add("Authorization", $"Bearer {ApiKey}");

                    client.Headers[HttpRequestHeader.ContentType] = "application/json";

                    if (!string.IsNullOrEmpty(BodyToPost))
                    {
                        // Requête POST
                        Content = client.UploadString(UrlToFetch, "POST", BodyToPost);
                    }
                    else
                    {
                        // Requête GET
                        Content = client.DownloadString(UrlToFetch);
                    }
                }
            }
            catch (Exception ex)
            {
                Content = $"Erreur: {ex.Message}";
            }
        }
    }
}
