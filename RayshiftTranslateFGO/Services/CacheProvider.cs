using System;
using Microsoft.Extensions.Caching.Memory;

namespace RayshiftTranslateFGO.Services
{
    public class CacheProvider: ICacheProvider
    {
        private readonly IMemoryCache _cache;

        public CacheProvider()
        {
            _cache = new MemoryCache(new MemoryCacheOptions() { });
        }
        public void Set<T>(string key, T value, DateTimeOffset absoluteExpiry)
        {
            _cache.Set(key, value, absoluteExpiry);
        }
        public void Set<T>(string key, T value)
        {
            _cache.Set(key, value);
        }
        public T Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out T value))
                return value;
            else
                return default(T);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}