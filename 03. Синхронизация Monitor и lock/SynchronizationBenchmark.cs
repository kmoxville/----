using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonitorLock;

public class SynchronizationBenchmark
{
    private readonly TransactionProcessor _processor = new TransactionProcessor();

    public (long Time, decimal Balance, bool Correct) BenchmarkNoSync(BankAccount account, List<decimal> transactions)
    {
        return Measure(account, transactions, _processor.ProcessTransactionsConcurrently);
    }

    public (long Time, decimal Balance, bool Correct) BenchmarkWithLock(BankAccount account, List<decimal> transactions)
    {
        return Measure(account, transactions, _processor.ProcessTransactionsWithLock);
    }

    public (long Time, decimal Balance, bool Correct) BenchmarkWithMonitor(BankAccount account, List<decimal> transactions)
    {
        return Measure(account, transactions, _processor.ProcessTransactionsWithMonitor);
    }

    public (
        (long Time, decimal Balance, bool Correct) NoSync,
        (long Time, decimal Balance, bool Correct) Lock,
        (long Time, decimal Balance, bool Correct) Monitor)
        CompareAllApproaches()
    {
        List<decimal> transactions = Program.CreateTransactions(1000);
        var noSync = BenchmarkNoSync(new BankAccount(0), transactions);
        var withLock = BenchmarkWithLock(new BankAccount(0), transactions);
        var withMonitor = BenchmarkWithMonitor(new BankAccount(0), transactions);

        Console.WriteLine("=== Сравнение подходов к синхронизации ===");
        Console.WriteLine($"Без синхронизации: {noSync.Time} мс, результат: {Text(noSync.Correct)}");
        Console.WriteLine($"С использованием lock: {withLock.Time} мс, результат: {Text(withLock.Correct)}");
        Console.WriteLine($"С использованием Monitor: {withMonitor.Time} мс, результат: {Text(withMonitor.Correct)}");
        Console.WriteLine();
        return (noSync, withLock, withMonitor);
    }

    private static (long Time, decimal Balance, bool Correct) Measure(
        BankAccount account,
        List<decimal> transactions,
        Func<BankAccount, List<decimal>, decimal> process)
    {
        decimal expected = account.Balance;
        for (int i = 0; i < transactions.Count; i++)
        {
            expected += transactions[i];
        }

        var watch = Stopwatch.StartNew();
        decimal balance = process(account, transactions);
        watch.Stop();
        return (watch.ElapsedMilliseconds, balance, balance == expected);
    }

    private static string Text(bool correct)
    {
        return correct ? "корректный" : "некорректный";
    }
}
