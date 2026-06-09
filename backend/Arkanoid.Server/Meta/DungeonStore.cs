using System.Text.Json;
using Arkanoid.Core.Meta;
namespace Arkanoid.Server.Meta;

/// <summary>
/// Loads and saves the single active <see cref="DungeonRun"/> as JSON at
/// &lt;server-project&gt;/saves/dungeon.json (mirrors <see cref="ProfileStore"/>).
/// Returns null when no run exists.
/// Thread-safe via a simple lock.
/// </summary>
public sealed class DungeonStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly object _gate = new();

    public DungeonStore()
    {
        _dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "saves"));
        Directory.CreateDirectory(_dir);
    }

    private string PathFor(string pid) =>
        Path.Combine(_dir, pid == "default" ? "dungeon.json" : $"dungeon-{ProfileStore.Sanitize(pid)}.json");

    /// <summary>Returns the saved run for the namespace, or null if none exists / empty.</summary>
    public DungeonRun? Load(string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<DungeonRun>(json);
        }
    }

    /// <summary>Persists the run atomically (write-to-temp then rename).</summary>
    public void Save(DungeonRun run, string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            var json = JsonSerializer.Serialize(run, JsonOpts);
            var tmp  = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
    }

    /// <summary>Deletes the saved run file (call after a run ends if desired).</summary>
    public void Clear(string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
