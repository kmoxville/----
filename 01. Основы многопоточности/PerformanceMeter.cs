using System;
using System.Diagnostics;

namespace MultithreadingBasics;

public static class PerformanceMeter
{
    public static (long ElapsedMilliseconds, decimal[] Result) MeasureExecutionTime(
        Func<decimal[]> action,
        string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        decimal[] result = action();
        stopwatch.Stop();

        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"{operationName}: {elapsedMilliseconds} мс");
        return (elapsedMilliseconds, result);
    }

    public static bool CompareResults(decimal[] result1, decimal[] result2, decimal tolerance = 0.0001m)
    {
        if (result1.Length != result2.Length)
        {
            return false;
        }

        for (int i = 0; i < result1.Length; i++)
        {
            decimal difference = Math.Abs(result1[i] - result2[i]);
            if (difference > tolerance)
            {
                return false;
            }
        }

        return true;
    }
}
