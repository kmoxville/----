using System;
using System.Collections.Concurrent;

namespace ConcurrentCollections;

public class CacheItem
{
    public object? Value { get; set; }

    public DateTime Timestamp { get; set; }

    public TimeSpan ExpirationTime { get; set; }
}

public class ConcurrentCache
{
    public ConcurrentDictionary<string, ConcurrentBag<CacheItem>> Cache { get; } = new ConcurrentDictionary<string, ConcurrentBag<CacheItem>>();

    public void AddToCache(string key, object value)
    {
        ConcurrentBag<CacheItem> bag = Cache.GetOrAdd(key, _ => new ConcurrentBag<CacheItem>());
        bag.Add(new CacheItem
        {
            Value = value,
            Timestamp = DateTime.Now,
            ExpirationTime = TimeSpan.FromMinutes(30)
        });
    }

    public bool TryGetFromCache(string key, out object? value)
    {
        value = null;
        if (!Cache.TryGetValue(key, out ConcurrentBag<CacheItem>? bag))
        {
            return false;
        }

        if (!bag.TryPeek(out CacheItem? item))
        {
            return false;
        }

        if (DateTime.Now - item.Timestamp > item.ExpirationTime)
        {
            return false;
        }

        value = item.Value;
        return value != null;
    }

    public bool RemoveFromCache(string key)
    {
        return Cache.TryRemove(key, out _);
    }

    public void ClearCache()
    {
        Cache.Clear();
    }

    public int GetCacheSize()
    {
        int count = 0;
        foreach (ConcurrentBag<CacheItem> bag in Cache.Values)
        {
            count += bag.Count;
        }

        return count;
    }
}
