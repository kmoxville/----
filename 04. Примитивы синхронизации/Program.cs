using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace SyncPrimitives;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "mutex-child")
        {
            bool entered = new CrossProcessSync().TryExecuteWithGlobalLock("LibraryCrossProcess", () => { }, 300);
            Console.WriteLine(entered ? "1" : "0");
            return;
        }

        var random = new Random(42);
        var catalog = new LibraryCatalog();
        for (int i = 0; i < 1000; i++)
        {
            catalog.AddBook("Book " + i, "Author " + random.Next(50));
        }

        var benchmark = new SynchronizationBenchmark();
        var rw = benchmark.BenchmarkReaderWriterLock(catalog, 50, 10);
        var pool = new ResourcePool(10);
        var semaphore = benchmark.BenchmarkSemaphore(pool, 100);
        var mutex = benchmark.BenchmarkMutex(20);
        var reads = benchmark.CompareReads(catalog, 50);
        var overhead = benchmark.MeasureOverhead();

        Console.WriteLine("=== Сравнение примитивов синхронизации ===");
        Console.WriteLine("ReaderWriterLockSlim:");
        Console.WriteLine($"  Чтение: {rw.ReadTime} мс");
        Console.WriteLine($"  Запись: {rw.WriteTime} мс");
        Console.WriteLine($"  Общее время: {rw.TotalTime} мс");
        Console.WriteLine();
        Console.WriteLine("SemaphoreSlim:");
        Console.WriteLine($"  Время выполнения: {semaphore.Time} мс");
        Console.WriteLine($"  Успешные запросы: {semaphore.Success}");
        Console.WriteLine($"  Неудачные запросы: {semaphore.Failed}");
        Console.WriteLine();
        Console.WriteLine("Mutex:");
        Console.WriteLine($"  Время выполнения: {mutex.Time} мс");
        Console.WriteLine($"  Успешные операции: {mutex.Success}");
        Console.WriteLine();

        Console.WriteLine("=== Результаты тестирования примитивов синхронизации ===");
        Console.WriteLine("ReaderWriterLockSlim:");
        Console.WriteLine("  Количество операций чтения: 50");
        Console.WriteLine("  Количество операций записи: 10");
        Console.WriteLine($"  Время чтения: {rw.ReadTime} мс");
        Console.WriteLine($"  Время записи: {rw.WriteTime} мс");
        Console.WriteLine($"  Целостность данных: {(rw.Intact ? "Да" : "Нет")}");
        Console.WriteLine();
        Console.WriteLine("SemaphoreSlim:");
        Console.WriteLine("  Количество запросов: 100");
        Console.WriteLine("  Доступных ресурсов: 10");
        Console.WriteLine($"  Успешные запросы: {semaphore.Success}");
        Console.WriteLine($"  Неудачные запросы: {semaphore.Failed}");
        Console.WriteLine();
        Console.WriteLine("Mutex:");
        Console.WriteLine("  Количество операций: 20");
        Console.WriteLine($"  Успешные операции: {mutex.Success}");
        Console.WriteLine($"  Время выполнения: {mutex.Time} мс");
        Console.WriteLine();
        Console.WriteLine("Сравнение производительности:");
        Console.WriteLine($"  Накладные расходы ReaderWriterLockSlim: {Format(overhead.Rw)}%");
        Console.WriteLine($"  Накладные расходы SemaphoreSlim: {Format(overhead.Semaphore)}%");
        Console.WriteLine($"  Накладные расходы Mutex: {Format(overhead.Mutex)}%");
        Console.WriteLine();
        Console.WriteLine($"50 читателей через ReaderWriterLockSlim: {reads.RwTime} мс");
        Console.WriteLine($"50 читателей через Mutex: {reads.ExclusiveTime} мс");
        Console.WriteLine();

        ShowTimeouts(catalog, pool);
        ShowCrossProcess();

        bool semaphoreClosed = pool.AvailableCount == pool.MaxResources && semaphore.Success + semaphore.Failed == 100;
        Console.WriteLine($"Проблемы синхронизации: {(rw.Intact && semaphoreClosed && mutex.Success == 20 ? "Нет" : "Да")}");

        catalog.Dispose();
        pool.Dispose();
    }

    private static void ShowTimeouts(LibraryCatalog catalog, ResourcePool pool)
    {
        var started = new ManualResetEvent(false);
        var finish = new ManualResetEvent(false);
        var holder = new Thread(() => catalog.HoldWriteLock(started, finish));
        holder.Start();
        started.WaitOne();

        bool addFailed = !catalog.TryAddBook("Late", "Nobody", 50);
        bool searchFailed = !catalog.TrySearchBooks("Book", 50);
        finish.Set();
        holder.Join();
        bool addLater = catalog.TryAddBook("Late", "Nobody", 500);

        for (int i = 0; i < pool.MaxResources; i++)
        {
            pool.AcquireResource();
        }

        bool takeFailed = !pool.TryAcquireResource(50);
        pool.ReleaseResource();
        bool takeLater = pool.TryAcquireResource(200);
        if (takeLater)
        {
            pool.ReleaseResource();
        }

        for (int i = 0; i < pool.MaxResources - 1; i++)
        {
            pool.ReleaseResource();
        }

        Console.WriteLine($"Таймаут записи, пока каталог занят: {(addFailed ? "не вошли" : "вошли")}");
        Console.WriteLine($"Таймаут чтения, пока каталог занят: {(searchFailed ? "не вошли" : "вошли")}");
        Console.WriteLine($"Запись после освобождения: {(addLater ? "успех" : "неудача")}");
        Console.WriteLine($"Таймаут ресурса, когда пул занят: {(takeFailed ? "не получили" : "получили")}");
        Console.WriteLine($"Ресурс после освобождения одного места: {(takeLater ? "успех" : "неудача")}");
        Console.WriteLine();

        started.Dispose();
        finish.Dispose();
    }

    private static void ShowCrossProcess()
    {
        var started = new ManualResetEvent(false);
        var finish = new ManualResetEvent(false);
        var sync = new CrossProcessSync();
        var holder = new Thread(() =>
        {
            sync.ExecuteWithGlobalLock("LibraryCrossProcess", () =>
            {
                started.Set();
                finish.WaitOne();
            });
        });
        holder.Start();
        started.WaitOne();

        bool sameProcess = sync.TryExecuteWithGlobalLock("LibraryCrossProcess", () => { }, 200);
        var child = Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = "mutex-child",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });
        string output = child!.StandardOutput.ReadToEnd().Trim();
        child.WaitForExit();
        finish.Set();
        holder.Join();

        Console.WriteLine($"Mutex в этом процессе, пока он занят: {(sameProcess ? "вошли" : "таймаут")}");
        Console.WriteLine($"Mutex в другом процессе: {(output == "0" ? "таймаут" : "вошли")}");
        Console.WriteLine();

        started.Dispose();
        finish.Dispose();
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
