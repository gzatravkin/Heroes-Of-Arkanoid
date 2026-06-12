import { metaApi } from "./metaApi";

const _names = new Map<string, string>();
let _loaded = false;

export function getRelicName(id: string): string {
  return _names.get(id) ?? id;
}

export async function preloadRelics(): Promise<void> {
  if (_loaded) return;
  try {
    const data = await metaApi.getRelics();
    for (const r of data.relics) _names.set(r.id, r.name);
    _loaded = true;
  } catch {
    // silently fall back to id as name
  }
}
