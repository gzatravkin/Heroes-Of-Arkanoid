// Loads the dotnet WASM module once and exposes GameBridge/MetaBridge exports.
// Called from main.ts before any scene mounts.
//
// OutputType=Library initialization order:
//   1. dotnet.create()  — loads native .wasm + runtime, loads all assemblies,
//                         runs module initializers which register [JSExport] bindings
//   2. getAssemblyExports() — bindings are already registered; returns the filled export object
//   3. _game.InitCatalogs() — C# code is live, safe to call
//
// (No dotnet.run() needed — that is only for OutputType=Exe to execute Program.cs Main().)

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
  // @vite-ignore suppresses Vite's unresolved-import warning for this runtime URL.
  // @ts-ignore — /_framework/dotnet.js exists at runtime, not resolvable at tsc
  const { dotnet } = await (import(/* @vite-ignore */ `${import.meta.env.BASE_URL}_framework/dotnet.js`) as any);

  // create() loads the WASM runtime + all assemblies + runs module initializers.
  // For OutputType=Library, [JSExport] bindings are registered by module initializers
  // during create(), so getAssemblyExports() works immediately after.
  const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

  const config = getConfig();

  // .NET 8 browser-wasm getAssemblyExports() groups exports by namespace, then by class:
  //   exports["ArkanoidWasm"]["GameBridge"].InitCatalogs()
  //   exports["ArkanoidWasm"]["MetaBridge"].GetProfile()
  const wasmExports = await getAssemblyExports(config.mainAssemblyName);
  const ns = (wasmExports["ArkanoidWasm"] ?? wasmExports) as Record<string, any>;
  _game = ns["GameBridge"] as Record<string, any>;
  _meta = ns["MetaBridge"] as Record<string, any>;

  if (!_game) {
    throw new Error(
      `[Arkanoid.Wasm] GameBridge not found. Top-level keys: ${Object.keys(wasmExports).join(", ")}`
    );
  }

  // Initialize catalogs (loads embedded JSON resources into static caches).
  _game.InitCatalogs();
}

export function gameBridge(): Record<string, any> {
  if (!_game) throw new Error("WASM not initialized — call initWasm() first");
  return _game;
}

export function metaBridge(): Record<string, any> {
  if (!_meta) throw new Error("WASM not initialized — call initWasm() first");
  return _meta;
}
