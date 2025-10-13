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
            if (cache.Contains(url))
            {
                return (string)cache.Get(url);
            }
            else
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
        }
    }
}
