using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace TplBasics;

public static class Program
{
    public static void Main()
    {
        var random = new Random(42);
        var data = new decimal[10_000_000];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (decimal)(1.0 + random.NextDouble() * 999.0);
        }

        var processor = new TaskDataProcessor();
        decimal[] sequential = processor.ProcessDataAsync(data).Result;

        var watch = Stopwatch.StartNew();
        decimal[] parallel = processor.ProcessDataInParallel(data, 4);
        watch.Stop();
        long parallelMs = watch.ElapsedMilliseconds;
        decimal parallelSum = Sum(parallel);
        bool parallelCorrect = Same(sequential, parallel);

        watch.Restart();
        decimal[] factory = processor.ProcessDataWithFactory(data).Result;
        watch.Stop();
        bool factoryCorrect = Same(sequential, factory);

        var chain = new TaskChainBuilder();
        watch.Restart();
        decimal[] chained = chain.BuildProcessingChain(data);
        watch.Stop();
        long chainMs = watch.ElapsedMilliseconds;
        bool chainCorrect = Same(sequential, chained);

        decimal[] slice = new decimal[20_000];
        Array.Copy(data, slice, slice.Length);
        decimal complex = chain.BuildComplexChain(slice);
        decimal[][] sets = new decimal[3][];
        for (int i = 0; i < sets.Length; i++)
        {
            sets[i] = new decimal[10_000];
            Array.Copy(data, i * 10_000, sets[i], 0, sets[i].Length);
        }

        decimal[][] parallelSets = chain.BuildParallelChain(sets);
        decimal[] retried = chain.BuildRetryChain(slice, 3);
        bool chainsOk = complex > 0 && parallelSets.Length == 3 && retried.Length == slice.Length;

        var errors = new ExceptionHandlingDemo();
        int handled = errors.HandleExceptionsWithWait(slice)
            + errors.HandleExceptionsWithWhenAll(slice)
            + errors.HandleExceptionsWithContinueWith(slice);
        bool errorsOk = handled == 4;

        bool canceled = false;
        var cancelWatch = Stopwatch.StartNew();
        using (var source = new CancellationTokenSource())
        {
            Task<decimal[]> cancelTask = processor.ProcessDataWithCancellation(data, source.Token);
            Thread.Sleep(20);
            source.Cancel();
            try
            {
                cancelTask.Wait();
            }
            catch (AggregateException exception)
            {
                exception.Flatten().Handle(error => error is OperationCanceledException);
                canceled = true;
            }
        }

        cancelWatch.Stop();

        bool timedOut = false;
        try
        {
            processor.ProcessDataWithTimeout(data, TimeSpan.FromMilliseconds(30));
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
        }

        var benchmark = new TPLBenchmark();
        var taskRun = benchmark.BenchmarkTaskRun(10_000, 10_000);
        var taskFactory = benchmark.BenchmarkTaskFactory(10_000, 10_000);
        var threads = benchmark.BenchmarkThreadAPI(10_000, 10_000);
        long tasks100 = benchmark.BenchmarkTaskRun(100, 100).Time;
        long tasks1000 = benchmark.BenchmarkTaskRun(1000, 1000).Time;
        long tasks10000 = taskRun.Time;

        double speedup = Speedup(threads.Time, taskRun.Time);
        double overhead = Overhead(threads.Time, taskRun.Time);
        string sizeText = data.Length.ToString("N0", CultureInfo.InvariantCulture);

        Console.WriteLine("=== Сравнение подходов к параллельному программированию ===");
        Console.WriteLine("Task.Run:");
        Console.WriteLine($"  Время выполнения: {taskRun.Time} мс");
        Console.WriteLine($"  Успешные операции: {taskRun.Success}");
        Console.WriteLine($"  Производительность: {Rate(taskRun.Success, taskRun.Time)} операций/сек");
        Console.WriteLine();
        Console.WriteLine("Task.Factory.StartNew:");
        Console.WriteLine($"  Время выполнения: {taskFactory.Time} мс");
        Console.WriteLine($"  Успешные операции: {taskFactory.Success}");
        Console.WriteLine($"  Производительность: {Rate(taskFactory.Success, taskFactory.Time)} операций/сек");
        Console.WriteLine();
        Console.WriteLine("Thread API (ручное управление):");
        Console.WriteLine($"  Время выполнения: {threads.Time} мс");
        Console.WriteLine($"  Успешные операции: {threads.Success}");
        Console.WriteLine($"  Производительность: {Rate(threads.Success, threads.Time)} операций/сек");
        Console.WriteLine();
        Console.WriteLine($"Ускорение TPL vs Thread API: {Format(speedup)}x");
        Console.WriteLine();

        Console.WriteLine("=== Результаты тестирования TPL ===");
        Console.WriteLine("Обработка данных с Task.Run:");
        Console.WriteLine($"  Количество элементов: {sizeText}");
        Console.WriteLine($"  Время выполнения: {parallelMs} мс");
        Console.WriteLine($"  Итоговый результат: {parallelSum}");
        Console.WriteLine($"  Корректность: {Yes(parallelCorrect && factoryCorrect)}");
        Console.WriteLine();
        Console.WriteLine("Обработка данных с продолжениями:");
        Console.WriteLine("  Количество задач в цепочке: 4");
        Console.WriteLine($"  Время выполнения: {chainMs} мс");
        Console.WriteLine($"  Успешное завершение: {Yes(chainCorrect && chainsOk)}");
        Console.WriteLine();
        Console.WriteLine("Обработка исключений:");
        Console.WriteLine($"  Количество исключений: {handled}");
        Console.WriteLine($"  Обработано корректно: {Yes(errorsOk)}");
        Console.WriteLine();
        Console.WriteLine("Отмена операций:");
        Console.WriteLine($"  Операция отменена: {Yes(canceled)}");
        Console.WriteLine($"  Время до отмены: {cancelWatch.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Таймаут сработал: {Yes(timedOut)}");
        Console.WriteLine();
        Console.WriteLine("Сравнение производительности:");
        Console.WriteLine($"  Task.Run vs Thread API: {Format(speedup)}x");
        Console.WriteLine($"  Накладные расходы TPL: {Format(overhead)}%");
        Console.WriteLine($"  Задачи 100/1000/10000: {tasks100} мс / {tasks1000} мс / {tasks10000} мс");

        bool ok = parallelCorrect && factoryCorrect && chainCorrect && chainsOk && errorsOk && canceled && timedOut && taskRun.Correct;
        Console.WriteLine($"Проблемы: {(ok ? "Нет" : "Да")}");
    }

    private static decimal Sum(decimal[] values)
    {
        decimal sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        return sum;
    }

    private static bool Same(decimal[] left, decimal[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string Yes(bool value)
    {
        return value ? "Да" : "Нет";
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

    private static double Speedup(long threadMs, long taskMs)
    {
        if (taskMs == 0)
        {
            return threadMs == 0 ? 1 : threadMs;
        }

        return (double)threadMs / taskMs;
    }

    private static double Overhead(long threadMs, long taskMs)
    {
        if (threadMs == 0)
        {
            return 0;
        }

        return (taskMs - threadMs) * 100.0 / threadMs;
    }
}
