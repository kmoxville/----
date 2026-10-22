using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ConcurrentCollections;

public class CollectionBenchmark
{
    public (long Time, int Success, int Failed) BenchmarkConcurrentDictionary(int operationCount)
    {
        return RunConcurrent(operationCount, 50);
    }

    public (long Time, int Processed) BenchmarkBlockingCollection(int taskCount, int workerCount)
    {
        var manager = new TaskQueueManager(100);
        manager.ProcessTasks(workerCount);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < taskCount; i++)
        {
            manager.AddTask("task " + i, () => { });
        }

        manager.CompleteAdding();
        watch.Stop();
        return (watch.ElapsedMilliseconds, manager.ProcessedCount);
    }

    public (long Time, int Success) BenchmarkSynchronizedDictionary(int operationCount)
    {
        int success = RunLocked(operationCount, 50);
        return (LastLockedMilliseconds, success);
    }

    private long LastLockedMilliseconds;

    private int RunLocked(int operationCount, int threads)
    {
        var dictionary = new Dictionary<string, int>();
        var gate = new object();
        int success = 0;
        var watch = Stopwatch.StartNew();
        RunWorkers(threads, operationCount, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                string key = "k" + i;
                lock (gate)
                {
                    dictionary[key] = i;
                    _ = dictionary.ContainsKey(key);
                    if (i % 5 == 0)
                    {
                        dictionary.Remove(key);
                    }
                }

                Interlocked.Increment(ref success);
            }
        });
        watch.Stop();
        LastLockedMilliseconds = watch.ElapsedMilliseconds;
        return success;
    }

    public (long Threads10, long Threads50, long Threads100) MeasureScaling(int operationCount)
    {
        long time10 = RunConcurrent(operationCount, 10).Time;
        long time50 = RunConcurrent(operationCount, 50).Time;
        long time100 = RunConcurrent(operationCount, 100).Time;
        return (time10, time50, time100);
    }

    public (long Concurrent, long Locked) CompareLoad(int operationCount, int threads)
    {
        long concurrent = RunConcurrent(operationCount, threads).Time;
        RunLocked(operationCount, threads);
        return (concurrent, LastLockedMilliseconds);
    }

    public (long Concurrent, long Locked) CompareReads(int itemCount, int readers)
    {
        var concurrent = new ConcurrentDictionary<string, int>();
        var locked = new Dictionary<string, int>();
        var gate = new object();
        for (int i = 0; i < itemCount; i++)
        {
            concurrent.TryAdd("k" + i, i);
            locked["k" + i] = i;
        }

        long concurrentTime = TimeReaders(readers, () =>
        {
            int sum = 0;
            for (int n = 0; n < 30; n++)
            {
                foreach (KeyValuePair<string, int> pair in concurrent)
                {
                    sum += pair.Value;
                }
            }

            Interlocked.Add(ref _readSum, sum);
        });

        long lockedTime = TimeReaders(readers, () =>
        {
            int sum = 0;
            lock (gate)
            {
                for (int n = 0; n < 30; n++)
                {
                    foreach (KeyValuePair<string, int> pair in locked)
                    {
                        sum += pair.Value;
                    }
                }
            }

            Interlocked.Add(ref _readSum, sum);
        });

        return (concurrentTime, lockedTime);
    }

    private int _readSum;

    private static (long Time, int Success, int Failed) RunConcurrent(int operationCount, int threads)
    {
        var dictionary = new ConcurrentDictionary<string, int>();
        int success = 0;
        int failed = 0;
        var watch = Stopwatch.StartNew();
        RunWorkers(threads, operationCount, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                string key = "k" + (i % (operationCount / 2 + 1));
                if (dictionary.TryAdd(key, i))
                {
                    Interlocked.Increment(ref success);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                }

                if (dictionary.TryGetValue(key, out _))
                {
                    Interlocked.Increment(ref success);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                }

                if (i % 5 == 0)
                {
                    if (dictionary.TryRemove(key, out _))
                    {
                        Interlocked.Increment(ref success);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                    }
                }
            }
        });
        watch.Stop();
        return (watch.ElapsedMilliseconds, success, failed);
    }

    private static long TimeReaders(int readers, System.Action read)
    {
        var workers = new Thread[readers];
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < readers; i++)
        {
            workers[i] = new Thread(() => read());
            workers[i].Start();
        }

        for (int i = 0; i < workers.Length; i++)
        {
            workers[i].Join();
        }

        watch.Stop();
        return watch.ElapsedMilliseconds;
    }

    private static void RunWorkers(int threads, int operationCount, System.Action<int, int> work)
    {
        if (threads > operationCount)
        {
            threads = operationCount;
        }

        var workers = new Thread[threads];
        int chunk = operationCount / threads;
        for (int t = 0; t < threads; t++)
        {
            int start = t * chunk;
            int end = t == threads - 1 ? operationCount : start + chunk;
            workers[t] = new Thread(() => work(start, end));
            workers[t].Start();
        }

        for (int t = 0; t < workers.Length; t++)
        {
            workers[t].Join();
        }
    }
}
