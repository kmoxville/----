using System;
using System.Threading;
using System.Threading.Tasks;

namespace TplBasics;

public class TaskChainBuilder
{
    private readonly TaskDataProcessor _processor = new TaskDataProcessor();

    public decimal[] BuildProcessingChain(decimal[] data)
    {
        int quarter = data.Length / 4;
        var result = new decimal[data.Length];

        Task step1 = Task.Factory.StartNew(
            () => Fill(data, result, 0, quarter),
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        Task step2 = step1.ContinueWith(
            _ => Fill(data, result, quarter, quarter * 2),
            TaskContinuationOptions.OnlyOnRanToCompletion);

        Task step3 = step2.ContinueWith(
            _ => Fill(data, result, quarter * 2, quarter * 3),
            TaskContinuationOptions.ExecuteSynchronously);

        Task step4 = step3.ContinueWith(
            _ => Fill(data, result, quarter * 3, data.Length),
            TaskContinuationOptions.OnlyOnRanToCompletion);

        Task faulted = step1.ContinueWith(
            finished =>
            {
                AggregateException? error = finished.Exception;
                error?.Flatten().Handle(item => item is InvalidOperationException);
            },
            TaskContinuationOptions.OnlyOnFaulted);

        using var source = new CancellationTokenSource();
        source.Cancel();
        Task canceledStart = Task.Run(() => 0, source.Token);
        Task canceled = canceledStart.ContinueWith(
            finished =>
            {
                _ = finished.IsCanceled;
            },
            TaskContinuationOptions.OnlyOnCanceled);

        Task.WaitAll(step4, canceled);
        _ = faulted.Status;
        return result;
    }

    public decimal BuildComplexChain(decimal[] data)
    {
        int third = data.Length / 3;
        var tasks = new Task<decimal>[3];
        tasks[0] = Task.Run(() => Sum(data, 0, third));
        tasks[1] = Task.Run(() => Sum(data, third, third * 2));
        tasks[2] = Task.Run(() => Sum(data, third * 2, data.Length));

        Task<decimal> first = Task.WhenAny(tasks).Result;
        decimal[] parts = Task.WhenAll(tasks).Result;
        decimal total = first.Result;
        for (int i = 0; i < parts.Length; i++)
        {
            total += parts[i];
        }

        return total - first.Result;
    }

    public decimal[][] BuildParallelChain(decimal[][] datasets)
    {
        var tasks = new Task<decimal[]>[datasets.Length];
        for (int i = 0; i < datasets.Length; i++)
        {
            decimal[] set = datasets[i];
            tasks[i] = _processor.ProcessDataAsync(set);
        }

        return Task.WhenAll(tasks).Result;
    }

    public decimal[] BuildRetryChain(decimal[] data, int maxRetries)
    {
        var attempt = new int[1];
        Task<decimal[]> current = Task.Run(() => RunOnce(data, attempt));
        for (int i = 0; i < maxRetries; i++)
        {
            current = current.ContinueWith(previous =>
            {
                if (previous.Status == TaskStatus.RanToCompletion)
                {
                    return Task.FromResult(previous.Result);
                }

                previous.Exception?.Flatten().Handle(error => error is InvalidOperationException);
                return Task.Run(() => RunOnce(data, attempt));
            }).Unwrap();
        }

        return current.Result;
    }

    private decimal[] RunOnce(decimal[] data, int[] attempt)
    {
        int number = Interlocked.Increment(ref attempt[0]);
        if (number == 1)
        {
            throw new InvalidOperationException("повтор");
        }

        return _processor.ProcessDataInParallel(data, 2);
    }

    private static void Fill(decimal[] data, decimal[] result, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            result[i] = TaskDataProcessor.Compute(data[i]);
        }
    }

    private static decimal Sum(decimal[] data, int start, int end)
    {
        decimal sum = 0;
        for (int i = start; i < end; i++)
        {
            sum += TaskDataProcessor.Compute(data[i]);
        }

        return sum;
    }
}
