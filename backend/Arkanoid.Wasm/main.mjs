// main.mjs — WASM bootstrap for Arkanoid.Wasm
//
// The Vite frontend (WasmConnection.ts, Task 9) overrides globalThis.arkStorage
// before calling dotnet.run(), so this default is only a safety net for direct
// HTML page loads (e.g. static hosting smoke-tests).
import { dotnet } from './_framework/dotnet.js';

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

// Default localStorage-backed storage (frontend overrides this before run).
if (!globalThis.arkStorage) {
    globalThis.arkStorage = {
        get:    (key)        => localStorage.getItem(key),
        set:    (key, value) => localStorage.setItem(key, value),
        remove: (key)        => localStorage.removeItem(key),
    };
}

const config  = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Expose bridge namespaces for WasmConnection.ts.
globalThis._ArkanoidWasm = {
    GameBridge: exports['ArkanoidWasm.GameBridge'],
    MetaBridge: exports['ArkanoidWasm.MetaBridge'],
};

await dotnet.run();
