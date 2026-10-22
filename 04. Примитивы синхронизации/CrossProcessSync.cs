using System;
using System.Threading;

namespace SyncPrimitives;

public class CrossProcessSync
{
    public void ExecuteWithGlobalLock(string mutexName, Action action)
    {
        using var mutex = Open(mutexName);
        bool taken = false;
        try
        {
            try
            {
                taken = mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                taken = true;
            }

            if (taken)
            {
                action();
            }
        }
        finally
        {
            if (taken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    public bool TryExecuteWithGlobalLock(string mutexName, Action action, int timeoutMs)
    {
        using var mutex = Open(mutexName);
        bool taken = false;
        try
        {
            try
            {
                taken = mutex.WaitOne(timeoutMs);
            }
            catch (AbandonedMutexException)
            {
                taken = true;
            }

            if (!taken)
            {
                return false;
            }

            action();
            return true;
        }
        finally
        {
            if (taken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static Mutex Open(string mutexName)
    {
        try
        {
            return Mutex.OpenExisting(mutexName);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return new Mutex(false, mutexName);
        }
    }
}
