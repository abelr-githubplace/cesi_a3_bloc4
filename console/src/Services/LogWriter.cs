using System.IO;
using System.Text;

namespace EasySave.Console.Services;

public sealed class LogWriter
{
    private readonly string _dir;
    private readonly object _lock = new();

    public LogWriter(string dir = "logs")
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public void Write(string line)
    {
        var path = Path.Combine(_dir, $"{DateTime.Now:yyyy-MM-dd}.log");
        lock (_lock)
        {
            using var w = new StreamWriter(path, append: true, Encoding.UTF8);
            w.WriteLine(line);
        }
    }
}
