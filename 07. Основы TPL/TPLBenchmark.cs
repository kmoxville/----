using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TplBasics;

public class TPLBenchmark
{
    public (long Time, int Success, bool Correct) BenchmarkTaskRun(int operationCount, int threadCount)
    {
        var tasks = new Task<int>[threadCount];
        int chunk = operationCount / threadCount;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < threadCount; i++)
        {
            int count = i == threadCount - 1 ? operationCount - (i * chunk) : chunk;
            tasks[i] = Task.Run(() => Count(count));
        }

        int[] parts = Task.WhenAll(tasks).Result;
        watch.Stop();
        int success = Total(parts);
        return (watch.ElapsedMilliseconds, success, success == operationCount);
    }

    public (long Time, int Success) BenchmarkTaskFactory(int operationCount, int threadCount)
    {
        var tasks = new Task<int>[threadCount];
        int chunk = operationCount / threadCount;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < threadCount; i++)
        {
            int count = i == threadCount - 1 ? operationCount - (i * chunk) : chunk;
            tasks[i] = Task.Factory.StartNew(
                () => Count(count),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        int[] parts = Task.WhenAll(tasks).Result;
        watch.Stop();
        return (watch.ElapsedMilliseconds, Total(parts));
    }

    public (long Time, int Success) BenchmarkThreadAPI(int operationCount, int threadCount)
    {
        var results = new int[threadCount];
        var threads = new Thread[threadCount];
        int chunk = operationCount / threadCount;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            int count = i == threadCount - 1 ? operationCount - (i * chunk) : chunk;
            threads[i] = new Thread(() => results[index] = Count(count));
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }

        watch.Stop();
        return (watch.ElapsedMilliseconds, Total(results));
    }

    private static int Count(int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++)
        {
            value++;
        }

        return value;
    }

    private static int Total(int[] parts)
    {
        int sum = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            sum += parts[i];
        }

        return sum;
    }
}
