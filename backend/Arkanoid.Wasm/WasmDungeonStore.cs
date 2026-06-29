using System.Text.Json;
using Arkanoid.Core.Meta;

namespace ArkanoidWasm;

internal sealed class WasmDungeonStore : IDungeonStore
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = false };
    private const string KeyPrefix = "ark_dungeon_";

    public DungeonRun? Load(string pid = "default")
    {
        var json = StorageInterop.Get(KeyPrefix + Sanitize(pid));
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<DungeonRun>(json);
    }

    public void Save(DungeonRun run, string pid = "default")
        => StorageInterop.Set(KeyPrefix + Sanitize(pid), JsonSerializer.Serialize(run, _opts));

    public void Clear(string pid = "default")
        => StorageInterop.Remove(KeyPrefix + Sanitize(pid));

    private static string Sanitize(string s) =>
        new string((s ?? "").Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()) is { Length: > 0 } v ? v : "default";
}
