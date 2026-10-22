using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PlinqQueries;

public static class Program
{
    public static void Main()
    {
        var generator = new DataGenerator();
        var optimizer = new QueryOptimizer();
        var benchmark = new PLINQBenchmark();

        List<MarketData> small = generator.GenerateMarketData(10_000);
        List<MarketData> large = generator.GenerateMarketData(1_000_000);
        var report = benchmark.CompareAllApproaches();
        var smallFilter = report.Small;
        var mediumFilter = report.Medium;
        var filter = report.Filter;
        var aggregation = report.Aggregation;
        var transformation = report.Transformation;
        Dictionary<int, long> degrees = report.Degrees;

        int optimized = optimizer.OptimizeQueryForLargeData(large).Count();
        bool orderKept = optimizer.OptimizeQueryForOrderDependent(large).First().Id == large[0].Id;
        int lowLatency = optimizer.OptimizeQueryForLowLatency(small).Count();
        int degree4 = optimizer.WithDegreeOfParallelism(large, 4).Count(item => item.Price > 0);
        int forced = optimizer.WithExecutionMode(small, ParallelExecutionMode.ForceParallelism).Count();
        int merged = optimizer.WithMergeOptions(small, ParallelMergeOptions.NotBuffered).Count();

        bool canceled = false;
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            try
            {
                optimizer.WithCancellation(large, source.Token).Count();
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (AggregateException exception)
            {
                canceled = exception.InnerException is OperationCanceledException;
            }
        }

        int bestDegree = 1;
        long bestTime = degrees[1];
        foreach (KeyValuePair<int, long> pair in degrees)
        {
            if (pair.Value < bestTime && pair.Value > 0)
            {
                bestTime = pair.Value;
                bestDegree = pair.Key;
            }
        }

        double optimizedSpeedup = Speedup(degrees[1], bestTime);
        bool match = filter.Match && aggregation.Match && transformation.Match && report.JoinMatch && orderKept;

        Console.WriteLine("=== Результаты тестирования PLINQ ===");
        PrintResult("Фильтрация данных (1,000,000 записей)", filter);
        PrintResult("Агрегация данных (1,000,000 записей)", aggregation);
        PrintResult("Трансформация данных (1,000,000 записей)", transformation);
        Console.WriteLine("Оптимизация запросов:");
        Console.WriteLine($"  Оптимальная степень параллелизма: {bestDegree} ядер");
        Console.WriteLine($"  Ускорение с оптимизацией: {Format(optimizedSpeedup)}x");
        Console.WriteLine();
        Console.WriteLine($"Малый объем 10,000: ускорение {Format(smallFilter.Speedup)}x");
        Console.WriteLine($"Средний объем 100,000: ускорение {Format(mediumFilter.Speedup)}x");
        Console.WriteLine($"Соединение 100,000 записей: {Yes(report.JoinMatch)}, строк {report.JoinCount}");
        Console.WriteLine($"Отмена запроса: {Yes(canceled)}");
        Console.WriteLine($"16 ядер: {degrees[16]} мс");
        Console.WriteLine($"Проблемы: {(match && canceled && optimized > 0 && degree4 == large.Count && forced == small.Count && lowLatency == small.Count && merged == small.Count ? "Нет" : "Да")}");
    }

    private static void PrintResult(string title, (long Sequential, long Parallel, double Speedup, bool Match) result)
    {
        Console.WriteLine(title + ":");
        Console.WriteLine($"  Последовательный запрос: {result.Sequential} мс");
        Console.WriteLine($"  Параллельный запрос: {result.Parallel} мс");
        Console.WriteLine($"  Ускорение: {Format(result.Speedup)}x");
        Console.WriteLine($"  Результаты совпадают: {Yes(result.Match)}");
        Console.WriteLine();
    }

    private static string Yes(bool value)
    {
        return value ? "Да" : "Нет";
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static double Speedup(long sequential, long parallel)
    {
        if (parallel == 0)
        {
            return sequential == 0 ? 1 : sequential;
        }

        return (double)sequential / parallel;
    }
}
