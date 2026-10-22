using System.Threading;

namespace AtomicOperations;

public class AtomicCounter
{
    private long _value;

    public AtomicCounter(long initialValue)
    {
        _value = initialValue;
    }

    public long Value => Interlocked.Read(ref _value);

    public long Increment()
    {
        return Interlocked.Increment(ref _value);
    }

    public long Decrement()
    {
        return Interlocked.Decrement(ref _value);
    }

    public long Add(long value)
    {
        return Interlocked.Add(ref _value, value);
    }

    public long Exchange(long newValue)
    {
        return Interlocked.Exchange(ref _value, newValue);
    }

    public long CompareExchange(long newValue, long comparand)
    {
        return Interlocked.CompareExchange(ref _value, newValue, comparand);
    }

    public void Reset()
    {
        long previous = Interlocked.Exchange(ref _value, 0);
        _ = previous;
    }

    public long GetAndIncrement()
    {
        return Interlocked.Increment(ref _value) - 1;
    }

    public long GetAndDecrement()
    {
        return Interlocked.Decrement(ref _value) + 1;
    }
}
