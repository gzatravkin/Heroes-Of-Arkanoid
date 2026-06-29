<script lang="ts">
  import type { CharactersResponse, HeroProgressView } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import BuyRandom from "../ui/BuyRandom.svelte";
  import { perkAtStar } from "../ui/heroPerks";

  const ICON_FILES: Record<string, string> = {
    FireHeroBall: "/art/FireHeroBall.png", HPFull: "/art/HPFull.png",
    FireTurretIco: "/art/FireTurretIco.png", MPFull: "/art/MPFull.png",
  };
  const CLASS_ART: Record<string, { banner: string; ico: string }> = {
    fire_mage:   { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/FireHeroIco.png" },
    paladin:     { banner: "/ui/ClassChoiceKnight.png", ico: "/ui/KnightHeroIco.png" },
    engineer:    { banner: "/ui/ClassChoiceTechno.png", ico: "/ui/TechnoHeroIco.png" },
    necromancer: { banner: "/ui/ClassChoiceMage.png",   ico: "/ui/NecrHeroIco.png" },
  };
  const UNLOCK_HINTS: Record<string, string> = {
    paladin:     "Beat the Hell boss to add to the pool, then Buy Random Hero",
    engineer:    "Beat the Caverns boss to add to the pool, then Buy Random Hero",
    necromancer: "Beat the Witchland boss to add to the pool, then Buy Random Hero",
  };

  function iconSrc(key: string) { return ICON_FILES[key] ?? "/art/ItemGem.png"; }

  let data       = $state<CharactersResponse | null>(null);
  let selectedId = $state("");
  let ascendId   = $state<string | null>(null);   // hero whose ascend modal is open
  let busy       = $state(false);

  async function load() {
    data = await metaApi.getCharacters();
    selectedId = data.selected ?? "";
  }

  async function select(id: string) {
    if (id === selectedId) return;
    await metaApi.selectCharacter(id);
    await load();
  }

  function prog(id: string): HeroProgressView | undefined { return data?.progress?.[id]; }

  async function ascend(id: string) {
    if (busy) return;
    busy = true;
    try { const r = await metaApi.ascendHero(id); if (r.ok) await load(); }
    finally { busy = false; }
  }

  load().catch(console.error);

  let selectable = $derived(
    data ? (data.unlocked.length === 0 ? data.characters.map(c => c.id) : data.unlocked) : []
  );
  // The hero whose ascend modal is open (with its live progress), for the upgrade UI.
  let ascendHeroData = $derived.by(() => {
    if (!ascendId || !data) return null;
    const char = data.characters.find(c => c.id === ascendId);
    const pr = prog(ascendId);
    if (!char || !pr) return null;
    const next = pr.stars + 1;
    return { char, pr, next, perk: perkAtStar(ascendId, next) };
  });
</script>

<div id="character-scene" class="root">
  <div class="bg"></div>
  <div class="inner">
    <div class="ui-topbar">
      <button class="ui-back" aria-label="Back to menu" onclick={() => navigateTo("/?scene=menu")}></button>
      <h1 class="ui-title">Heroes</h1>
      <CurrencyBar />
    </div>
    <div class="hero-actions">
      <button id="btn-open-loadout" class="loadout-btn" aria-label="Edit spell loadout"
              onclick={() => navigateTo("/?scene=loadout")}>Loadout</button>
      <div class="buy-wrap"><BuyRandom kind="hero" noun="Hero" onrolled={load} /></div>
    </div>
    <div class="content">
      <div id="character-list" class="list" data-selected={selectedId}>
        {#each data?.characters ?? [] as char}
          {@const isSelected   = char.id === selectedId}
          {@const isSelectable = selectable.includes(char.id)}
          {@const art = CLASS_ART[char.id]}
          <div data-character={char.id}
               class="card {isSelected ? 'selected' : ''} {isSelectable ? '' : 'locked'}"
               onclick={() => isSelectable && select(char.id)}
               role={isSelectable ? "button" : undefined}
               tabindex={isSelectable ? 0 : undefined}>
            {#if art}
              <div class="banner" style="background-image:url('{art.banner}')">
                <img src={art.ico} alt={char.name} class="hero-ico" />
                <span class="banner-name">{char.name}</span>
              </div>
            {:else}
              <img src={iconSrc(char.icon)} alt={char.name} class="fallback-ico" />
              <div class="banner-name banner-name--standalone">{char.name}</div>
            {/if}
            {#if isSelected}
              <div class="selected-chip">SELECTED</div>
            {/if}
            {#if isSelectable}
              {@const pr = prog(char.id)}
              <div class="ascend-row">
                <span class="stars" aria-label="{pr?.stars ?? 0} of {pr?.maxStars ?? 6} stars">
                  {#each Array(pr?.maxStars ?? 6) as _, i}<span class="star {i < (pr?.stars ?? 0) ? 'on' : ''}">{i < (pr?.stars ?? 0) ? "★" : "☆"}</span>{/each}
                </span>
                {#if pr && pr.ascendCost > 0}
                  <button class="ascend-btn {pr.canAscend ? 'ready' : ''}"
                          disabled={!pr.canAscend}
                          onclick={(e) => { e.stopPropagation(); if (pr.canAscend) ascendId = char.id; }}>
                    {pr.canAscend ? `Ascend ★${pr.stars + 1}` : `★${pr.stars + 1}: ${pr.pips}/${pr.ascendCost}`}
                  </button>
                {:else if pr}
                  <span class="ascend-max">★ MAX</span>
                {/if}
              </div>
            {/if}
            {#if char.spells?.length}
              <div class="kit">
                <span class="kit-label">Spells</span>
                <span class="kit-names">{char.spells.map(s => s.name).join(" · ")}</span>
              </div>
            {/if}
            <div class="passive">
              {isSelectable ? char.passive : (UNLOCK_HINTS[char.id] ?? "Locked")}
            </div>
            {#if !isSelectable}
              <div class="lock-badge">🔒</div>
            {/if}
          </div>
        {/each}
      </div>
    </div>
  </div>

  <!-- ── Ascend upgrade modal (what the next ★ grants) ── -->
  {#if ascendHeroData}
    {@const a = ascendHeroData}
    <div class="asc-ovl" role="button" tabindex="-1"
         onclick={() => ascendId = null}
         onkeydown={(e) => { if (e.key === "Escape" || e.key === "Enter" || e.key === " ") ascendId = null; }}>
      <div class="asc-card" role="dialog" tabindex="-1" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
        <button class="asc-x" aria-label="Close" onclick={() => ascendId = null}>✕</button>
        <div class="asc-title">Ascend {a.char.name}</div>
        <div class="asc-stars">
          <span class="asc-from">{"★".repeat(a.pr.stars)}{"☆".repeat(a.pr.maxStars - a.pr.stars)}</span>
          <span class="asc-arrow">→</span>
          <span class="asc-to">{"★".repeat(a.next)}{"☆".repeat(a.pr.maxStars - a.next)}</span>
        </div>
        <div class="asc-gains">
          <div class="asc-gain"><span class="asc-plus">+8%</span> all hero stats (Power, Vitality, Crit…)</div>
          {#if a.perk}
            <div class="asc-gain asc-perk"><span class="asc-star">★{a.next}</span> {a.perk.text}{a.perk.soon ? " (soon)" : ""}</div>
          {/if}
        </div>
        <div class="asc-cost">Spends {a.pr.ascendCost} duplicate pips ({a.pr.pips} banked)</div>
        <button class="asc-confirm" disabled={busy || !a.pr.canAscend}
                onclick={async () => { await ascend(a.char.id); ascendId = null; }}>
          Ascend to ★{a.next}
        </button>
      </div>
    </div>
  {/if}
</div>

<style>
  .root {
    position: relative; height: 100cqh; overflow-y: auto; overflow-x: hidden;
    display: flex; flex-direction: column; font-family: var(--font-body); color: var(--text);
    -webkit-font-smoothing: antialiased;
  }
  .bg {
    position: absolute; inset: 0;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none; z-index: 0;
  }
  .inner { flex: 1; min-height: 0; position: relative; z-index: 1; display: flex; flex-direction: column; }
  .hero-actions {
    display: flex; align-items: center; gap: var(--sp-3);
    padding: var(--sp-2) var(--sp-4) 0; width: 100%; max-width: 520px; align-self: center;
  }
  .hero-actions .buy-wrap { flex: 1; }
  .content {
    flex: 1; min-height: 0; overflow-y: auto; display: flex; flex-direction: column; align-items: center;
    padding: var(--sp-1) var(--sp-3h) max(env(safe-area-inset-bottom,0px), var(--sp-3h));
  }
  .list {
    display: flex; flex-direction: column;
    gap: var(--sp-3); width: 100%; max-width: 480px;
  }
  .card {
    /* Content-height (was flex:1, which split the viewport into 4 too-short cards and overflowed
       the spell list outside the border). The page (.root) scrolls instead. */
    background: none; border-style: solid; border-width: 13px 15px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-2h) var(--sp-3) var(--sp-3h);
    display: flex; flex-direction: column; position: relative; cursor: pointer;
    box-sizing: border-box; transition: filter var(--dur-normal), transform var(--dur-normal);
    touch-action: manipulation; -webkit-tap-highlight-color: transparent;
  }
  .card:not(.selected):not(.locked) { filter: brightness(0.88); }
  .card.selected { filter: drop-shadow(0 0 10px rgba(255,190,80,0.6)); }
  .card:hover:not(.locked):not(.selected) { filter: brightness(1.1); transform: scale(1.01); }
  .card:hover.selected { filter: drop-shadow(0 0 16px rgba(255,190,80,0.85)); }
  .card:active:not(.locked) { transform: scale(0.97); }
  .card:not(.locked):focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .card.locked { filter: var(--filter-locked); cursor: default; }
  .banner {
    position: relative; width: 100%; height: 72px; flex-shrink: 0;
    background-size: auto 100%; background-position: left center; background-repeat: no-repeat;
    border-radius: 4px; display: flex; align-items: center; margin-bottom: var(--sp-2);
    -webkit-mask-image: linear-gradient(90deg, black 72%, transparent 100%);
    mask-image: linear-gradient(90deg, black 72%, transparent 100%);
  }
  .hero-ico {
    position: absolute; left: 2px; top: 50%; transform: translateY(-50%);
    width: 68px; height: 68px; object-fit: contain; z-index: 2;
    filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7));
  }
  .banner-name {
    position: absolute; left: 76px; right: 28px;
    font-family: var(--font-display); font-size: var(--fs-large); font-weight: 700;
    color: var(--gold-bright); text-shadow: 0 1px 3px rgba(0,0,0,0.95), 0 0 10px rgba(200,140,30,0.35);
    letter-spacing: 0.06em; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  }
  .banner-name--standalone {
    position: static; display: block; margin: var(--sp-1h) 0 var(--sp-2h); font-size: var(--fs-large);
  }
  .selected-chip {
    display: inline-flex; align-self: flex-start; align-items: center; justify-content: center;
    font-family: var(--font-display); font-size: var(--fs-tiny); font-weight: 900;
    letter-spacing: 0.16em; color: var(--gold-bright); text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    padding: 2px 10px; margin-bottom: var(--sp-1h); border: 1px solid var(--gold);
    border-radius: 3px; background: rgba(216,168,78,0.18); min-height: 20px;
  }
  /* ── ★ ascension row ── */
  .ascend-row { display: flex; align-items: center; gap: var(--sp-2); padding: 0 4px; margin-bottom: var(--sp-1h); }
  .stars { letter-spacing: 1px; line-height: 1; }
  .star { font-size: 15px; color: #5a4a30; }
  .star.on { color: var(--gold-bright); text-shadow: 0 0 6px rgba(255,210,80,0.7); }
  .ascend-max { font-size: var(--fs-tiny); font-weight: 800; letter-spacing: 0.1em; color: var(--gold-bright); }
  .ascend-btn {
    margin-left: auto; min-height: 30px; padding: 3px 12px; border-radius: 7px; cursor: pointer;
    font-family: var(--font-body); font-weight: 800; font-size: var(--fs-tiny); white-space: nowrap;
    color: var(--text-dim); background: rgba(0,0,0,0.35); border: 1px solid rgba(150,110,50,0.5);
    transition: filter var(--dur-fast), transform var(--dur-fast);
  }
  .ascend-btn.ready { color: #1a1208; background: linear-gradient(180deg, #ffe06a, #d89a2e); border-color: #ffe08a;
    box-shadow: 0 0 10px rgba(255,200,80,0.5); }
  .ascend-btn.ready:hover { filter: brightness(1.12); }
  .ascend-btn:active:not(:disabled) { transform: scale(0.96); }
  .ascend-btn:disabled { cursor: default; }

  /* ── Ascend upgrade modal ── */
  .asc-ovl { position: fixed; inset: 0; z-index: 260; display: flex; align-items: center; justify-content: center;
    background: rgba(0,0,0,0.76); padding: 20px; animation: asc-fade 0.18s ease; }
  @keyframes asc-fade { from { opacity: 0; } to { opacity: 1; } }
  .asc-card { position: relative; width: min(340px, 92cqw);
    background: linear-gradient(180deg, rgba(40,30,14,0.99), rgba(18,12,7,0.99));
    border: 2px solid var(--gold-bright); border-radius: 16px; padding: var(--sp-5) var(--sp-4) var(--sp-4);
    box-shadow: 0 8px 40px rgba(0,0,0,0.7), 0 0 30px rgba(255,200,80,0.4); text-align: center;
    animation: asc-pop 0.3s cubic-bezier(0.2,1.2,0.4,1); color: var(--text); }
  @keyframes asc-pop { from { transform: scale(0.85) translateY(8px); opacity: 0; } to { transform: scale(1) translateY(0); opacity: 1; } }
  .asc-x { position: absolute; top: 8px; right: 10px; background: none; border: none; color: var(--text-dim); font-size: 18px; cursor: pointer; padding: 4px; }
  .asc-x:hover { color: var(--gold-bright); }
  .asc-title { font-family: var(--font-display); font-size: var(--fs-large); font-weight: 800; color: var(--gold-bright); margin-bottom: var(--sp-3); }
  .asc-stars { display: flex; align-items: center; justify-content: center; gap: var(--sp-2h); font-size: 20px; margin-bottom: var(--sp-3); }
  .asc-from { color: #5a4a30; }
  .asc-to { color: var(--gold-bright); text-shadow: 0 0 8px rgba(255,210,80,0.7); }
  .asc-arrow { color: var(--text-dim); font-size: 16px; }
  .asc-gains { display: flex; flex-direction: column; gap: var(--sp-2); margin-bottom: var(--sp-3); text-align: left; }
  .asc-gain { font-size: var(--fs-caption); color: var(--text); background: rgba(0,0,0,0.3); border-radius: 8px; padding: 8px 10px; }
  .asc-plus { font-weight: 800; color: #7fe3a0; }
  .asc-perk .asc-star { font-weight: 800; color: var(--gold-bright); }
  .asc-cost { font-size: var(--fs-tiny); color: var(--text-dim); margin-bottom: var(--sp-3); }
  .asc-confirm { width: 100%; min-height: 46px; border: none; border-radius: 10px; cursor: pointer;
    font-family: var(--font-body); font-weight: 800; font-size: var(--fs-body); color: #1a1208;
    background: linear-gradient(180deg, #ffe06a, #d89a2e); box-shadow: 0 2px 10px rgba(0,0,0,0.4);
    transition: filter var(--dur-fast), transform var(--dur-fast); }
  .asc-confirm:hover:not(:disabled) { filter: brightness(1.12); }
  .asc-confirm:active:not(:disabled) { transform: scale(0.97); }
  .asc-confirm:disabled { filter: grayscale(0.6) brightness(0.6); cursor: default; }

  .kit {
    display: flex; align-items: baseline; gap: var(--sp-1h); flex-wrap: wrap;
    padding: 0 4px; margin-bottom: var(--sp-1h);
  }
  .kit-label {
    font-family: var(--font-display); font-size: var(--fs-tiny); font-weight: 700;
    letter-spacing: 0.12em; color: var(--gold); text-transform: uppercase;
    opacity: 0.9; flex-shrink: 0;
  }
  .kit-names {
    font-size: var(--fs-caption); color: var(--text); line-height: 1.35;
    text-shadow: 0 1px 2px rgba(0,0,0,0.6);
  }
  .loadout-btn {
    flex-shrink: 0; min-height: 36px; padding: 4px 14px;
    background: none; border-style: solid; border-width: 7px 14px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    color: var(--gold-bright); font-family: var(--font-body); font-size: var(--fs-caption);
    font-weight: 700; letter-spacing: 0.04em; cursor: pointer;
    touch-action: manipulation; -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    text-shadow: 0 1px 2px var(--shadow-hard);
  }
  .loadout-btn:hover  { filter: brightness(1.15); }
  .loadout-btn:active { transform: scale(0.96); }
  .loadout-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .passive { font-size: var(--fs-caption); color: var(--text-dim); line-height: 1.4; padding: 0 4px; margin-top: auto; }
  .lock-badge { position: absolute; bottom: 11px; right: 13px; font-size: var(--fs-section); opacity: 0.75; line-height: 1; pointer-events: none; }
  .fallback-ico { width: 64px; height: 64px; object-fit: contain; display: block; margin: 0 auto 8px; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6)); }
  @container (min-width: 480px) {
    .list { max-width: 520px; }
    .banner { height: 80px; }
    .hero-ico { width: 74px; height: 74px; }
  }
</style>
