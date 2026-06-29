// Loads the dotnet WASM module once and exposes GameBridge/MetaBridge exports.
// Called from main.ts before any scene mounts.
//
// Initialization pattern mirrors backend/Arkanoid.Wasm/main.mjs:
//   dotnet.create() → { getAssemblyExports, getConfig } → getAssemblyExports(mainAssemblyName)
//   exports keyed as 'ArkanoidWasm.GameBridge' / 'ArkanoidWasm.MetaBridge'
//   dotnet.run() called after exports are fetched.

let _game: Record<string, any> | null = null;
let _meta: Record<string, any> | null = null;

export async function initWasm(): Promise<void> {
  if (_game) return; // already initialized

  // Provide localStorage-backed storage for the WASM C# interop layer.
  // The C# StorageInterop calls globalThis.arkStorage.get/set/remove.
  if (!(globalThis as any).arkStorage) {
    (globalThis as any).arkStorage = {
      get:    (key: string)               => localStorage.getItem(key),
      set:    (key: string, value: string) => localStorage.setItem(key, value),
      remove: (key: string)               => localStorage.removeItem(key),
    };
  }

  // The dotnet.js module is served from /_framework/ (placed there by build-wasm.ps1).
  // It must be loaded as a module to get the dotnet runtime API.
  // @vite-ignore suppresses Vite's unresolved-import warning for this runtime URL.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { dotnet } = await (import(/* @vite-ignore */ "/_framework/dotnet.js") as any);

  // create() returns { setModuleImports, getAssemblyExports, getConfig }.
  const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);

  _game = exports["ArkanoidWasm.GameBridge"] as Record<string, any>;
  _meta = exports["ArkanoidWasm.MetaBridge"] as Record<string, any>;

  // Initialize catalogs (loads embedded JSON resources; caches in static fields).
  _game!.InitCatalogs();

  // Start the .NET runtime event loop.
  await dotnet.run();
}

export function gameBridge(): Record<string, any> {
  if (!_game) throw new Error("WASM not initialized — call initWasm() first");
  return _game;
}

export function metaBridge(): Record<string, any> {
  if (!_meta) throw new Error("WASM not initialized — call initWasm() first");
  return _meta;
}
