<script lang="ts">
  import type { SpellPoolEntry } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";
  import { buildSpellIcon } from "../ui/hud/spellIcon";
  import { sortEquippedFirst } from "../ui/collection";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import BuyRandom from "../ui/BuyRandom.svelte";
  import ItemTile from "../ui/ItemTile.svelte";
  import ItemDetail, { type DetailEntry } from "../ui/ItemDetail.svelte";

  const MAX_SPELL_LEVEL = 10; // mirrors SpellService.MaxSpellLevel

  let spells        = $state<SpellPoolEntry[]>([]);
  let loadout       = $state<string[]>([]);
  let signature     = $state("");
  let unlockedSlots = $state(3);
  let character     = $state("");
  let loading       = $state(true);
  let loadError     = $state(false);
  let busy          = $state(false);
  let selectedId    = $state<string | null>(null);

  const equippedCount = $derived(loadout.length);
  const charName = $derived(
    character ? character.split("_").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ") : ""
  );
  // Equipped (hotbar) spells float to the FRONT; signature is equipped so it leads.
  const sortedSpells = $derived(sortEquippedFirst(spells, (s) => (s.equipped ? 0 : 1)));
  const sel = $derived(selectedId ? spells.find(s => s.id === selectedId) ?? null : null);

  const detail = $derived.by<DetailEntry | null>(() => {
    if (!sel) return null;
    return {
      id: sel.id, name: sel.name, icon: "", tone: sel.signature ? "#6cc0ff" : "#d8a84e",
      meta: (sel.signature ? "SIGNATURE · " : "") + (sel.manaCost > 0 ? `${sel.manaCost} mana` : "Free"),
      description: sel.desc, owned: sel.owned, level: sel.level, copies: sel.copies, maxLevel: MAX_SPELL_LEVEL,
      equipped: sel.equipped, equippable: !sel.signature,
      equipLabel: sel.signature ? "Always equipped (slot 0)" : undefined,
      locked: !sel.owned && !sel.signature,
    };
  });

  async function load() {
    try {
      const data = await metaApi.getSpells();
      spells = data.spells; loadout = [...data.loadout];
      signature = data.signature; unlockedSlots = data.unlockedSlots; character = data.character;
      loading = false;
    } catch { loading = false; loadError = true; }
  }
  load();

  async function act(fn: () => Promise<unknown>) { if (busy) return; busy = true; try { await fn(); await load(); } finally { busy = false; } }
  function toggleEquip(id: string) {
    const e = spells.find(s => s.id === id);
    if (!e || e.signature) return;
    act(() => e.equipped ? metaApi.unequipSpell(id) : metaApi.equipSpell(id));
  }
  function levelUp(id: string) { act(() => metaApi.spellLevelUp(id)); }

  // Svelte action: render a spell's icon (atlas → legacy art → letter) into the node.
  function spellIcon(node: HTMLElement, entry: SpellPoolEntry) {
    buildSpellIcon(node, entry);
    return { update(e: SpellPoolEntry) { node.innerHTML = ""; buildSpellIcon(node, e); } };
  }
</script>

