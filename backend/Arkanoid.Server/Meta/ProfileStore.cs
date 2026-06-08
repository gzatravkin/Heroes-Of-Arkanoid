using System.Text.Json;
using Arkanoid.Core.Meta;
namespace Arkanoid.Server.Meta;

/// <summary>
/// Loads and saves the single player Profile as JSON at &lt;server-project&gt;/saves/profile.json.
/// Thread-safe via a simple lock.
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _gate = new();

    public ProfileStore()
    {
        // Mirror FileSimLog.DirFor() pattern: server-project/saves, independent of CWD.
        var savesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "saves"));
        Directory.CreateDirectory(savesDir);
        _filePath = Path.Combine(savesDir, "profile.json");
    }

    /// <summary>Returns the saved profile, or a fresh default if no file exists yet.</summary>
    public Profile Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
                return Profile.NewDefault();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Profile>(json) ?? Profile.NewDefault();
        }
    }

    /// <summary>Persists the profile atomically (write-to-temp then rename).</summary>
    public void Save(Profile profile)
    {
        lock (_gate)
        {
            var json = JsonSerializer.Serialize(profile, JsonOpts);
            var tmp  = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
    }
}
