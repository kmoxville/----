using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ConcurrentCollections;

public class TaskItem
{
    public Guid Id { get; set; }

    public string Description { get; set; } = "";

    public Action? Action { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class TaskQueueManager
{
    private Thread[] _workers = Array.Empty<Thread>();
    private int _processed;

    public TaskQueueManager(int boundedCapacity)
    {
        TaskQueue = new BlockingCollection<TaskItem>(boundedCapacity);
    }

    public BlockingCollection<TaskItem> TaskQueue { get; }

    public int ProcessedCount => _processed;

    public void AddTask(string description, Action taskAction)
    {
        var item = new TaskItem
        {
            Id = Guid.NewGuid(),
            Description = description,
            Action = taskAction,
            CreatedAt = DateTime.Now
        };
        TaskQueue.Add(item);
    }

    public void ProcessTasks(int workerCount)
    {
        _workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = new Thread(Consume);
            _workers[i].IsBackground = true;
            _workers[i].Start();
        }
    }

    public void CompleteAdding()
    {
        TaskQueue.CompleteAdding();
        for (int i = 0; i < _workers.Length; i++)
        {
            _workers[i].Join();
        }
    }

    public int GetPendingTaskCount()
    {
        return TaskQueue.Count;
    }

    private void Consume()
    {
        foreach (TaskItem item in TaskQueue.GetConsumingEnumerable())
        {
            item.Action?.Invoke();
            Interlocked.Increment(ref _processed);
        }
    }
}
