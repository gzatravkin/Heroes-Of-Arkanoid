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

    private readonly string _dir;
    private readonly object _gate = new();

    public ProfileStore()
    {
        // Mirror FileSimLog.DirFor() pattern: server-project/saves, independent of CWD.
        _dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "saves"));
        Directory.CreateDirectory(_dir);
    }

    // Per-namespace file so parallel test workers (or future multi-profile) never
    // clobber each other. "default" keeps the original profile.json path.
    private string PathFor(string pid) =>
        Path.Combine(_dir, pid == "default" ? "profile.json" : $"profile-{Sanitize(pid)}.json");

    internal static string Sanitize(string s) =>
        new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()) is { Length: > 0 } v ? v : "default";

    /// <summary>Returns the saved profile for the namespace, or a fresh default if none exists.</summary>
    public Profile Load(string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            if (!File.Exists(path))
                return Profile.NewDefault();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Profile>(json) ?? Profile.NewDefault();
        }
    }

    /// <summary>Persists the profile atomically (write-to-temp then rename).</summary>
    public void Save(Profile profile, string pid = "default")
    {
        lock (_gate)
        {
            var path = PathFor(pid);
            var json = JsonSerializer.Serialize(profile, JsonOpts);
            var tmp  = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
    }
}
