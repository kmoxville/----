using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace ConcurrentCollections;

public static class Program
{
    public static void Main()
    {
        var catalog = new ConcurrentLibraryCatalog();
        int added = 0;
        int addFailed = 0;
        var fillers = new Thread[50];
        for (int t = 0; t < fillers.Length; t++)
        {
            int thread = t;
            fillers[t] = new Thread(() =>
            {
                var random = new Random(42 + thread);
                for (int i = 0; i < 20; i++)
                {
                    string title = "Book " + (thread * 20 + i);
                    if (catalog.AddBook(title, "Author " + random.Next(40)))
                    {
                        Interlocked.Increment(ref added);
                    }
                    else
                    {
                        Interlocked.Increment(ref addFailed);
                    }
                }
            });
            fillers[t].Start();
        }

        for (int t = 0; t < fillers.Length; t++)
        {
            fillers[t].Join();
        }

        bool bookFound = catalog.TryGetBook("Book 0", out _);
        bool renamed = catalog.UpdateBook("Book 1", "Book 1", "Updated");
        int foundBySearch = catalog.SearchBooks("Book").Count;
        bool intact = catalog.GetBookCount() == 1000 && added == 1000 && bookFound && renamed && foundBySearch == 1000;

        var benchmark = new CollectionBenchmark();
        var concurrent = benchmark.BenchmarkConcurrentDictionary(1000);
        var blocking = benchmark.BenchmarkBlockingCollection(1000, 10);
        var synchronized = benchmark.BenchmarkSynchronizedDictionary(1000);
        var heavy = benchmark.CompareLoad(200000, 100);
        var reads = benchmark.CompareReads(20000, 100);
        var scaling = benchmark.MeasureScaling(20000);

        var cache = new ConcurrentCache();
        int cacheSuccess = 0;
        var cacheThreads = new Thread[20];
        var cacheWatch = Stopwatch.StartNew();
        for (int t = 0; t < cacheThreads.Length; t++)
        {
            int thread = t;
            cacheThreads[t] = new Thread(() =>
            {
                for (int i = 0; i < 25; i++)
                {
                    string key = "cache-" + (thread * 25 + i);
                    cache.AddToCache(key, "value " + i);
                    if (cache.TryGetFromCache(key, out _))
                    {
                        Interlocked.Increment(ref cacheSuccess);
                    }
                }
            });
            cacheThreads[t].Start();
        }

        for (int t = 0; t < cacheThreads.Length; t++)
        {
            cacheThreads[t].Join();
        }

        cacheWatch.Stop();
        bool cacheIntact = cache.GetCacheSize() == 500 && cacheSuccess == 500;

        double speedup = Speedup(reads.Locked, reads.Concurrent);
        double overhead = Overhead(reads.Concurrent, reads.Locked);

        Console.WriteLine("=== Сравнение потокобезопасных коллекций ===");
        Console.WriteLine("ConcurrentDictionary:");
        Console.WriteLine($"  Время выполнения: {concurrent.Time} мс");
        Console.WriteLine($"  Успешные операции: {concurrent.Success}");
        Console.WriteLine($"  Производительность: {Rate(1000, concurrent.Time)} операций/сек");
        Console.WriteLine();
        Console.WriteLine("BlockingCollection:");
        Console.WriteLine($"  Время выполнения: {blocking.Time} мс");
        Console.WriteLine($"  Обработанные задачи: {blocking.Processed}");
        Console.WriteLine($"  Производительность: {Rate(blocking.Processed, blocking.Time)} задач/сек");
        Console.WriteLine();
        Console.WriteLine("Synchronized Dictionary (с lock):");
        Console.WriteLine($"  Время выполнения: {synchronized.Time} мс");
        Console.WriteLine($"  Успешные операции: {synchronized.Success}");
        Console.WriteLine($"  Производительность: {Rate(1000, synchronized.Time)} операций/сек");
        Console.WriteLine();
        Console.WriteLine($"Ускорение ConcurrentDictionary vs Synchronized: {Format(speedup)}x");
        Console.WriteLine();

        Console.WriteLine("=== Результаты тестирования потокобезопасных коллекций ===");
        Console.WriteLine("ConcurrentDictionary:");
        Console.WriteLine("  Количество операций: 1000");
        Console.WriteLine($"  Время выполнения: {concurrent.Time} мс");
        Console.WriteLine($"  Успешные операции: {concurrent.Success}");
        Console.WriteLine($"  Производительность: {Rate(1000, concurrent.Time)} операций/сек");
        Console.WriteLine($"  Целостность данных: {(intact ? "Да" : "Нет")}");
        Console.WriteLine();
        Console.WriteLine("BlockingCollection:");
        Console.WriteLine("  Количество задач: 1000");
        Console.WriteLine("  Количество обработчиков: 10");
        Console.WriteLine($"  Время выполнения: {blocking.Time} мс");
        Console.WriteLine($"  Обработанные задачи: {blocking.Processed}");
        Console.WriteLine($"  Производительность: {Rate(blocking.Processed, blocking.Time)} задач/сек");
        Console.WriteLine();
        Console.WriteLine("ConcurrentCache:");
        Console.WriteLine("  Количество операций: 500");
        Console.WriteLine($"  Время выполнения: {cacheWatch.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Успешные операции: {cacheSuccess}");
        Console.WriteLine();
        Console.WriteLine("Сравнение производительности:");
        Console.WriteLine($"  ConcurrentDictionary vs Synchronized Dictionary: {Format(speedup)}x");
        Console.WriteLine($"  Накладные расходы синхронизации: {Format(overhead)}%");
        Console.WriteLine($"  200000 записей и 100 потоков, ConcurrentDictionary: {heavy.Concurrent} мс, Dictionary с lock: {heavy.Locked} мс");
        Console.WriteLine($"  100 потоков читают 20000 книг, ConcurrentDictionary: {reads.Concurrent} мс, Dictionary с lock: {reads.Locked} мс");
        Console.WriteLine($"  Потоки 10/50/100: {scaling.Threads10} мс / {scaling.Threads50} мс / {scaling.Threads100} мс");
        Console.WriteLine($"Проблемы синхронизации: {(intact && cacheIntact && blocking.Processed == 1000 && addFailed == 0 ? "Нет" : "Да")}");
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Rate(int count, long milliseconds)
    {
        double seconds = milliseconds / 1000.0;
        if (seconds == 0)
        {
            seconds = 0.001;
        }

        return (count / seconds).ToString("0", CultureInfo.InvariantCulture);
    }

    private static double Speedup(long synchronizedMs, long concurrentMs)
    {
        if (concurrentMs == 0)
        {
            return synchronizedMs == 0 ? 1 : synchronizedMs;
        }

        return (double)synchronizedMs / concurrentMs;
    }

    private static double Overhead(long concurrentMs, long synchronizedMs)
    {
        if (concurrentMs == 0)
        {
            return 0;
        }

        return (synchronizedMs - concurrentMs) * 100.0 / concurrentMs;
    }
}
