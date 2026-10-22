using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ConcurrentCollections;

public class Book
{
    public string Title { get; set; } = "";

    public string Author { get; set; } = "";
}

public class ConcurrentLibraryCatalog
{
    public ConcurrentDictionary<string, Book> Books { get; } = new ConcurrentDictionary<string, Book>();

    public bool AddBook(string title, string author)
    {
        var book = new Book { Title = title, Author = author };
        if (Books.TryAdd(title, book))
        {
            return true;
        }

        Books.AddOrUpdate(title, book, (_, _) => book);
        return false;
    }

    public bool RemoveBook(string title)
    {
        return Books.TryRemove(title, out _);
    }

    public bool UpdateBook(string title, string newTitle, string newAuthor)
    {
        var updated = new Book { Title = newTitle, Author = newAuthor };
        if (title == newTitle)
        {
            if (!Books.TryGetValue(title, out Book? current))
            {
                return false;
            }

            if (Books.TryUpdate(title, updated, current))
            {
                return true;
            }

            Books.AddOrUpdate(title, updated, (_, _) => updated);
            return true;
        }

        if (!Books.TryRemove(title, out _))
        {
            return false;
        }

        return Books.TryAdd(newTitle, updated);
    }

    public List<Book> SearchBooks(string keyword)
    {
        var found = new List<Book>();
        foreach (Book book in Books.Values)
        {
            if (book.Title.Contains(keyword) || book.Author.Contains(keyword))
            {
                found.Add(book);
            }
        }

        return found;
    }

    public List<Book> GetAllBooks()
    {
        return new List<Book>(Books.Values);
    }

    public int GetBookCount()
    {
        return Books.Count;
    }

    public void ClearCatalog()
    {
        Books.Clear();
    }

    public bool TryGetBook(string title, out Book? book)
    {
        return Books.TryGetValue(title, out book);
    }
}
