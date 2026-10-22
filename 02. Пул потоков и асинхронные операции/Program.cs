using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace ThreadPoolAsync;

public static class Program
{
    public static void Main(string[] args)
    {
        int dataSize = args.Length > 0 ? int.Parse(args[0]) : 10_000_000;
        var random = new Random(42);
        var data = new decimal[dataSize];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (decimal)(1.0 + random.NextDouble() * 999.0);
        }

        var watch = Stopwatch.StartNew();
        decimal[] sequential = TaskProcessor.ProcessDataSequential(data);
        watch.Stop();
        long sequentialMs = watch.ElapsedMilliseconds;

        watch.Restart();
        decimal[] threadPoolResult = TaskProcessor.ProcessDataWithThreadPool(data);
        watch.Stop();
        long threadPoolMs = watch.ElapsedMilliseconds;

        watch.Restart();
        decimal[] tapResult = TaskProcessor.ProcessDataAsync(data).Result;
        watch.Stop();
        long tapMs = watch.ElapsedMilliseconds;

        watch.Restart();
        decimal[] apmResult = TaskProcessor.ProcessDataWithAPM(data);
        watch.Stop();
        long apmMs = watch.ElapsedMilliseconds;

        bool match = Same(sequential, threadPoolResult)
            && Same(sequential, tapResult)
            && Same(sequential, apmResult);

        AsyncLogger.LogAsync("ThreadPool: " + threadPoolMs + " мс, TAP: " + tapMs + " мс").Wait();
        using var logged = new ManualResetEvent(false);
        AsyncLogger.LogWithCallback("APM: " + apmMs + " мс", () => logged.Set());
        logged.WaitOne();

        string sizeText = dataSize.ToString("N0", CultureInfo.InvariantCulture);
        Console.WriteLine("=== Результаты обработки ===");
        Console.WriteLine($"Размер данных: {sizeText} элементов");
        Console.WriteLine($"Последовательная обработка: {sequentialMs} мс");
        Console.WriteLine($"ThreadPool обработка: {threadPoolMs} мс");
        Console.WriteLine($"TAP обработка: {tapMs} мс");
        Console.WriteLine($"APM обработка: {apmMs} мс");
        Console.WriteLine($"Ускорение ThreadPool: {Speedup(sequentialMs, threadPoolMs)}x");
        Console.WriteLine($"Ускорение TAP: {Speedup(sequentialMs, tapMs)}x");
        Console.WriteLine($"Ускорение APM: {Speedup(sequentialMs, apmMs)}x");
        Console.WriteLine($"Результаты совпадают: {(match ? "Да" : "Нет")}");
    }

    private static bool Same(decimal[] left, decimal[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (Math.Abs(left[i] - right[i]) > 0.0001m)
            {
                return false;
            }
        }

        return true;
    }

    private static string Speedup(long sequentialMs, long parallelMs)
    {
        double speedup = parallelMs == 0 ? sequentialMs : (double)sequentialMs / parallelMs;
        return speedup.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
