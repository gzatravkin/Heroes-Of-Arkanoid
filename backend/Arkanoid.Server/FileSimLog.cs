using System.Text.Json;
using Arkanoid.Core.Sim;
namespace Arkanoid.Server;

/// <summary>Writes one JSONL line per record to logs/&lt;runId&gt;.jsonl. Thread-safe (sim + receive loops share it).</summary>
public sealed class FileSimLog : ISimLog, IDisposable
{
    private readonly StreamWriter _w;
    private readonly object _gate = new();
    public bool Verbose { get; }

    public FileSimLog(string filePath, bool verbose)
    {
        Verbose = verbose;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _w = new StreamWriter(filePath, append: false) { AutoFlush = true };
    }

    public void Log(long tick, string cat, string msg, string data = "")
    {
        var line = JsonSerializer.Serialize(new {
            ts = DateTime.UtcNow.ToString("HH:mm:ss.fff"), t = tick, cat, msg, data
        });
        lock (_gate) _w.WriteLine(line);
    }

    public void Note(string cat, string msg, string data = "") => Log(-1, cat, msg, data);
    public void Dispose() { lock (_gate) _w.Dispose(); }

    /// <summary>Deterministic log dir = the server project's /logs, independent of CWD.</summary>
    public static string DirFor() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs"));
}
