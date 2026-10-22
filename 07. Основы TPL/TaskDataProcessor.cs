using System;
using System.Threading;
using System.Threading.Tasks;

namespace TplBasics;

public class TaskDataProcessor
{
    public Task<decimal[]> ProcessDataAsync(decimal[] data)
    {
        return Task.Run(() => ProcessRange(data, 0, data.Length));
    }

    public decimal[] ProcessDataInParallel(decimal[] data, int degreeOfParallelism)
    {
        var result = new decimal[data.Length];
        var tasks = new Task[degreeOfParallelism];
        int chunk = data.Length / degreeOfParallelism;
        for (int i = 0; i < degreeOfParallelism; i++)
        {
            int start = i * chunk;
            int end = i == degreeOfParallelism - 1 ? data.Length : start + chunk;
            tasks[i] = Task.Run(() => Fill(data, result, start, end));
        }

        Task.WhenAll(tasks).Wait();
        return result;
    }

    public Task<decimal[]> ProcessDataWithFactory(decimal[] data)
    {
        int parts = 4;
        var result = new decimal[data.Length];
        var tasks = new Task[parts];
        int chunk = data.Length / parts;
        for (int i = 0; i < parts; i++)
        {
            int start = i * chunk;
            int end = i == parts - 1 ? data.Length : start + chunk;
            tasks[i] = Task.Factory.StartNew(
                () => Fill(data, result, start, end),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
        }

        return Task.WhenAll(tasks).ContinueWith(finished =>
        {
            finished.GetAwaiter().GetResult();
            return result;
        });
    }

    public Task<decimal[]> ProcessDataWithCancellation(decimal[] data, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            () =>
            {
                var result = new decimal[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result[i] = Compute(data[i]);
                }

                return result;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public decimal[] ProcessDataWithTimeout(decimal[] data, TimeSpan timeout)
    {
        using var source = new CancellationTokenSource(timeout);
        Task<decimal[]> task = ProcessDataWithCancellation(data, source.Token);
        try
        {
            return task.Result;
        }
        catch (AggregateException exception)
        {
            exception.Flatten().Handle(error => error is OperationCanceledException);
            throw new OperationCanceledException();
        }
    }

    public static decimal Compute(decimal value)
    {
        double number = (double)value;
        return (decimal)(Math.Sqrt(number) * Math.Log10(number + 1));
    }

    private static decimal[] ProcessRange(decimal[] data, int start, int end)
    {
        var result = new decimal[end - start];
        for (int i = start; i < end; i++)
        {
            result[i - start] = Compute(data[i]);
        }

        return result;
    }

    private static void Fill(decimal[] data, decimal[] result, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            result[i] = Compute(data[i]);
        }
    }
}
