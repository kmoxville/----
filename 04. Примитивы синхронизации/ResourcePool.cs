using System;
using System.Threading;

namespace SyncPrimitives;

public class ResourcePool : IDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public ResourcePool(int maxResources)
    {
        _semaphore = new SemaphoreSlim(maxResources, maxResources);
        MaxResources = maxResources;
    }

    public int MaxResources { get; }

    public int AvailableCount => _semaphore.CurrentCount;

    public void AcquireResource()
    {
        _semaphore.Wait();
    }

    public void ReleaseResource()
    {
        _semaphore.Release();
    }

    public bool TryAcquireResource(int timeoutMs)
    {
        return _semaphore.Wait(timeoutMs);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
