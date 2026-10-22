using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolAsync;

public static class AsyncLogger
{
    private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    private const string LogFile = "log.txt";

    public static async Task LogAsync(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message + Environment.NewLine);
        await Gate.WaitAsync();
        try
        {
            using var stream = new FileStream(LogFile, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await stream.WriteAsync(bytes);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static void LogWithCallback(string message, Action callback)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message + Environment.NewLine);
        Gate.Wait();
        FileStream stream;
        try
        {
            stream = new FileStream(LogFile, FileMode.Append, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        }
        catch
        {
            Gate.Release();
            throw;
        }

        stream.BeginWrite(bytes, 0, bytes.Length, ar =>
        {
            try
            {
                stream.EndWrite(ar);
                callback();
            }
            finally
            {
                stream.Dispose();
                Gate.Release();
            }
        }, null);
    }
}
