<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { ModulesResponse, ModuleDef } from "../net/metaApi";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import BuyRandom from "../ui/BuyRandom.svelte";
  import ItemTile from "../ui/ItemTile.svelte";
  import ItemDetail, { type DetailEntry } from "../ui/ItemDetail.svelte";
  import { moduleIcon, rarityColor, sortEquippedFirst } from "../ui/collection";

  // Modules: collected via Buy Random (defId → level + banked copies); equip one per slot. Manual level-up.
  let data = $state<ModulesResponse | null>(null);
  let busy = $state(false);
  let selected = $state<string | null>(null);

  const SLOTS = ["core", "paddle", "ball", "field"];
  const SLOT_LABEL: Record<string, string> = { core: "Core", paddle: "Paddle", ball: "Ball", field: "Field" };

  async function load() { data = await metaApi.getModules(); }
  load();

  let defById = $derived.by(() => { const m: Record<string, ModuleDef> = {}; for (const d of data?.modules ?? []) m[d.id] = d; return m; });
  // Equipped modules float to the FRONT of the collection.
  let mods = $derived(sortEquippedFirst(data?.modules ?? [],
    (d) => (data?.equipped[d.slot] === d.id ? 0 : 1)));
  function lvl(id: string) { return data?.owned[id] ?? 0; }
  function copies(id: string) { return data?.copies?.[id] ?? 0; }
  function isEquipped(d: ModuleDef) { return data?.equipped[d.slot] === d.id; }

  let detail = $derived.by<DetailEntry | null>(() => {
    if (!selected || !data) return null;
    const d = defById[selected];
    if (!d) return null;
    const owned = !!data.owned[d.id];
    return {
      id: d.id, name: d.name, icon: moduleIcon(d.id), tone: rarityColor(d.rarity),
      meta: `${SLOT_LABEL[d.slot]} · ${d.rarity.toUpperCase()}`,
      description: d.description || d.effectValue || "Slot passive",
      owned, level: data.owned[d.id] ?? 0, copies: copies(d.id), maxLevel: data.maxLevel,
      equipped: isEquipped(d), equippable: true,
    };
  });

  async function act(fn: () => Promise<unknown>) { if (busy) return; busy = true; try { await fn(); await load(); } finally { busy = false; } }
  function toggleEquip(id: string) {
    const d = defById[id]; if (!d) return;
    act(() => isEquipped(d) ? metaApi.unequipModule(d.slot) : metaApi.equipModule(id));
  }
  function levelUp(id: string) { act(() => metaApi.moduleLevelUp(id)); }
</script>

<div class="mod-root">
  <div class="ui-topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">Modules</h1>
    <CurrencyBar />
  </div>

  <div class="buy-row"><div class="buy-wrap"><BuyRandom kind="module" noun="Module" onrolled={load} /></div></div>

  {#if data}
    <!-- Equipped slots (compact strip) -->
    <div class="slots">
      {#each SLOTS as slot}
        {@const eqId = data.equipped[slot]}
        {@const d = eqId ? defById[eqId] : null}
        <button class="slot" class:filled={!!d} style="--tone:{d ? rarityColor(d.rarity) : '#5a4a2a'}"
                onclick={() => { if (d) selected = d.id; }}>
          <div class="slot-label">{SLOT_LABEL[slot]}</div>
          {#if d}
            <img class="slot-ico" src={moduleIcon(d.id)} alt={d.name}
                 onerror={function (this: HTMLImageElement) { this.src = '/items/ItemOrb.png'; this.onerror = null; }} />
            <div class="slot-name">{d.name}</div>
          {:else}
            <div class="slot-empty">— empty —</div>
          {/if}
        </button>
      {/each}
    </div>

    <h3 class="inv-title">Collection</h3>
    <div class="grid">
      {#each mods as d (d.id)}
        <ItemTile icon={moduleIcon(d.id)} name={d.name}
                  level={lvl(d.id)} copies={copies(d.id)}
                  owned={lvl(d.id) > 0} equipped={isEquipped(d)} locked={lvl(d.id) === 0}
                  tone={rarityColor(d.rarity)} selected={selected === d.id}
                  onclick={() => selected = d.id} />
      {/each}
    </div>
  {:else}
    <div class="loading">Loading…</div>
  {/if}

  {#if detail}
    <ItemDetail entry={detail} {busy} onClose={() => selected = null} onLevelUp={levelUp} onEquip={toggleEquip} />
  {/if}
</div>

<style>
  .mod-root { min-height: 100cqh; display: flex; flex-direction: column;
    background: radial-gradient(ellipse at 50% 0%, rgba(120,80,20,.30), transparent 60%), linear-gradient(180deg,#140d06,#0a0a12 55%,#050406);
    color: #efe6d6; font-family: var(--font-body); padding-bottom: 28px; }
  .buy-row { display: flex; padding: 10px 14px 4px; }
  .buy-wrap { flex: 1; max-width: 340px; margin: 0 auto; }
  .slots { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; padding: 8px 14px 4px; }
  .slot { display: flex; flex-direction: column; align-items: center; gap: 2px; cursor: pointer;
    border: 1px solid color-mix(in srgb, var(--tone) 45%, rgba(120,90,40,0.4)); border-radius: 9px;
    background: rgba(28,22,14,.7); padding: 5px 3px; min-height: 74px; -webkit-tap-highlight-color: transparent; }
  .slot.filled { box-shadow: inset 0 0 10px color-mix(in srgb, var(--tone) 22%, transparent); }
  .slot:hover { filter: brightness(1.1); }
  .slot-label { font-size: 9px; color: var(--gold,#ffce5a); font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
  .slot-ico { width: 32px; height: 32px; object-fit: contain; filter: drop-shadow(0 1px 2px rgba(0,0,0,.6)); }
  .slot-name { font-size: 8.5px; line-height: 1.05; text-align: center; color: var(--text);
    display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
  .slot-empty { color: #8a7c5e; font-style: italic; font-size: 9px; padding-top: 10px; }
  .inv-title { margin: 8px 16px 2px; color: var(--gold,#ffce5a); font-size: 14px; }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(86px, 1fr)); gap: 10px; padding: 6px 14px 24px; }
  .loading { padding: 40px; text-align: center; color: #b8a98a; }
  @container (min-width: 520px) { .grid { grid-template-columns: repeat(6, 1fr); } }
</style>
