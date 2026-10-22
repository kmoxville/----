using System;
using System.Collections.Generic;
using System.Globalization;

namespace MonitorLock;

public static class Program
{
    public static void Main()
    {
        var benchmark = new SynchronizationBenchmark();
        var results = benchmark.CompareAllApproaches();

        long noSyncMs = results.NoSync.Time;
        long lockMs = results.Lock.Time;
        long monitorMs = results.Monitor.Time;
        double lockOverhead = Overhead(noSyncMs, lockMs);
        double monitorOverhead = Overhead(noSyncMs, monitorMs);
        string speedup = Speedup(lockMs, monitorMs);

        Console.WriteLine("=== Результаты тестирования синхронизации ===");
        Console.WriteLine("Количество транзакций: 1000");
        Console.WriteLine("Без синхронизации:");
        Console.WriteLine($"  Время: {noSyncMs} мс");
        Console.WriteLine($"  Итоговый баланс: {results.NoSync.Balance}");
        Console.WriteLine($"  Корректность: {(results.NoSync.Correct ? "Да" : "Нет")}");
        Console.WriteLine($"  Гонки данных: {(results.NoSync.Correct ? "Нет" : "Да")}");
        Console.WriteLine();
        Console.WriteLine("С использованием lock:");
        Console.WriteLine($"  Время: {lockMs} мс");
        Console.WriteLine($"  Итоговый баланс: {results.Lock.Balance}");
        Console.WriteLine($"  Корректность: {(results.Lock.Correct ? "Да" : "Нет")}");
        Console.WriteLine($"  Накладные расходы: {Format(lockOverhead)}%");
        Console.WriteLine();
        Console.WriteLine("С использованием Monitor:");
        Console.WriteLine($"  Время: {monitorMs} мс");
        Console.WriteLine($"  Итоговый баланс: {results.Monitor.Balance}");
        Console.WriteLine($"  Корректность: {(results.Monitor.Correct ? "Да" : "Нет")}");
        Console.WriteLine($"  Накладные расходы: {Format(monitorOverhead)}%");
        Console.WriteLine();
        Console.WriteLine("Сравнение производительности:");
        Console.WriteLine($"  Ускорение lock vs Monitor: {speedup}x");
        Console.WriteLine($"  Накладные расходы lock: {Format(lockOverhead)}%");
        Console.WriteLine($"  Накладные расходы Monitor: {Format(monitorOverhead)}%");
        Console.WriteLine();

        var few = new SynchronizationBenchmark().BenchmarkWithLock(new BankAccount(0), CreateTransactions(100));
        var many = new SynchronizationBenchmark().BenchmarkWithLock(new BankAccount(0), CreateTransactions(1000));
        Console.WriteLine($"100 потоков: {few.Time} мс, корректность: {(few.Correct ? "Да" : "Нет")}");
        Console.WriteLine($"1000 потоков: {many.Time} мс, корректность: {(many.Correct ? "Да" : "Нет")}");
        Console.WriteLine();

        var accounts = new List<BankAccount>();
        for (int i = 0; i < 10; i++)
        {
            var account = new BankAccount(i);
            account.Balance = 1000m;
            accounts.Add(account);
        }

        decimal before = Total(accounts);
        new TransactionProcessor().ProcessConcurrentTransfers(accounts, 1000);
        decimal after = Total(accounts);
        Console.WriteLine($"Сумма 10 счетов до переводов: {before}");
        Console.WriteLine($"Сумма 10 счетов после 1000 переводов: {after}");
        Console.WriteLine($"Сумма сохранилась: {(before == after ? "Да" : "Нет")}");
    }

    public static List<decimal> CreateTransactions(int count)
    {
        var random = new Random(42);
        var transactions = new List<decimal>(count);
        int deposits = count / 2;
        for (int i = 0; i < count; i++)
        {
            decimal amount = (decimal)(10 + random.NextDouble() * 990);
            transactions.Add(i < deposits ? amount : -amount);
        }

        return transactions;
    }

    private static decimal Total(List<BankAccount> accounts)
    {
        decimal sum = 0;
        for (int i = 0; i < accounts.Count; i++)
        {
            sum += accounts[i].Balance;
        }

        return sum;
    }

    private static double Overhead(long baseMs, long syncMs)
    {
        if (baseMs == 0)
        {
            return 0;
        }

        return (syncMs - baseMs) * 100.0 / baseMs;
    }

    private static string Speedup(long lockMs, long monitorMs)
    {
        double speedup = monitorMs == 0 ? lockMs : (double)lockMs / monitorMs;
        return speedup.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Format(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
