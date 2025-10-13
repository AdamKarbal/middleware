using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;
using System.Net;

namespace ProxyCache
{
    class ProxyCache : IProxyCache
    {
        private static MemoryCache cache = MemoryCache.Default;
        
        public string Get(string url)
        {
            if (cache.Contains(url)) return (string)cache.Get(url);
            
            try
            {
                using(WebClient client = new WebClient())
                {
                     string content = client.DownloadString(url);
                     CacheItemPolicy policy = new CacheItemPolicy();
                       policy.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(5);
                        cache.Add(url, content, policy);
                        return content;
                    }
                
            }
            catch (Exception ex)
            {
                return $"Error fetching URL: {ex.Message}";
            }
        }

        public string GetWithTtl(string url,double seconds)
        {
            if (cache.Contains(url)) return (string)cache.Get(url);
            try
            {
                using (WebClient client = new WebClient())
                {
                    if(seconds<1) seconds=1;
                    string content = client.DownloadString(url);
                    CacheItemPolicy policy = new CacheItemPolicy();
                    policy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(seconds);
                    cache.Add(url, content, policy);
                    return content;
                }

            }
            catch (Exception ex)
            {
                return $"Error fetching URL: {ex.Message}";
            }
        }
    }
}
