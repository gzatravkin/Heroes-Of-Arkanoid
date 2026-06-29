import { wasmApi as metaApi } from "./WasmApi";

// Display cache for spells (name / icon / mana cost), flattened from /characters across all
// classes — the shared pool's display source. Mirrors relicCache; used by the floor-clear pick
// overlay to render drafted-spell choices (docs/04 §5).

const _names = new Map<string, string>();
const _icons = new Map<string, string>();
const _mana  = new Map<string, number>();
const _descs = new Map<string, string>();
let _loaded = false;

export function getSpellName(id: string): string {
  return _names.get(id) ?? id;
}

export function getSpellIcon(id: string): string | undefined {
  return _icons.get(id);
}

export function getSpellManaCost(id: string): number | undefined {
  return _mana.get(id);
}

/** The spell's one-line effect description, or undefined if unknown. */
export function getSpellDesc(id: string): string | undefined {
  return _descs.get(id);
}

/** One-line blurb for a spell pick: prefer the effect description, fall back to the mana cost. */
export function getSpellBlurb(id: string): string {
  const d = _descs.get(id);
  if (d) return d;
  const m = _mana.get(id);
  return m === undefined ? "Spell" : m > 0 ? `Spell · ${m} mana` : "Spell · Free";
}

export async function preloadSpells(): Promise<void> {
  if (_loaded) return;
  try {
    const data = await metaApi.getCharacters();
    const all = [...data.characters.flatMap((ch: any) => ch.spells ?? []), ...(data.neutralSpells ?? [])];
    for (const sp of all) {
      if (!_names.has(sp.id)) {
        _names.set(sp.id, sp.name);
        _icons.set(sp.id, sp.icon);
        _mana.set(sp.id, sp.manaCost);
        if (sp.desc) _descs.set(sp.id, sp.desc);
      }
    }
    _loaded = true;
  } catch {
    // fall back to id as name
  }
}
