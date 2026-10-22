using System;
using System.Globalization;

namespace MultithreadingBasics;

public static class Program
{
    public static void Main(string[] args)
    {
        int dataSize = args.Length > 0 ? int.Parse(args[0]) : 10_000_000;
        int threadCount = args.Length > 1 ? int.Parse(args[1]) : 4;

        var random = new Random(42);
        var data = new decimal[dataSize];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (decimal)(1.0 + random.NextDouble() * 999.0);
        }

        (long sequentialMs, decimal[] sequentialResult) = PerformanceMeter.MeasureExecutionTime(
            () => DataProcessor.ProcessDataSequential(data),
            "Последовательная обработка");

        (long parallelMs, decimal[] parallelResult) = PerformanceMeter.MeasureExecutionTime(
            () => DataProcessor.ProcessDataParallel(data, threadCount),
            "Параллельная обработка");

        bool match = PerformanceMeter.CompareResults(sequentialResult, parallelResult);
        Console.WriteLine(match ? "Результаты совпадают." : "Результаты не совпадают.");

        double speedup = parallelMs == 0 ? sequentialMs : (double)sequentialMs / parallelMs;
        string sizeText = dataSize.ToString("N0", CultureInfo.InvariantCulture);
        string speedupText = speedup.ToString("0.00", CultureInfo.InvariantCulture);

        Console.WriteLine();
        Console.WriteLine("=== Результаты обработки ===");
        Console.WriteLine($"Размер данных: {sizeText} элементов");
        Console.WriteLine($"Последовательная обработка: {sequentialMs} мс");
        Console.WriteLine($"Параллельная обработка ({threadCount} потока): {parallelMs} мс");
        Console.WriteLine($"Ускорение: {speedupText}x");
        Console.WriteLine($"Результаты совпадают: {(match ? "Да" : "Нет")}");
    }
}
