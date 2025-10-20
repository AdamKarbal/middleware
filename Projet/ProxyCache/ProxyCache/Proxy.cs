using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;
using System.Net;

namespace ProxyCache
{
    class Proxy : IProxy
    {
        private static GenericProxyCache<UrlContentFetcher> cache = new GenericProxyCache<UrlContentFetcher>();

        public string Get(string url)
        {
            UrlContentFetcher.UrlToFetch = url;
            UrlContentFetcher fetchedData = cache.Get(url);
            return fetchedData.Content;
        }

        public string GetWithTtl(string url, double seconds)
        {
            UrlContentFetcher.UrlToFetch = url;
            UrlContentFetcher fetchedData = cache.Get(url, seconds);
            return fetchedData.Content;
        }
    }
}
