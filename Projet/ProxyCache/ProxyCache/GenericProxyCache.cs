using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace ProxyCache
{
    public class GenericProxyCache<T> where T : new()
    {
        private static MemoryCache cache = MemoryCache.Default;
        public DateTimeOffset dt_default = ObjectCache.InfiniteAbsoluteExpiration;
        public T Get(string cacheItemName)
        {
            if (cache.Contains(cacheItemName))
                return (T)cache.Get(cacheItemName);
            T item = new T();
            cache.Add(cacheItemName, item, dt_default);
            return item;
        }

        public T Get(string cacheItemName, double seconds)
        {
            if (cache.Contains(cacheItemName))
                return (T)cache.Get(cacheItemName);
            T item = new T();
            CacheItemPolicy policy = new CacheItemPolicy();
            if (seconds < 1) seconds = 1;
            policy.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(seconds);
            cache.Add(cacheItemName, item, policy);
            return item;
        }

        public T Get(string cacheItemName, DateTimeOffset absoluteExpiration)
        {
            if (cache.Contains(cacheItemName))
                return (T)cache.Get(cacheItemName);
            T item = new T();
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = absoluteExpiration;
            cache.Add(cacheItemName, item, policy);
            return item;
        }
    }
}
