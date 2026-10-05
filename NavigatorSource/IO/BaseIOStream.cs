

using System;
using System.IO;

namespace XQuinn.IO
{
    
public abstract class IOStream : IDisposable
{
    public virtual void Dispose()
    {
        Writer.Close();
        GC.SuppressFinalize(this);
    }

    protected void Flush() => Writer.Flush();

    protected virtual StreamWriter Writer => _writer;

    StreamWriter _writer = null!;

    internal IOStream()
    {

    }
    public static void SafetyCheck(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Pah cannot be null or whitespace.");
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("Unable to get directory name.");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            using FileStream fs = File.Create(path);
        }

    }

    protected static T New<T>(T obj, string path, bool makeFileIfNotFound) where T : IOStream
    {
        if (makeFileIfNotFound)
            SafetyCheck(path);
        obj._writer = new(path);
        return obj;
    }

}
}