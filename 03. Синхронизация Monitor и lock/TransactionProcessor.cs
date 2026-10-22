using System;
using System.Collections.Generic;
using System.Threading;

namespace MonitorLock;

public class TransactionProcessor
{
    public decimal ProcessTransactionsConcurrently(BankAccount account, List<decimal> transactions)
    {
        Run(transactions, amount =>
        {
            if (amount > 0)
            {
                account.Deposit(amount);
            }
            else
            {
                account.Withdraw(-amount);
            }
        });
        return account.Balance;
    }

    public decimal ProcessTransactionsWithLock(BankAccount account, List<decimal> transactions)
    {
        Run(transactions, amount =>
        {
            if (amount > 0)
            {
                account.DepositWithLock(amount);
            }
            else
            {
                account.WithdrawWithLock(-amount);
            }
        });
        return account.Balance;
    }

    public decimal ProcessTransactionsWithMonitor(BankAccount account, List<decimal> transactions)
    {
        Run(transactions, amount =>
        {
            if (amount > 0)
            {
                account.DepositWithMonitor(amount);
            }
            else
            {
                account.WithdrawWithMonitor(-amount);
            }
        });
        return account.Balance;
    }

    public void ProcessConcurrentTransfers(List<BankAccount> accounts, int transferCount)
    {
        bool deadlock = BankAccount.WouldDeadlock(accounts[0], accounts[1]);
        Console.WriteLine(deadlock
            ? "Разный порядок захвата: взаимная блокировка, второй поток не дождался блокировки."
            : "Разный порядок захвата: взаимная блокировка не проявилась.");

        var random = new Random(42);
        var threads = new Thread[transferCount];
        for (int i = 0; i < transferCount; i++)
        {
            int from = random.Next(accounts.Count);
            int to = random.Next(accounts.Count - 1);
            if (to >= from)
            {
                to++;
            }

            decimal amount = (decimal)(10 + random.NextDouble() * 990);
            BankAccount source = accounts[from];
            BankAccount target = accounts[to];
            threads[i] = new Thread(() => source.TransferWithLock(target, amount));
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }
    }

    private static void Run(List<decimal> transactions, Action<decimal> action)
    {
        var threads = new Thread[transactions.Count];
        for (int i = 0; i < transactions.Count; i++)
        {
            decimal amount = transactions[i];
            threads[i] = new Thread(() => action(amount));
            threads[i].Start();
        }

        for (int i = 0; i < threads.Length; i++)
        {
            threads[i].Join();
        }
    }
}
