using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Back-compat for <see cref="Profile.OwnedModules"/>: pre-rework saves stored modules as an ARRAY of
/// instances (<c>[{ "defId": "...", "level": N, ... }]</c>); the rework stores a dict (<c>{ "id": level }</c>).
/// This reads either shape into the dict (a duplicate instance keeps the higher level) and always WRITES the
/// new dict shape — so old saves load cleanly and convert on first save.
/// </summary>
public sealed class OwnedModulesConverter : JsonConverter<Dictionary<string, int>>
{
    public override Dictionary<string, int> Read(ref Utf8JsonReader reader, System.Type t, JsonSerializerOptions o)
    {
        var dict = new Dictionary<string, int>();
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("defId", out var d)) continue;
                var id = d.GetString();
                if (string.IsNullOrEmpty(id)) continue;
                int lvl = el.TryGetProperty("level", out var l) && l.TryGetInt32(out var lv) ? lv : 1;
                dict[id] = System.Math.Max(dict.TryGetValue(id, out var cur) ? cur : 0, lvl);
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                if (prop.Value.TryGetInt32(out var lv)) dict[prop.Name] = lv;
        }
        return dict;
    }

    public override void Write(Utf8JsonWriter w, Dictionary<string, int> v, JsonSerializerOptions o)
    {
        w.WriteStartObject();
        foreach (var kv in v) w.WriteNumber(kv.Key, kv.Value);
        w.WriteEndObject();
    }
}
