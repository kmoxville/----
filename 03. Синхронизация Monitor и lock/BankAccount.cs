using System;
using System.Threading;

namespace MonitorLock;

public class BankAccount
{
    private readonly object _lock = new object();

    public BankAccount(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public decimal Balance { get; set; }

    public void Deposit(decimal amount)
    {
        decimal balance = Balance;
        Balance = balance + amount;
    }

    public void Withdraw(decimal amount)
    {
        decimal balance = Balance;
        Balance = balance - amount;
    }

    public void Transfer(BankAccount target, decimal amount)
    {
        decimal sourceBalance = Balance;
        decimal targetBalance = target.Balance;
        Balance = sourceBalance - amount;
        target.Balance = targetBalance + amount;
    }

    public void DepositWithLock(decimal amount)
    {
        lock (_lock)
        {
            Balance += amount;
        }
    }

    public void WithdrawWithLock(decimal amount)
    {
        lock (_lock)
        {
            Balance -= amount;
        }
    }

    public void TransferWithLock(BankAccount target, decimal amount)
    {
        BankAccount first = Id < target.Id ? this : target;
        BankAccount second = Id < target.Id ? target : this;
        lock (first._lock)
        {
            lock (second._lock)
            {
                Balance -= amount;
                target.Balance += amount;
            }
        }
    }

    public void DepositWithMonitor(decimal amount)
    {
        bool taken = false;
        try
        {
            Monitor.Enter(_lock, ref taken);
            Balance += amount;
        }
        finally
        {
            if (taken)
            {
                Monitor.Exit(_lock);
            }
        }
    }

    public void WithdrawWithMonitor(decimal amount)
    {
        bool taken = false;
        try
        {
            Monitor.Enter(_lock, ref taken);
            Balance -= amount;
        }
        finally
        {
            if (taken)
            {
                Monitor.Exit(_lock);
            }
        }
    }

    public void TransferWithMonitor(BankAccount target, decimal amount)
    {
        BankAccount first = Id < target.Id ? this : target;
        BankAccount second = Id < target.Id ? target : this;
        bool firstTaken = false;
        bool secondTaken = false;
        try
        {
            Monitor.Enter(first._lock, ref firstTaken);
            Monitor.Enter(second._lock, ref secondTaken);
            Balance -= amount;
            target.Balance += amount;
        }
        finally
        {
            if (secondTaken)
            {
                Monitor.Exit(second._lock);
            }

            if (firstTaken)
            {
                Monitor.Exit(first._lock);
            }
        }
    }

    public bool DepositWithTimeout(decimal amount, int timeoutMs)
    {
        if (!Monitor.TryEnter(_lock, timeoutMs))
        {
            return false;
        }

        try
        {
            Balance += amount;
            return true;
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public bool WithdrawWithTimeout(decimal amount, int timeoutMs)
    {
        if (!Monitor.TryEnter(_lock, timeoutMs))
        {
            return false;
        }

        try
        {
            Balance -= amount;
            return true;
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public static bool WouldDeadlock(BankAccount left, BankAccount right)
    {
        bool leftTookBoth = false;
        bool rightTookBoth = false;

        var first = new Thread(() =>
        {
            Monitor.Enter(left._lock);
            try
            {
                Thread.Sleep(50);
                if (Monitor.TryEnter(right._lock, 200))
                {
                    leftTookBoth = true;
                    Monitor.Exit(right._lock);
                }
            }
            finally
            {
                Monitor.Exit(left._lock);
            }
        });

        var second = new Thread(() =>
        {
            Monitor.Enter(right._lock);
            try
            {
                Thread.Sleep(50);
                if (Monitor.TryEnter(left._lock, 200))
                {
                    rightTookBoth = true;
                    Monitor.Exit(left._lock);
                }
            }
            finally
            {
                Monitor.Exit(right._lock);
            }
        });

        first.Start();
        second.Start();
        first.Join();
        second.Join();
        return !(leftTookBoth && rightTookBoth);
    }
}
