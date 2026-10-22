using System.Threading;

namespace AtomicOperations;

public class LockFreeStack<T>
{
    private sealed class Node
    {
        public Node(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public Node? Next { get; set; }
    }

    private Node? _head;

    public void Push(T item)
    {
        var node = new Node(item);
        while (true)
        {
            Node? head = _head;
            node.Next = head;
            Node? previous = Interlocked.CompareExchange(ref _head, node, head);
            if (previous == head)
            {
                return;
            }
        }
    }

    public bool TryPop(out T item)
    {
        while (true)
        {
            Node? head = _head;
            if (head == null)
            {
                Node? current = Interlocked.CompareExchange(ref _head, null, null);
                if (current == null)
                {
                    item = default!;
                    return false;
                }

                continue;
            }

            Node? next = head.Next;
            Node? previous = Interlocked.CompareExchange(ref _head, next, head);
            if (previous == head)
            {
                item = head.Value;
                return true;
            }
        }
    }

    public bool TryPeek(out T item)
    {
        Node? head = Interlocked.CompareExchange(ref _head, null, null);
        if (head == null)
        {
            item = default!;
            return false;
        }

        item = head.Value;
        return true;
    }

    public bool IsEmpty()
    {
        Node? head = Interlocked.CompareExchange(ref _head, null, null);
        return head == null;
    }

    public void Clear()
    {
        while (true)
        {
            Node? head = _head;
            Node? previous = Interlocked.CompareExchange(ref _head, null, head);
            if (previous == head)
            {
                return;
            }
        }
    }
}
