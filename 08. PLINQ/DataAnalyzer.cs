using System;
using System.Collections.Generic;
using System.Linq;

namespace PlinqQueries;

public class AggregateResult
{
    public decimal Sum { get; set; }

    public decimal Average { get; set; }

    public decimal Max { get; set; }

    public decimal Min { get; set; }

    public int Count { get; set; }
}

public class DataAnalyzer
{
    public IEnumerable<MarketData> FilterSequential(IEnumerable<MarketData> data, decimal minPrice, decimal maxPrice)
    {
        return data.Where(item => item.Price >= minPrice && item.Price <= maxPrice);
    }

    public AggregateResult AggregateSequential(IEnumerable<MarketData> data)
    {
        var list = data.ToList();
        if (list.Count == 0)
        {
            return new AggregateResult();
        }

        return new AggregateResult
        {
            Sum = list.Sum(item => item.Price),
            Average = list.Average(item => item.Price),
            Max = list.Max(item => item.Price),
            Min = list.Min(item => item.Price),
            Count = list.Count
        };
    }

    public IEnumerable<decimal> TransformSequential(IEnumerable<MarketData> data)
    {
        var list = data.ToList();
        return Enumerable.Range(0, list.Count).Select(index => WindowAverage(list, index));
    }

    public IEnumerable<MarketData> JoinSequential(IEnumerable<MarketData> data1, IEnumerable<MarketData> data2)
    {
        return data1.Join(data2, left => left.Id, right => right.Id, (left, right) => left);
    }

    public IEnumerable<MarketData> FilterParallel(IEnumerable<MarketData> data, decimal minPrice, decimal maxPrice)
    {
        return data.AsParallel()
            .WithDegreeOfParallelism(4)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Where(item => item.Price >= minPrice && item.Price <= maxPrice);
    }

    public AggregateResult AggregateParallel(IEnumerable<MarketData> data)
    {
        var query = data.AsParallel().WithDegreeOfParallelism(4);
        int count = query.Count();
        if (count == 0)
        {
            return new AggregateResult();
        }

        return new AggregateResult
        {
            Sum = query.Sum(item => item.Price),
            Average = query.Average(item => item.Price),
            Max = query.Max(item => item.Price),
            Min = query.Min(item => item.Price),
            Count = count
        };
    }

    public IEnumerable<decimal> TransformParallel(IEnumerable<MarketData> data)
    {
        var list = data.ToList();
        var result = new decimal[list.Count];
        Enumerable.Range(0, list.Count)
            .AsParallel()
            .WithDegreeOfParallelism(4)
            .ForAll(index => result[index] = WindowAverage(list, index));
        return result;
    }

    public IEnumerable<MarketData> JoinParallel(IEnumerable<MarketData> data1, IEnumerable<MarketData> data2)
    {
        return data1.AsParallel()
            .AsOrdered()
            .WithDegreeOfParallelism(4)
            .Join(data2.AsParallel(), left => left.Id, right => right.Id, (left, right) => left);
    }

    private static decimal WindowAverage(List<MarketData> list, int index)
    {
        int from = Math.Max(0, index - 49);
        decimal sum = 0;
        int count = 0;
        for (int i = from; i <= index; i++)
        {
            sum += list[i].Price;
            count++;
        }

        return sum / count;
    }
}
