using System.Threading;

namespace AtomicOperations;

public class StatisticsTracker
{
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _totalProcessingTime;

    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    public long SuccessfulRequests => Interlocked.Read(ref _successfulRequests);

    public long FailedRequests => Interlocked.Read(ref _failedRequests);

    public long TotalProcessingTime => Interlocked.Read(ref _totalProcessingTime);

    public void RecordRequest(bool success, long processingTime)
    {
        Interlocked.Increment(ref _totalRequests);
        if (success)
        {
            Interlocked.Increment(ref _successfulRequests);
        }
        else
        {
            Interlocked.Increment(ref _failedRequests);
        }

        Interlocked.Add(ref _totalProcessingTime, processingTime);
    }

    public double GetSuccessRate()
    {
        long total = Interlocked.Read(ref _totalRequests);
        if (total == 0)
        {
            return 0;
        }

        return Interlocked.Read(ref _successfulRequests) * 100.0 / total;
    }

    public double GetAverageProcessingTime()
    {
        long total = Interlocked.Read(ref _totalRequests);
        if (total == 0)
        {
            return 0;
        }

        return (double)Interlocked.Read(ref _totalProcessingTime) / total;
    }

    public void Reset()
    {
        long total = Interlocked.Exchange(ref _totalRequests, 0);
        long success = Interlocked.Exchange(ref _successfulRequests, 0);
        long failed = Interlocked.Exchange(ref _failedRequests, 0);
        long time = Interlocked.Exchange(ref _totalProcessingTime, 0);
        _ = total + success + failed + time;
    }
}
