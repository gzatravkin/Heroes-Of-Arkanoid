<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { CardsResponse, CardDef } from "../net/metaApi";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import BuyRandom from "../ui/BuyRandom.svelte";
  import ItemTile from "../ui/ItemTile.svelte";
  import ItemDetail, { type DetailEntry } from "../ui/ItemDetail.svelte";
  import { cardIcon, rarityColor, sortEquippedFirst } from "../ui/collection";

  let data = $state<CardsResponse | null>(null);
  let busy = $state(false);
  let selected = $state<string | null>(null);

  async function load() { data = await metaApi.getCards(); }
  load();

  let equippedSet = $derived(new Set(data?.equipped ?? []));
  let slotsUsed   = $derived(data?.equipped.length ?? 0);
  // Equipped cards float to the FRONT (in equip order), then the rest in catalog order.
  let defs = $derived(sortEquippedFirst(data?.cards ?? [],
    (c) => { const i = (data?.equipped ?? []).indexOf(c.id); return i >= 0 ? i : 1000; }));
  function own(id: string) { return data?.owned[id]; }

  let detail = $derived.by<DetailEntry | null>(() => {
    if (!selected || !data) return null;
    const c = data.cards.find(x => x.id === selected);
    if (!c) return null;
    const o = data.owned[c.id];
    return {
      id: c.id, name: c.name, icon: cardIcon(c.id), tone: rarityColor(c.rarity),
      meta: c.rarity.toUpperCase(), description: c.description,
      owned: !!o, level: o?.level ?? 0, copies: o?.copies ?? 0, maxLevel: data.maxLevel,
      equipped: equippedSet.has(c.id), equippable: true,
    };
  });

  async function act(fn: () => Promise<unknown>) {
    if (busy) return; busy = true;
    try { await fn(); await load(); } finally { busy = false; }
  }
  function toggleEquip(id: string) {
    act(() => equippedSet.has(id) ? metaApi.unequipCard(id) : metaApi.equipCard(id));
  }
  function levelUp(id: string) { act(() => metaApi.cardLevelUp(id)); }
</script>

<div class="cards-root">
  <div class="ui-topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">Items</h1>
    <CurrencyBar />
  </div>

  <div class="buy-row">
    <span class="slots">Equipped {slotsUsed}/{data?.slots ?? 0}</span>
    <div class="buy-wrap"><BuyRandom kind="card" noun="Item" onrolled={load} /></div>
  </div>

  {#if data}
    <div class="grid">
      {#each defs as c (c.id)}
        {@const o = own(c.id)}
        <ItemTile icon={cardIcon(c.id)} name={c.name}
                  level={o?.level ?? 0} copies={o?.copies ?? 0}
                  owned={!!o} equipped={equippedSet.has(c.id)} locked={!o}
                  tone={rarityColor(c.rarity)} selected={selected === c.id}
                  onclick={() => selected = c.id} />
      {/each}
    </div>
  {:else}
    <div class="loading">Loading…</div>
  {/if}

  {#if detail}
    <ItemDetail entry={detail} {busy}
                onClose={() => selected = null}
                onLevelUp={levelUp} onEquip={toggleEquip} />
  {/if}
</div>

<style>
  .cards-root { min-height: 100cqh; display: flex; flex-direction: column;
    background: radial-gradient(ellipse at 50% 0%, rgba(120,80,20,.30), transparent 60%), linear-gradient(180deg,#140d06,#0a0a12 55%,#050406);
    color: #efe6d6; font-family: var(--font-body); padding-bottom: 30px; }
  .buy-row { display: flex; align-items: center; gap: 14px; padding: 10px 14px 4px; }
  .slots { color: #c8b890; font-weight: 700; white-space: nowrap; font-size: var(--fs-caption); }
  .buy-wrap { flex: 1; max-width: 320px; margin-left: auto; }
  .grid {
    display: grid; grid-template-columns: repeat(auto-fill, minmax(86px, 1fr));
    gap: 10px; padding: 10px 14px 24px;
  }
  .loading { padding: 40px; text-align: center; color: var(--text-dim,#a99fce); }
  @container (min-width: 520px) { .grid { grid-template-columns: repeat(6, 1fr); } }
</style>
