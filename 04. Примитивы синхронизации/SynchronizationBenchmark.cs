using System;
using System.Diagnostics;
using System.Threading;

namespace SyncPrimitives;

public class SynchronizationBenchmark
{
    public (long ReadTime, long WriteTime, long TotalTime, bool Intact) BenchmarkReaderWriterLock(
        LibraryCatalog catalog,
        int readerCount,
        int writerCount)
    {
        var readerTimes = new long[readerCount];
        var writerTimes = new long[writerCount];
        var threads = new Thread[readerCount + writerCount];
        var watch = Stopwatch.StartNew();

        for (int i = 0; i < readerCount; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                var local = Stopwatch.StartNew();
                catalog.SearchBooks("Book");
                local.Stop();
                readerTimes[index] = local.ElapsedMilliseconds;
            });
            threads[i].Start();
        }

        for (int i = 0; i < writerCount; i++)
        {
            int index = i;
            threads[readerCount + i] = new Thread(() =>
            {
                var local = Stopwatch.StartNew();
                catalog.AddBook("Extra " + index, "Writer");
                local.Stop();
                writerTimes[index] = local.ElapsedMilliseconds;
            });
            threads[readerCount + i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }

        watch.Stop();
        bool intact = catalog.GetAllBooks().Count == 1000 + writerCount;
        return (Max(readerTimes), Max(writerTimes), watch.ElapsedMilliseconds, intact);
    }

    public (long Time, int Success, int Failed) BenchmarkSemaphore(ResourcePool pool, int requestCount)
    {
        int success = 0;
        int failed = 0;
        var threads = new Thread[requestCount];
        var watch = Stopwatch.StartNew();

        for (int i = 0; i < requestCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                if (!pool.TryAcquireResource(30))
                {
                    Interlocked.Increment(ref failed);
                    return;
                }

                try
                {
                    Thread.Sleep(150);
                    Interlocked.Increment(ref success);
                }
                finally
                {
                    pool.ReleaseResource();
                }
            });
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }

        watch.Stop();
        return (watch.ElapsedMilliseconds, success, failed);
    }

    public (long Time, int Success) BenchmarkMutex(int operationCount)
    {
        int success = 0;
        var threads = new Thread[operationCount];
        var sync = new CrossProcessSync();
        var watch = Stopwatch.StartNew();

        for (int i = 0; i < operationCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                sync.ExecuteWithGlobalLock("LibraryBenchmarkMutex", () =>
                {
                    Interlocked.Increment(ref success);
                });
            });
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }

        watch.Stop();
        return (watch.ElapsedMilliseconds, success);
    }

    public (long RwTime, long ExclusiveTime) CompareReads(LibraryCatalog catalog, int readerCount)
    {
        long rwTime = RunReaders(readerCount, () =>
        {
            for (int i = 0; i < 30; i++)
            {
                catalog.SearchBooks("Book");
            }
        });
        var sync = new CrossProcessSync();
        long exclusiveTime = RunReaders(readerCount, () =>
        {
            sync.ExecuteWithGlobalLock("LibraryReadMutex", () =>
            {
                for (int i = 0; i < 30; i++)
                {
                    catalog.SearchBooks("Book");
                }
            });
        });
        return (rwTime, exclusiveTime);
    }

    public (double Rw, double Semaphore, double Mutex) MeasureOverhead()
    {
        var rwLock = new ReaderWriterLockSlim();
        double rw = Percent(
            () => { },
            () =>
            {
                rwLock.EnterReadLock();
                try
                {
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
            },
            5000);

        var pool = new ResourcePool(1);
        double semaphore = Percent(
            () => { },
            () =>
            {
                pool.AcquireResource();
                pool.ReleaseResource();
            },
            5000);

        using var mutex = new Mutex(false, "LibraryOverheadMutex");
        double mutexOverhead = Percent(
            () => { },
            () =>
            {
                mutex.WaitOne();
                mutex.ReleaseMutex();
            },
            5000);

        rwLock.Dispose();
        pool.Dispose();
        return (rw, semaphore, mutexOverhead);
    }

    private static long RunReaders(int readerCount, Action action)
    {
        var threads = new Thread[readerCount];
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < readerCount; i++)
        {
            threads[i] = new Thread(() => action());
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }

        watch.Stop();
        return watch.ElapsedMilliseconds;
    }

    private static double Percent(Action bare, Action sync, int iterations = 20000)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            bare();
        }

        watch.Stop();
        long bareTicks = watch.ElapsedTicks;

        watch.Restart();
        for (int i = 0; i < iterations; i++)
        {
            sync();
        }

        watch.Stop();
        if (bareTicks == 0)
        {
            bareTicks = 1;
        }

        return (watch.ElapsedTicks - bareTicks) * 100.0 / bareTicks;
    }

    private static long Max(long[] values)
    {
        long max = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }

        return max;
    }
}
