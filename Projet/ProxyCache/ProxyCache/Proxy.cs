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

        public string PostWithTtl(string url, string body, double seconds)
        {
            UrlContentFetcher.UrlToFetch = url;
            UrlContentFetcher.BodyToPost = body;
            UrlContentFetcher.ApiKey = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImU3NTFkMjk3MjQ4MjQwZjJiNDA2ZDI4ZjVhMjIzMmY2IiwiaCI6Im11cm11cjY0In0=";

            UrlContentFetcher fetchedData = cache.Get(url + body, seconds);
            UrlContentFetcher.BodyToPost = null; // reset
            UrlContentFetcher.ApiKey = null;
            return fetchedData.Content;
        }


    }
}
