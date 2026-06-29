using System.Text.Json;
namespace Arkanoid.Server.Meta;

/// <summary>
/// Thread-safe atomic JSON file store. Eliminates the identical read/write/lock
/// pattern that ProfileStore and DungeonStore used to duplicate.
/// </summary>
internal sealed class JsonStore<T> where T : class
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly string _defaultFile;
    private readonly string _prefix;
    private readonly object _gate = new();

    internal JsonStore(string dir, string defaultFile, string prefix)
    {
        _dir         = dir;
        _defaultFile = defaultFile;
        _prefix      = prefix;
        Directory.CreateDirectory(dir);
    }

    internal static string Sanitize(string s) =>
        new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()) is { Length: > 0 } v ? v : "default";

    private string PathFor(string pid) =>
        Path.Combine(_dir, pid == "default" ? _defaultFile : $"{_prefix}-{Sanitize(pid)}.json");

    internal T? Load(string pid = "default", Func<T>? createDefault = null)
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            if (!File.Exists(path)) return createDefault?.Invoke();
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return createDefault?.Invoke();
            return JsonSerializer.Deserialize<T>(json);
        }
    }

    internal void Save(T value, string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            var json = JsonSerializer.Serialize(value, JsonOpts);
            var tmp  = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
    }

    internal void Clear(string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
