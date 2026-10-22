using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace AtomicOperations;

public static class Program
{
    public static void Main()
    {
        var sample = new AtomicCounter(0);
        long beforeIncrement = sample.GetAndIncrement();
        long afterAdd = sample.Add(4);
        long beforeExchange = sample.Exchange(3);
        long beforeCompare = sample.CompareExchange(8, 3);
        long beforeDecrement = sample.GetAndDecrement();
        sample.Reset();
        bool api =
            beforeIncrement == 0 &&
            afterAdd == 5 &&
            beforeExchange == 5 &&
            beforeCompare == 3 &&
            beforeDecrement == 8 &&
            sample.Value == 0;

        var benchmark = new AtomicBenchmark();
        var interlocked = benchmark.BenchmarkInterlockedCounter(1_000_000, 100);
        var locked = benchmark.BenchmarkLockedCounter(1_000_000, 100);
        var stack = benchmark.BenchmarkLockFreeStack(100_000, 50);
        var scaling = benchmark.MeasureScaling(200_000);
        var singleInterlocked = benchmark.BenchmarkInterlockedCounter(1_000_000, 1);
        var singleLocked = benchmark.BenchmarkLockedCounter(1_000_000, 1);

        var statistics = new StatisticsTracker();
        int requests = 50_000;
        int statisticsThreads = 200;
        var watch = Stopwatch.StartNew();
        RunStatistics(statistics, requests, statisticsThreads);
        watch.Stop();
        bool statisticsCorrect =
            statistics.TotalRequests == requests &&
            statistics.SuccessfulRequests + statistics.FailedRequests == requests;

        double speedup = Speedup(locked.Time, interlocked.Time);
        double overhead = Overhead(interlocked.Time, locked.Time);
        double singleSpeedup = Speedup(singleLocked.Time, singleInterlocked.Time);
        double singleOverhead = Overhead(singleInterlocked.Time, singleLocked.Time);
        string countText = 1_000_000.ToString("N0", CultureInfo.InvariantCulture);

        Console.WriteLine("=== Сравнение атомарных операций и блокировок ===");
        Console.WriteLine("Interlocked Counter:");
        Console.WriteLine($"  Время выполнения: {interlocked.Time} мс");
        Console.WriteLine($"  Итоговое значение: {interlocked.Value}");
        Console.WriteLine($"  Корректность: {Yes(interlocked.Correct)}");
        Console.WriteLine();
        Console.WriteLine("Locked Counter (с lock):");
        Console.WriteLine($"  Время выполнения: {locked.Time} мс");
        Console.WriteLine($"  Итоговое значение: {locked.Value}");
        Console.WriteLine($"  Корректность: {Yes(locked.Correct)}");
        Console.WriteLine();
        Console.WriteLine("Lock-Free Stack:");
        Console.WriteLine($"  Время выполнения: {stack.Time} мс");
        Console.WriteLine($"  Успешные операции: {stack.Success}");
        Console.WriteLine();
        Console.WriteLine($"Ускорение Interlocked vs Locked: {Format(speedup)}x");
        Console.WriteLine();

        Console.WriteLine("=== Результаты тестирования атомарных операций ===");
        Console.WriteLine("Interlocked Counter:");
        Console.WriteLine($"  Количество операций: {countText}");
        Console.WriteLine("  Количество потоков: 100");
        Console.WriteLine($"  Время выполнения: {interlocked.Time} мс");
        Console.WriteLine($"  Итоговое значение: {interlocked.Value}");
        Console.WriteLine($"  Корректность: {Yes(interlocked.Correct)}");
        Console.WriteLine();
        Console.WriteLine("Locked Counter:");
        Console.WriteLine($"  Количество операций: {countText}");
        Console.WriteLine("  Количество потоков: 100");
        Console.WriteLine($"  Время выполнения: {locked.Time} мс");
        Console.WriteLine($"  Итоговое значение: {locked.Value}");
        Console.WriteLine($"  Корректность: {Yes(locked.Correct)}");
        Console.WriteLine();
        Console.WriteLine("Lock-Free Stack:");
        Console.WriteLine("  Количество операций: 100,000");
        Console.WriteLine("  Количество потоков: 50");
        Console.WriteLine($"  Время выполнения: {stack.Time} мс");
        Console.WriteLine($"  Успешные операции: {stack.Success}");
        Console.WriteLine($"  Корректность: {Yes(stack.Correct)}");
        Console.WriteLine();
        Console.WriteLine("Statistics Tracker:");
        Console.WriteLine("  Количество запросов: 50,000");
        Console.WriteLine("  Количество потоков: 200");
        Console.WriteLine($"  Время выполнения: {watch.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Успешные запросы: {statistics.SuccessfulRequests}");
        Console.WriteLine($"  Неудачные запросы: {statistics.FailedRequests}");
        Console.WriteLine($"  Процент успеха: {Format(statistics.GetSuccessRate())}%");
        Console.WriteLine($"  Среднее время: {Format(statistics.GetAverageProcessingTime())} мс");
        Console.WriteLine();
        Console.WriteLine("Сравнение производительности:");
        Console.WriteLine($"  Interlocked vs Locked на 100 потоках: {Format(speedup)}x");
        Console.WriteLine($"  Накладные расходы блокировок на 100 потоках: {Format(overhead)}%");
        Console.WriteLine($"  Один поток: Interlocked {singleInterlocked.Time} мс, lock {singleLocked.Time} мс, ускорение {Format(singleSpeedup)}x, накладные расходы {Format(singleOverhead)}%");
        Console.WriteLine($"  Потоки 10/50/100/200: {scaling.Threads10} мс / {scaling.Threads50} мс / {scaling.Threads100} мс / {scaling.Threads200} мс");

        bool ok = api && interlocked.Correct && locked.Correct && stack.Correct && statisticsCorrect;
        Console.WriteLine($"Проблемы синхронизации: {(ok ? "Нет" : "Да")}");
    }

    private static void RunStatistics(StatisticsTracker statistics, int requests, int threadCount)
    {
        var workers = new Thread[threadCount];
        int chunk = requests / threadCount;
        for (int t = 0; t < threadCount; t++)
        {
            int start = t * chunk;
            int end = t == threadCount - 1 ? requests : start + chunk;
            int seed = 42 + t;
            workers[t] = new Thread(() =>
            {
                var random = new Random(seed);
                for (int i = start; i < end; i++)
                {
                    bool success = random.Next(0, 2) == 1;
                    long time = random.Next(1, 11);
                    statistics.RecordRequest(success, time);
                }
            });
            workers[t].Start();
        }

        for (int t = 0; t < workers.Length; t++)
        {
            workers[t].Join();
        }
    }

    private static string Yes(bool value)
    {
        return value ? "Да" : "Нет";
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static double Speedup(long lockedMs, long interlockedMs)
    {
        if (interlockedMs == 0)
        {
            return lockedMs == 0 ? 1 : lockedMs;
        }

        return (double)lockedMs / interlockedMs;
    }

    private static double Overhead(long interlockedMs, long lockedMs)
    {
        if (interlockedMs == 0)
        {
            return 0;
        }

        return (lockedMs - interlockedMs) * 100.0 / interlockedMs;
    }
}
