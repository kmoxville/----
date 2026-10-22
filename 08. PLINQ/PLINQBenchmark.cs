using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PlinqQueries;

public class PLINQBenchmark
{
    private readonly DataGenerator _generator = new DataGenerator();
    private readonly DataAnalyzer _analyzer = new DataAnalyzer();

    public (long Sequential, long Parallel, double Speedup, bool Match) BenchmarkFilter(int dataSize, int iterations)
    {
        List<MarketData> data = _generator.GenerateMarketData(dataSize);
        bool match = Same(
            _analyzer.FilterSequential(data, 100, 500),
            _analyzer.FilterParallel(data, 100, 500));
        long sequential = Measure(iterations, () => _analyzer.FilterSequential(data, 100, 500).Count());
        long parallel = Measure(iterations, () => _analyzer.FilterParallel(data, 100, 500).Count());
        return (sequential, parallel, Speedup(sequential, parallel), match);
    }

    public (long Sequential, long Parallel, double Speedup, bool Match) BenchmarkAggregation(int dataSize, int iterations)
    {
        List<MarketData> data = _generator.GenerateMarketData(dataSize);
        AggregateResult sequentialResult = _analyzer.AggregateSequential(data);
        AggregateResult parallelResult = _analyzer.AggregateParallel(data);
        bool match = sequentialResult.Count == parallelResult.Count
            && sequentialResult.Min == parallelResult.Min
            && sequentialResult.Max == parallelResult.Max
            && Math.Abs(sequentialResult.Sum - parallelResult.Sum) <= 0.01m;
        long sequential = Measure(iterations, () => _analyzer.AggregateSequential(data));
        long parallel = Measure(iterations, () => _analyzer.AggregateParallel(data));
        return (sequential, parallel, Speedup(sequential, parallel), match);
    }

    public (long Sequential, long Parallel, double Speedup, bool Match) BenchmarkTransformation(int dataSize, int iterations)
    {
        List<MarketData> data = _generator.GenerateMarketData(dataSize);
        bool match = SameValues(
            _analyzer.TransformSequential(data),
            _analyzer.TransformParallel(data));
        long sequential = Measure(iterations, () => _analyzer.TransformSequential(data).Count());
        long parallel = Measure(iterations, () => _analyzer.TransformParallel(data).Count());
        return (sequential, parallel, Speedup(sequential, parallel), match);
    }

    public Dictionary<int, long> BenchmarkDifferentDegrees(int dataSize)
    {
        List<MarketData> data = _generator.GenerateMarketData(dataSize);
        var times = new Dictionary<int, long>();
        int[] degrees = { 1, 2, 4, 8, 16 };
        for (int i = 0; i < degrees.Length; i++)
        {
            int degree = degrees[i];
            var watch = Stopwatch.StartNew();
            int count = data.AsParallel()
                .WithDegreeOfParallelism(degree)
                .Count(item => item.Price >= 100 && item.Price <= 500);
            watch.Stop();
            _ = count;
            times[degree] = watch.ElapsedMilliseconds;
        }

        return times;
    }

    public (
        (long Sequential, long Parallel, double Speedup, bool Match) Small,
        (long Sequential, long Parallel, double Speedup, bool Match) Medium,
        (long Sequential, long Parallel, double Speedup, bool Match) Filter,
        (long Sequential, long Parallel, double Speedup, bool Match) Aggregation,
        (long Sequential, long Parallel, double Speedup, bool Match) Transformation,
        Dictionary<int, long> Degrees,
        bool JoinMatch,
        int JoinCount) CompareAllApproaches()
    {
        var small = BenchmarkFilter(10_000, 1);
        var medium = BenchmarkFilter(100_000, 1);
        var filter = BenchmarkFilter(1_000_000, 1);
        var aggregation = BenchmarkAggregation(1_000_000, 1);
        var transformation = BenchmarkTransformation(1_000_000, 1);
        Dictionary<int, long> degrees = BenchmarkDifferentDegrees(1_000_000);
        List<MarketData> joined = _generator.GenerateMarketData(100_000);
        int joinCount = _analyzer.JoinParallel(joined, joined).Count();
        bool joinMatch = joinCount == joined.Count
            && _analyzer.JoinSequential(joined, joined).Count() == joinCount;

        Console.WriteLine("=== Сравнение последовательных и параллельных запросов ===");
        Print("Фильтрация данных", filter);
        Print("Агрегация данных", aggregation);
        Print("Трансформация данных", transformation);
        Console.WriteLine("Оптимальная степень параллелизма:");
        Console.WriteLine($"  1 ядро: {degrees[1]} мс");
        Console.WriteLine($"  2 ядра: {degrees[2]} мс");
        Console.WriteLine($"  4 ядра: {degrees[4]} мс");
        Console.WriteLine($"  8 ядер: {degrees[8]} мс");
        Console.WriteLine();

        return (small, medium, filter, aggregation, transformation, degrees, joinMatch, joinCount);
    }

    private static void Print(string title, (long Sequential, long Parallel, double Speedup, bool Match) result)
    {
        Console.WriteLine(title + ":");
        Console.WriteLine($"  Последовательный запрос: {result.Sequential} мс");
        Console.WriteLine($"  Параллельный запрос (4 ядра): {result.Parallel} мс");
        Console.WriteLine($"  Ускорение: {result.Speedup.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}x");
        Console.WriteLine();
    }

    private static long Measure(int iterations, Action action)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        watch.Stop();
        return watch.ElapsedMilliseconds;
    }

    private static bool Same(IEnumerable<MarketData> left, IEnumerable<MarketData> right)
    {
        var first = left.OrderBy(item => item.Id).ToList();
        var second = right.OrderBy(item => item.Id).ToList();
        if (first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].Id != second[i].Id)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameValues(IEnumerable<decimal> left, IEnumerable<decimal> right)
    {
        var first = left.ToList();
        var second = right.ToList();
        if (first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i] != second[i])
            {
                return false;
            }
        }

        return true;
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
