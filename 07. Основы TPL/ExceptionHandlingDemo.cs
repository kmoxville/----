using System;
using System.Threading.Tasks;

namespace TplBasics;

public class ExceptionHandlingDemo
{
    public int HandleExceptionsWithWait(decimal[] data)
    {
        Task task = Task.Run(() =>
        {
            _ = data.Length;
            throw new InvalidOperationException("ошибка в задаче");
        });

        try
        {
            task.Wait();
        }
        catch (AggregateException)
        {
            return HandleAggregatedExceptions(task);
        }

        return 0;
    }

    public int HandleExceptionsWithWhenAll(decimal[] data)
    {
        var tasks = new Task[3];
        tasks[0] = Task.Run(() =>
        {
            _ = data.Length;
            throw new InvalidOperationException("первая");
        });
        tasks[1] = Task.Run(() => throw new DivideByZeroException());
        tasks[2] = Task.Run(() => _ = data.Length);

        try
        {
            Task.WhenAll(tasks).Wait();
        }
        catch (AggregateException)
        {
            int count = 0;
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].IsFaulted)
                {
                    count += HandleAggregatedExceptions(tasks[i]);
                }
            }

            return count;
        }

        return 0;
    }

    public int HandleExceptionsWithContinueWith(decimal[] data)
    {
        Task task = Task.Run(() =>
        {
            _ = data[0];
            throw new InvalidOperationException("продолжение");
        });

        Task<int> continuation = task.ContinueWith(
            finished => HandleAggregatedExceptions(finished),
            TaskContinuationOptions.OnlyOnFaulted);
        return continuation.Result;
    }

    public int HandleAggregatedExceptions(Task task)
    {
        if (task.Exception == null)
        {
            return 0;
        }

        int count = 0;
        task.Exception.Flatten().Handle(error =>
        {
            count++;
            return error is InvalidOperationException || error is DivideByZeroException;
        });
        return count;
    }
}
