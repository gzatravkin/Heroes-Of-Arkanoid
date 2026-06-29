using System.Runtime.InteropServices.JavaScript;

namespace ArkanoidWasm;

internal static partial class StorageInterop
{
    [JSImport("globalThis.arkStorage.get")]
    internal static partial string? Get(string key);

    [JSImport("globalThis.arkStorage.set")]
    internal static partial void Set(string key, string value);

    [JSImport("globalThis.arkStorage.remove")]
    internal static partial void Remove(string key);
}
