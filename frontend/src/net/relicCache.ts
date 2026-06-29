import { wasmApi as metaApi } from "./WasmApi";

const _names = new Map<string, string>();
const _icons = new Map<string, string>();
const _descs = new Map<string, string>();
let _loaded = false;

export function getRelicName(id: string): string {
  return _names.get(id) ?? id;
}

export function getRelicIcon(id: string): string | undefined {
  return _icons.get(id);
}

/** The catalog description for a relic, or undefined for non-relic ids (ball cores etc.). */
export function getRelicDesc(id: string): string | undefined {
  return _descs.get(id);
}

export async function preloadRelics(): Promise<void> {
  if (_loaded) return;
  try {
    const data = await metaApi.getRelics();
    for (const r of data.relics) {
      _names.set(r.id, r.name);
      _icons.set(r.id, r.icon);
      if (r.description) _descs.set(r.id, r.description);
    }
    _loaded = true;
  } catch {
    // silently fall back to id as name
  }
}
