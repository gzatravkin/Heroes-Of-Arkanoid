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

    private readonly string _filePath;
    private readonly object _gate = new();

    public DungeonStore()
    {
        var savesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "saves"));
        Directory.CreateDirectory(savesDir);
        _filePath = Path.Combine(savesDir, "dungeon.json");
    }

    /// <summary>Returns the saved run, or null if no file exists or the file is empty.</summary>
    public DungeonRun? Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<DungeonRun>(json);
        }
    }

    /// <summary>Persists the run atomically (write-to-temp then rename).</summary>
    public void Save(DungeonRun run)
    {
        lock (_gate)
        {
            var json = JsonSerializer.Serialize(run, JsonOpts);
            var tmp  = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
    }

    /// <summary>Deletes the saved run file (call after a run ends if desired).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
    }
}
