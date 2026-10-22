using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolAsync;

public static class TaskProcessor
{
    public static decimal[] ProcessDataSequential(decimal[] data)
    {
        var result = new decimal[data.Length];
        Fill(data, result, 0, data.Length);
        return result;
    }

    public static decimal[] ProcessDataWithThreadPool(decimal[] data)
    {
        return ProcessInParts(data, 8);
    }

    public static async Task<decimal[]> ProcessDataAsync(decimal[] data)
    {
        int parts = Environment.ProcessorCount;
        var result = new decimal[data.Length];
        var tasks = new Task[parts];
        int baseCount = data.Length / parts;
        int remainder = data.Length % parts;
        int start = 0;

        for (int i = 0; i < parts; i++)
        {
            int from = start;
            int length = baseCount + (i < remainder ? 1 : 0);
            int to = from + length;
            tasks[i] = Task.Run(() => Fill(data, result, from, to));
            start = to;
        }

        await Task.WhenAll(tasks);
        return result;
    }

    public static decimal[] ProcessDataWithAPM(decimal[] data)
    {
        IAsyncResult asyncResult = BeginProcessData(data, null, null);
        return EndProcessData(asyncResult);
    }

    public static IAsyncResult BeginProcessData(decimal[] data, AsyncCallback? callback, object? state)
    {
        var asyncResult = new ProcessAsyncResult(state);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                asyncResult.Complete(ProcessInParts(data, 8), null);
            }
            catch (Exception exception)
            {
                asyncResult.Complete(null, exception);
            }

            if (callback != null)
            {
                callback(asyncResult);
            }
        });
        return asyncResult;
    }

    public static decimal[] EndProcessData(IAsyncResult asyncResult)
    {
        var processResult = (ProcessAsyncResult)asyncResult;
        processResult.AsyncWaitHandle.WaitOne();
        if (processResult.Error != null)
        {
            throw processResult.Error;
        }

        return processResult.Result!;
    }

    private static decimal[] ProcessInParts(decimal[] data, int partCount)
    {
        var result = new decimal[data.Length];
        int baseCount = data.Length / partCount;
        int remainder = data.Length % partCount;
        var errors = new Exception?[partCount];
        using var completed = new CountdownEvent(partCount);

        int start = 0;
        for (int i = 0; i < partCount; i++)
        {
            int from = start;
            int length = baseCount + (i < remainder ? 1 : 0);
            int to = from + length;
            int index = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Fill(data, result, from, to);
                }
                catch (Exception exception)
                {
                    errors[index] = exception;
                }
                finally
                {
                    completed.Signal();
                }
            });
            start = to;
        }

        completed.Wait();

        var errorList = new List<Exception>();
        for (int i = 0; i < errors.Length; i++)
        {
            if (errors[i] != null)
            {
                errorList.Add(errors[i]!);
            }
        }

        if (errorList.Count > 0)
        {
            throw new AggregateException(errorList);
        }

        return result;
    }

    private static void Fill(decimal[] data, decimal[] result, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            result[i] = Compute(data[i]);
        }
    }

    private static decimal Compute(decimal value)
    {
        double number = (double)value;
        return (decimal)(Math.Sqrt(number) * Math.Log10(number + 1));
    }

    private sealed class ProcessAsyncResult : IAsyncResult
    {
        private readonly ManualResetEvent _done = new ManualResetEvent(false);

        public ProcessAsyncResult(object? state)
        {
            AsyncState = state;
        }

        public object? AsyncState { get; }

        public WaitHandle AsyncWaitHandle => _done;

        public bool CompletedSynchronously => false;

        public bool IsCompleted { get; private set; }

        public decimal[]? Result { get; private set; }

        public Exception? Error { get; private set; }

        public void Complete(decimal[]? result, Exception? error)
        {
            Result = result;
            Error = error;
            IsCompleted = true;
            _done.Set();
        }
    }
}
