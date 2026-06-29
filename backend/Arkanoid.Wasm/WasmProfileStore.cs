using System.Text.Json;
using Arkanoid.Core.Meta;

namespace ArkanoidWasm;

internal sealed class WasmProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = false };
    private const string KeyPrefix = "ark_profile_";

    public Profile Load(string pid = "default")
    {
        var key = KeyPrefix + Sanitize(pid);
        var json = StorageInterop.Get(key);
        if (string.IsNullOrEmpty(json)) return Profile.NewDefault();
        var p = JsonSerializer.Deserialize<Profile>(json) ?? Profile.NewDefault();
        if (!p.CurrencyMigrated)
        {
            p.MigrateCurrencies();
            Save(p, pid);
        }
        return p;
    }

    public void Save(Profile profile, string pid = "default")
    {
        var key = KeyPrefix + Sanitize(pid);
        StorageInterop.Set(key, JsonSerializer.Serialize(profile, _opts));
    }

    private static string Sanitize(string s) =>
        new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()) is { Length: > 0 } v ? v : "default";
}