<div id="loadout-root" class="root">
  <div class="header">
    <button id="btn-loadout-back" class="ui-back" aria-label="Back to characters"
            onclick={() => navigateTo("/?scene=characters")}></button>
    <div class="title-wrap">
      <h1 class="title">Spell Loadout</h1>
      {#if charName}<div class="char-sub" id="loadout-char">{charName}</div>{/if}
    </div>
    <CurrencyBar />
  </div>

  <div class="buy-row">
    <span class="slots-count" id="loadout-slots">Hotbar {equippedCount}/{unlockedSlots}</span>
    <div class="buy-wrap"><BuyRandom kind="spell" noun="Spell" onrolled={load} /></div>
  </div>

  <div class="equipped-section">
    <div class="section-label">HOTBAR</div>
    <div id="loadout-equipped-row" class="equipped-row">
      {#each Array.from({ length: unlockedSlots }) as _, i}
        {@const spellId = loadout[i]}
        {@const def = spellId ? spells.find(s => s.id === spellId) : undefined}
        {#if def}
          <div class="equip-slot equip-slot-filled {def.signature ? 'equip-slot-sig' : ''}"
               data-slot={i} data-equipped={def.id}>
            <div class="slot-icon" use:spellIcon={def}></div>
            <div class="slot-label">{def.name}</div>
            {#if def.signature}<div class="sig-tag">SIG</div>{/if}
          </div>
        {:else}
          <div class="equip-slot equip-slot-empty" data-slot={i}>{["Q","E","W","R","T"][i] ?? i + 1}</div>
        {/if}
      {/each}
    </div>
  </div>

  <div class="section-label catalog-label">ALL SPELLS</div>

  <div id="loadout-grid" class="grid">
    {#if loading}
      <div class="loading">Loading…</div>
    {:else if loadError}
      <div class="loading">Failed to load spells.</div>
    {:else}
      {#each sortedSpells as entry (entry.id)}
        <ItemTile name={entry.name} level={entry.level} copies={entry.copies}
                  owned={entry.owned || entry.signature} equipped={entry.equipped}
                  locked={!entry.owned && !entry.signature}
                  tone={entry.signature ? "#6cc0ff" : "#d8a84e"} selected={selectedId === entry.id}
                  onclick={() => selectedId = entry.id}>
          {#snippet iconRender()}<div class="sp-ico" use:spellIcon={entry}></div>{/snippet}
        </ItemTile>
      {/each}
    {/if}
  </div>

  {#if detail && sel}
    <ItemDetail entry={detail} {busy} onClose={() => selectedId = null} onLevelUp={levelUp} onEquip={toggleEquip}>
      {#snippet iconRender()}<div class="sp-ico-lg" use:spellIcon={sel}></div>{/snippet}
    </ItemDetail>
  {/if}
</div>

<style>
  .root {
    position: relative; min-height: 100cqh; width: 100%;
    color: var(--text); font-family: var(--font-body);
    display: flex; flex-direction: column; box-sizing: border-box;
    padding: env(safe-area-inset-top,0px) env(safe-area-inset-right,0px) env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px);
    background: radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
  }
  .header { display: flex; align-items: center; gap: var(--sp-2); padding: max(12px, env(safe-area-inset-top,0px)) 12px 8px 12px; flex-shrink: 0; }
  .title-wrap { flex: 1; display: flex; flex-direction: column; align-items: center; gap: 1px; }
  .title { margin: 0; font-family: var(--font-display); font-size: var(--fs-title); font-weight: 700;
    letter-spacing: 0.05em; color: var(--gold-bright); text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25); text-align: center; }
  .char-sub { font-family: var(--font-body); font-size: var(--fs-caption); font-weight: 700; color: var(--text-dim); letter-spacing: 0.06em; text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
  .buy-row { display: flex; align-items: center; gap: 14px; padding: 6px 16px 2px; }
  .buy-row .slots-count { font-size: var(--fs-caption); font-weight: 700; color: var(--gold-bright,#ffd970); white-space: nowrap; }
  .buy-row .buy-wrap { flex: 1; max-width: 320px; margin-left: auto; }
  .equipped-section { padding: var(--sp-2) var(--sp-4) var(--sp-1h); flex-shrink: 0; display: flex; flex-direction: column; align-items: center; }
  .section-label {
    display: inline-flex; align-items: center; justify-content: center; min-height: 28px; padding: 2px 22px; margin-bottom: var(--sp-2);
    background: none; border-style: solid; border-width: 9px 28px; border-image: url('/ui/NameBlock.png') 40 120 40 120 fill stretch;
    font-family: var(--font-display); font-size: var(--fs-caption); font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase;
    color: var(--gold-bright); text-shadow: 0 1px 3px rgba(0,0,0,0.95); white-space: nowrap;
  }
  .catalog-label { align-self: center; margin: 6px auto 2px; }
  .equipped-row { display: flex; gap: var(--sp-2h); justify-content: center; flex-wrap: wrap; }
  .equip-slot {
    width: 72px; height: 78px; position: relative; background: none; border-style: solid; border-width: 7px;
    border-image: url('/ui/Kvadrat.png') 14 14 14 14 fill stretch; display: flex; flex-direction: column; align-items: center; justify-content: center;
    font-family: var(--font-display); font-size: var(--fs-xl); color: rgba(216,168,78,0.35); flex-shrink: 0; gap: 2px;
  }
  .equip-slot-filled { filter: drop-shadow(0 0 6px rgba(255,190,80,0.45)); }
  .equip-slot-sig { filter: drop-shadow(0 0 7px rgba(120,180,255,0.6)); }
  .slot-icon { width: 40px; height: 40px; display: flex; align-items: center; justify-content: center; }
  .slot-icon :global(img) { width: 38px; height: 38px; object-fit: contain; }
  .slot-label { font-family: var(--font-body); font-size: var(--fs-tiny); color: var(--text-dim); text-align: center; line-height: 1.1; max-width: 62px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .sig-tag { position: absolute; top: -8px; right: -6px; font-size: 9px; font-weight: 900; color: #cfe6ff; background: rgba(40,80,150,0.9); border: 1px solid #6cf; padding: 0 4px; border-radius: 3px; letter-spacing: 0.08em; }

  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(86px, 1fr)); gap: 10px; padding: 10px 14px 28px; overflow-y: auto; flex: 1; }
  .loading { grid-column: 1/-1; text-align: center; color: var(--text-dim); padding: 30px; }
  /* Spell icons render via the buildSpellIcon action inside the shared ItemTile / ItemDetail icon slots. */
  .sp-ico { width: 56px; height: 56px; display: flex; align-items: center; justify-content: center; font-size: 26px; font-weight: 800; color: var(--gold-bright); }
  .sp-ico :global(img) { width: 52px; height: 52px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6)); }
  .sp-ico-lg { width: 52px; height: 52px; display: flex; align-items: center; justify-content: center; font-size: 26px; font-weight: 800; color: var(--gold-bright); }
  .sp-ico-lg :global(img) { width: 52px; height: 52px; object-fit: contain; }
</style>
