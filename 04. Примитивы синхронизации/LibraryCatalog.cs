using System;
using System.Collections.Generic;
using System.Threading;

namespace SyncPrimitives;

public class Book
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
}

public class LibraryCatalog : IDisposable
{
    private readonly List<Book> _books = new List<Book>();
    private readonly ReaderWriterLockSlim _rw = new ReaderWriterLockSlim();

    public void AddBook(string title, string author)
    {
        _rw.EnterWriteLock();
        try
        {
            _books.Add(new Book { Title = title, Author = author });
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    public void RemoveBook(string title)
    {
        _rw.EnterWriteLock();
        try
        {
            for (int i = 0; i < _books.Count; i++)
            {
                if (_books[i].Title == title)
                {
                    _books.RemoveAt(i);
                    return;
                }
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    public void UpdateBook(string title, string newTitle, string newAuthor)
    {
        _rw.EnterWriteLock();
        try
        {
            for (int i = 0; i < _books.Count; i++)
            {
                if (_books[i].Title == title)
                {
                    _books[i].Title = newTitle;
                    _books[i].Author = newAuthor;
                    return;
                }
            }
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    public List<Book> SearchBooks(string keyword)
    {
        _rw.EnterReadLock();
        try
        {
            return Find(keyword);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public List<Book> GetAllBooks()
    {
        _rw.EnterReadLock();
        try
        {
            return new List<Book>(_books);
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public bool TryAddBook(string title, string author, int timeoutMs)
    {
        if (!_rw.TryEnterWriteLock(timeoutMs))
        {
            return false;
        }

        try
        {
            _books.Add(new Book { Title = title, Author = author });
            return true;
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    public bool TrySearchBooks(string keyword, int timeoutMs)
    {
        if (!_rw.TryEnterReadLock(timeoutMs))
        {
            return false;
        }

        try
        {
            Find(keyword);
            return true;
        }
        finally
        {
            _rw.ExitReadLock();
        }
    }

    public void HoldWriteLock(ManualResetEvent started, ManualResetEvent finish)
    {
        _rw.EnterWriteLock();
        try
        {
            started.Set();
            finish.WaitOne();
        }
        finally
        {
            _rw.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _rw.Dispose();
    }

    private List<Book> Find(string keyword)
    {
        var found = new List<Book>();
        for (int i = 0; i < _books.Count; i++)
        {
            if (_books[i].Title.Contains(keyword) || _books[i].Author.Contains(keyword))
            {
                found.Add(_books[i]);
            }
        }

        return found;
    }
}
