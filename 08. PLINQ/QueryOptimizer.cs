using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PlinqQueries;

public class QueryOptimizer
{
    public ParallelQuery<MarketData> WithDegreeOfParallelism(IEnumerable<MarketData> data, int degree)
    {
        return data.AsParallel().WithDegreeOfParallelism(degree);
    }

    public ParallelQuery<MarketData> WithExecutionMode(IEnumerable<MarketData> data, ParallelExecutionMode mode)
    {
        return data.AsParallel().WithExecutionMode(mode);
    }

    public ParallelQuery<MarketData> WithMergeOptions(IEnumerable<MarketData> data, ParallelMergeOptions options)
    {
        return data.AsParallel().WithMergeOptions(options);
    }

    public ParallelQuery<MarketData> WithCancellation(IEnumerable<MarketData> data, CancellationToken cancellationToken)
    {
        return data.AsParallel().WithCancellation(cancellationToken);
    }

    public IEnumerable<MarketData> OptimizeQueryForLargeData(IEnumerable<MarketData> data)
    {
        return data.AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
            .Where(item => item.Price >= 100 && item.Price <= 500);
    }

    public IEnumerable<MarketData> OptimizeQueryForOrderDependent(IEnumerable<MarketData> data)
    {
        return data.AsParallel()
            .AsOrdered()
            .WithMergeOptions(ParallelMergeOptions.FullyBuffered)
            .Select(item => item);
    }

    public IEnumerable<MarketData> OptimizeQueryForLowLatency(IEnumerable<MarketData> data)
    {
        return data.AsParallel()
            .WithDegreeOfParallelism(2)
            .WithMergeOptions(ParallelMergeOptions.NotBuffered)
            .Where(item => item.Volume > 0);
    }
}
