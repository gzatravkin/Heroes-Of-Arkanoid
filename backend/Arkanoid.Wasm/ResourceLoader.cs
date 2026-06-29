using System.IO;
using System.Reflection;
using System.Text;

namespace ArkanoidWasm;

internal static class ResourceLoader
{
    private static readonly Assembly _asm = typeof(ResourceLoader).Assembly;

    internal static string GetJson(string logicalName)
    {
        var stream = _asm.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {logicalName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    internal static bool TryGetJson(string logicalName, out string json)
    {
        var stream = _asm.GetManifestResourceStream(logicalName);
        if (stream == null) { json = ""; return false; }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        json = reader.ReadToEnd();
        return true;
    }

    internal static IEnumerable<string> ListLevelIds()
    {
        return _asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("config/levels/") && n.EndsWith(".json"))
            .Select(n => n["config/levels/".Length..].Replace(".json", ""));
    }
}
