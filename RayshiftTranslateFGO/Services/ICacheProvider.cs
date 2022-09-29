using System;

namespace RayshiftTranslateFGO.Services
{
    public interface ICacheProvider
    {
        public void Set<T>(string key, T value, DateTimeOffset absoluteExpiry);
        public void Set<T>(string key, T value);
        public T Get<T>(string key);
        public void Remove(string key);
    }
}