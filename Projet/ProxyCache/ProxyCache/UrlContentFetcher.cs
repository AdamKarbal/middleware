using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ProxyCache
{
    class UrlContentFetcher
    {
        public static string UrlToFetch { get; set; }
        public string Content { get; private set; }

        public UrlContentFetcher()
        {
            if (string.IsNullOrEmpty(UrlToFetch))
            {
                Content = "Erreur";
                return;
            }
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    Content = client.DownloadString(UrlToFetch);
                }
            }
            catch (Exception ex)
            {
                Content = $"Erreur: {ex.Message}";
            }
        }
    }
}
