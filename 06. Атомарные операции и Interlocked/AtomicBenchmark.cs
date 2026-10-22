using System.Diagnostics;
using System.Threading;

namespace AtomicOperations;

public class AtomicBenchmark
{
    public (long Time, long Value, bool Correct) BenchmarkInterlockedCounter(int operationCount, int threadCount)
    {
        var counter = new AtomicCounter(0);
        var watch = Stopwatch.StartNew();
        Run(threadCount, operationCount, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                if ((i & 1) == 0)
                {
                    counter.Increment();
                }
                else
                {
                    counter.Decrement();
                }
            }
        });
        watch.Stop();
        long value = counter.Value;
        return (watch.ElapsedMilliseconds, value, value == 0);
    }

    public (long Time, long Value, bool Correct) BenchmarkLockedCounter(int operationCount, int threadCount)
    {
        long value = 0;
        var gate = new object();
        var watch = Stopwatch.StartNew();
        Run(threadCount, operationCount, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                lock (gate)
                {
                    if ((i & 1) == 0)
                    {
                        value++;
                    }
                    else
                    {
                        value--;
                    }
                }
            }
        });
        watch.Stop();
        return (watch.ElapsedMilliseconds, value, value == 0);
    }

    public (long Time, int Success, bool Correct) BenchmarkLockFreeStack(int operationCount, int threadCount)
    {
        int pushes = operationCount / 2;
        int pops = operationCount - pushes;
        var stack = new LockFreeStack<int>();
        var seen = new int[pushes];
        int popped = 0;
        int duplicates = 0;

        var watch = Stopwatch.StartNew();
        Run(threadCount, pushes, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                stack.Push(i);
            }
        });
        Run(threadCount, pops, (start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                if (!stack.TryPop(out int value))
                {
                    continue;
                }

                Interlocked.Increment(ref popped);
                if ((uint)value >= (uint)pushes)
                {
                    Interlocked.Increment(ref duplicates);
                    continue;
                }

                int previous = Interlocked.CompareExchange(ref seen[value], 1, 0);
                if (previous != 0)
                {
                    Interlocked.Increment(ref duplicates);
                }
            }
        });
        watch.Stop();

        bool correct = stack.IsEmpty() && popped == pops && duplicates == 0 && !stack.TryPeek(out _);
        return (watch.ElapsedMilliseconds, pushes + popped, correct);
    }

    public (long Threads10, long Threads50, long Threads100, long Threads200) MeasureScaling(int operationCount)
    {
        long time10 = BenchmarkInterlockedCounter(operationCount, 10).Time;
        long time50 = BenchmarkInterlockedCounter(operationCount, 50).Time;
        long time100 = BenchmarkInterlockedCounter(operationCount, 100).Time;
        long time200 = BenchmarkInterlockedCounter(operationCount, 200).Time;
        return (time10, time50, time100, time200);
    }

    private static void Run(int threadCount, int operationCount, System.Action<int, int> work)
    {
        if (threadCount > operationCount)
        {
            threadCount = operationCount;
        }

        var workers = new Thread[threadCount];
        int chunk = operationCount / threadCount;
        for (int t = 0; t < threadCount; t++)
        {
            int start = t * chunk;
            int end = t == threadCount - 1 ? operationCount : start + chunk;
            workers[t] = new Thread(() => work(start, end));
            workers[t].Start();
        }

        for (int t = 0; t < workers.Length; t++)
        {
            workers[t].Join();
        }
    }
}
