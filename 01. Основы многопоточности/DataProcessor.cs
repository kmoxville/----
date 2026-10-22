using System;
using System.Collections.Generic;
using System.Threading;

namespace MultithreadingBasics;

public static class DataProcessor
{
    public static decimal[] ProcessDataSequential(decimal[] data)
    {
        var result = new decimal[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = Compute(data[i]);
        }

        return result;
    }

    public static decimal[] ProcessDataParallel(decimal[] data, int threadCount)
    {
        var result = new decimal[data.Length];
        int baseCount = data.Length / threadCount;
        int remainder = data.Length % threadCount;
        var workItems = new WorkItem[threadCount];

        using var completed = new CountdownEvent(threadCount);

        int start = 0;
        for (int i = 0; i < threadCount; i++)
        {
            int length = baseCount + (i < remainder ? 1 : 0);
            int end = start + length;
            var workItem = new WorkItem(data, result, start, end, completed);
            workItems[i] = workItem;

            var thread = new Thread(ProcessRange);
            thread.IsBackground = true;
            thread.Start(workItem);
            start = end;
        }

        completed.Wait();

        var errors = new List<Exception>();
        for (int i = 0; i < workItems.Length; i++)
        {
            Exception? error = workItems[i].Error;
            if (error != null)
            {
                errors.Add(error);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(errors);
        }

        return result;
    }

    private static void ProcessRange(object? state)
    {
        var workItem = (WorkItem)state!;
        try
        {
            for (int i = workItem.Start; i < workItem.End; i++)
            {
                workItem.Result[i] = Compute(workItem.Source[i]);
            }
        }
        catch (Exception exception)
        {
            workItem.Error = exception;
        }
        finally
        {
            workItem.Completed.Signal();
        }
    }

    private static decimal Compute(decimal value)
    {
        double number = (double)value;
        return (decimal)(Math.Sqrt(number) * Math.Log10(number + 1));
    }

    private sealed class WorkItem
    {
        public WorkItem(decimal[] source, decimal[] result, int start, int end, CountdownEvent completed)
        {
            Source = source;
            Result = result;
            Start = start;
            End = end;
            Completed = completed;
        }

        public decimal[] Source { get; }
        public decimal[] Result { get; }
        public int Start { get; }
        public int End { get; }
        public CountdownEvent Completed { get; }
        public Exception? Error { get; set; }
    }
}
