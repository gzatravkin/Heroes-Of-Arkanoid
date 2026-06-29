<script lang="ts">
  import type { Profile, CharactersResponse, HeroStatsResult } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import CurrencyBar from "../ui/CurrencyBar.svelte";

  // §5.5 hero perks unlocked at ★1/★3/★5 — shown so the player knows what ascending grants.
  const PERKS: Record<string, { star: number; text: string; soon?: boolean }[]> = {
    fire_mage: [
      { star: 1, text: "+5% Crit Chance" },
      { star: 3, text: "Ignited blocks take +15% from crits" },
      { star: 5, text: "A crit kill ignites a nearby block" },
    ],
    paladin: [
      { star: 1, text: "+0.2 Crit Damage" },
      { star: 3, text: "First ball-drain each level is saved" },
      { star: 5, text: "Below 50% HP, +25% Crit Damage" },
    ],
    engineer: [
      { star: 1, text: "+1 Tempo step" },
      { star: 3, text: "+1 starting ball" },
      { star: 5, text: "Extra balls deal full damage", soon: true },
    ],
    necromancer: [
      { star: 1, text: "Heal 1 HP per 60 kills" },
      { star: 3, text: "Crits drain mana to you" },
      { star: 5, text: "Full-combo kill may raise a helper-ball" },
    ],
  };

  // §5.6 mastery nodes (account-wide, spend the shared Skill-Points pool).
  const MASTERIES = [
    { id: "sharpshooter", name: "Sharpshooter", effect: "+1% Crit Chance / lvl", max: 5 },
    { id: "brutality",    name: "Brutality",    effect: "+0.05 Crit Damage / lvl", max: 5 },
    { id: "conditioning", name: "Conditioning", effect: "+1 Max HP / lvl", max: 3 },
    { id: "juggler",      name: "Juggler",      effect: "+1 Multiball / lvl", max: 2 },
    { id: "momentum",     name: "Momentum",     effect: "+2% Tempo / lvl", max: 5 },
  ];

  let allData = $state<CharactersResponse | null>(null);
  let profile = $state<Profile | null>(null);
  let hero    = $state<HeroStatsResult | null>(null);
  let activeId = $state("");

  let activeChar = $derived(allData?.characters.find(c => c.id === activeId));

  async function loadAll() {
    [allData, profile] = await Promise.all([metaApi.getCharacters(), metaApi.getProfile()]);
    activeId = allData.selected ?? allData.characters[0]?.id ?? "fire_mage";
    await loadHero();
  }

  async function loadHero() {
    if (!activeId) return;
    hero = await metaApi.getHeroStats(activeId);
  }

  async function pick(id: string) { activeId = id; await loadHero(); }

  async function buyMastery(node: string) {
    const data = await metaApi.mastery(node);
    if (data.ok) { profile = data.profile; await loadHero(); }
  }

  async function respec() {
    const data = await metaApi.resetMasteries();
    if (data.ok) { profile = data.profile; await loadHero(); }
  }
  const masteryCost = (lvl: number) => 25 * (lvl + 1); // mirrors Upgrades.MasteryCost

  const pct = (n: number) => `${(n * 100).toFixed(1)}%`;
  const mult = (n: number) => `×${n.toFixed(2)}`;

  loadAll().catch(console.error);
</script>

<div id="masteries-scene" class="root">
  <div class="bg"></div>
  <div class="inner">
    <div class="sk-topbar">
      <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
      <h1 class="ui-title">Masteries</h1>
      <CurrencyBar />
    </div>
    <div class="points-row">
      <span class="insight-note">Masteries cost <span class="ui-coin ui-coin-insight">{profile?.insight ?? 0}</span></span>
      <button class="respec-btn" onclick={respec}
              disabled={(profile?.souls ?? 0) < 60 || Object.keys(profile?.masteries ?? {}).length === 0}
              title="Refund all Insight spent on masteries for a 60-Souls fee">Respec · 60 ◆</button>
    </div>

    <div id="ms-tabs" class="tabs" role="tablist">
      {#each allData?.characters ?? [] as ch}
        <button class="tab {ch.id === activeId ? 'active' : ''}"
          role="tab" aria-selected={ch.id === activeId}
          onclick={() => pick(ch.id)}>{ch.name}</button>
      {/each}
    </div>

    {#if hero}
      <!-- Hero header: level, XP, ★ ascension -->
      <div class="hero-card">
        <div class="hero-head">
          <span class="hero-name">{activeChar?.name ?? hero.hero}</span>
          <span id="ms-hero-level" class="hero-level">Lv {hero.level}</span>
        </div>
        <div class="xp-track" aria-label="XP">
          <div class="xp-fill" style="width:{Math.min(100, hero.level >= 30 ? 100 : (hero.exp / hero.xpToNext) * 100)}%"></div>
          <span class="xp-text">{hero.level >= 30 ? "MAX" : `${hero.exp} / ${hero.xpToNext} XP`}</span>
        </div>
        <div class="stars-row" id="ms-stars">
          {#each Array(6) as _, i}
            <span class="star {i < hero.stars ? 'on' : ''}">{i < hero.stars ? "★" : "☆"}</span>
          {/each}
        </div>
        <div class="ascend-row">
          {#if hero.stars >= 6}
            <span class="ascend-max">★6 MAX</span>
          {:else}
            <a class="ascend-hint" href="/?scene=characters">Bank duplicates, then Ascend ★ on the Heroes screen</a>
          {/if}
        </div>
        <div class="perks" id="ms-perks">
          {#each PERKS[hero.hero] ?? [] as perk}
            <div class="perk {hero.stars >= perk.star ? 'on' : 'off'}">
              <span class="perk-star">★{perk.star}</span>
              <span class="perk-text">{perk.text}{perk.soon ? " (soon)" : ""}</span>
            </div>
          {/each}
        </div>
      </div>

      <!-- Resolved stat block (§5.1) -->
      <div class="stat-grid" id="ms-stats">
        <div class="stat"><span class="lbl">Power</span><span class="val">{hero.stats.power.toFixed(1)}</span></div>
        <div class="stat"><span class="lbl">Vitality</span><span class="val">{Math.round(hero.stats.vitality)}</span></div>
        <div class="stat"><span class="lbl">Crit Chance</span><span class="val">{pct(hero.stats.critChance)}</span></div>
        <div class="stat"><span class="lbl">Crit Damage</span><span class="val">{mult(hero.stats.critDamage)}</span></div>
        <div class="stat"><span class="lbl">Multiball</span><span class="val">+{hero.stats.multiball}</span></div>
        <div class="stat"><span class="lbl">Tempo</span><span class="val">{mult(hero.stats.tempo)}</span></div>
      </div>
    {/if}

    <!-- Masteries (account-wide; shared Skill Points) -->
    <h2 class="section-title">Masteries</h2>
    <div class="mastery-grid">
      {#each MASTERIES as m}
        {@const lvl = profile?.masteries?.[m.id] ?? 0}
        {@const atMax = lvl >= m.max}
        {@const cost = masteryCost(lvl)}
        {@const canAfford = (profile?.insight ?? 0) >= cost && !atMax}
        <div class="mastery-card">
          <div class="m-head">
            <span class="m-name">{m.name}</span>
            <span id="ms-lvl-{m.id}" class="m-lvl">{lvl}/{m.max}</span>
          </div>
          <div class="m-effect">{m.effect}</div>
          <button id="ms-buy-{m.id}"
            class="upgrade-btn {canAfford ? 'can-afford' : 'cannot-afford'}"
            disabled={!canAfford}
            onclick={() => buyMastery(m.id)}>{atMax ? "MAX" : `+ Upgrade · ${cost}`}</button>
        </div>
      {/each}
    </div>
  </div>
</div>

<style>
  .root {
    position: relative; min-height: 100cqh; overflow-x: hidden; overflow-y: auto;
    font-family: var(--font-body); color: var(--text); box-sizing: border-box;
  }
  .bg {
    position: absolute; inset: 0; min-height: 100cqh;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    pointer-events: none; z-index: 0;
  }
  .inner {
    position: relative; z-index: 1;
    display: flex; flex-direction: column; align-items: center;
    padding: 0 16px max(env(safe-area-inset-bottom,0px),24px);
  }
  .sk-topbar {
    display: flex; align-items: center; gap: var(--sp-2);
    padding: max(12px, env(safe-area-inset-top,0px)) 0 8px;
    width: min(360px, 96cqw); align-self: center;
  }
  .points-row { display: flex; align-items: center; gap: var(--sp-2h); justify-content: center; margin: var(--sp-2h) 0 var(--sp-1h); flex-wrap: wrap; }
  .insight-note { display: inline-flex; align-items: center; gap: var(--sp-1h); font-size: var(--fs-caption); color: var(--text-dim); }
  .respec-btn {
    min-height: 36px; padding: 4px 14px; border-radius: 7px; cursor: pointer;
    font-family: var(--font-body); font-weight: 700; font-size: var(--fs-caption);
    color: #eaf3ff; border: 1px solid rgba(120,160,210,.55);
    background: linear-gradient(180deg, rgba(50,90,150,.7), rgba(30,55,100,.8));
    transition: filter var(--dur-fast);
  }
  .respec-btn:hover:not(:disabled) { filter: brightness(1.15); }
  .respec-btn:disabled { filter: saturate(.3) brightness(.6); cursor: default; }
  .ascend-hint { color: var(--gold-bright,#ffd970); font-size: var(--fs-caption); font-weight: 700; text-decoration: none; }
  .tabs {
    display: flex; gap: var(--sp-2); flex-wrap: wrap;
    justify-content: center; margin-bottom: var(--sp-3); width: min(360px, 96cqw);
  }
  .tab {
    min-height: 44px; padding: 0 14px; cursor: pointer;
    background: none; border-style: solid; border-width: 8px 30px;
    border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
    font-family: var(--font-body); font-size: var(--fs-caption);
    font-weight: 700; color: var(--text-dim);
    filter: saturate(0.4) brightness(0.75);
    -webkit-tap-highlight-color: transparent; touch-action: manipulation;
    transition: filter var(--dur-normal), color var(--dur-normal);
  }
  .tab.active { filter: none; color: var(--text); }
  .tab:hover:not(.active) { filter: saturate(0.6) brightness(0.9); }
  .tab:active { transform: scale(0.96); }

  .hero-card {
    width: min(360px, 96cqw); margin-bottom: var(--sp-3);
    background: none; border-style: solid; border-width: 12px 14px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-3) var(--sp-3); display: flex; flex-direction: column; gap: var(--sp-2);
  }
  .hero-head { display: flex; justify-content: space-between; align-items: baseline; }
  .hero-name { font-family: var(--font-display); font-size: var(--fs-h2); font-weight: 700;
    color: var(--gold-bright); text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
  .hero-level { font-size: var(--fs-body); font-weight: 700; color: var(--text); }
  .xp-track {
    position: relative; height: 18px; border-radius: 9px; overflow: hidden;
    background: rgba(0,0,0,0.45); border: 1px solid var(--gold-dim);
  }
  .xp-fill { position: absolute; inset: 0 auto 0 0; height: 100%;
    background: linear-gradient(90deg, #6cc0ff, #3a8fd8); transition: width var(--dur-normal); }
  .xp-text { position: relative; z-index: 1; display: block; text-align: center;
    font-size: var(--fs-tiny); font-weight: 700; line-height: 18px;
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
  .stars-row { display: flex; gap: 2px; justify-content: center; }
  .star { font-size: 22px; color: #5a4a30; line-height: 1; }
  .star.on { color: var(--gold-bright); text-shadow: 0 0 6px rgba(255,210,59,0.8); }
  .ascend-row { display: flex; align-items: center; justify-content: space-between; gap: var(--sp-2); }
  .ascend-max { font-size: var(--fs-caption); font-weight: 700; color: var(--gold-bright); }
  .perks { display: flex; flex-direction: column; gap: 3px; margin-top: 2px;
    border-top: 1px solid rgba(216,168,78,0.25); padding-top: var(--sp-1h); }
  .perk { display: flex; gap: var(--sp-1h); align-items: baseline; font-size: var(--fs-tiny); line-height: 1.25; }
  .perk-star { flex-shrink: 0; font-weight: 700; min-width: 22px; }
  .perk.on  .perk-star { color: var(--gold-bright); text-shadow: 0 0 5px rgba(255,210,59,0.7); }
  .perk.on  .perk-text { color: var(--text); }
  .perk.off .perk-star { color: #5a4a30; }
  .perk.off .perk-text { color: var(--text-dim); opacity: 0.7; }
  .stat-grid {
    display: grid; grid-template-columns: repeat(3, 1fr); gap: var(--sp-2);
    width: min(360px, 96cqw); margin-bottom: var(--sp-4);
  }
  .stat {
    display: flex; flex-direction: column; align-items: center; gap: 2px;
    padding: var(--sp-2) var(--sp-1);
    background: none; border-style: solid; border-width: 7px;
    border-image: url('/ui/Kvadrat.png') 14 14 14 14 fill stretch;
  }
  .stat .lbl { font-size: var(--fs-tiny); color: var(--text-dim); text-align: center; }
  .stat .val { font-size: var(--fs-body); font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); }

  .section-title {
    font-family: var(--font-display); font-size: var(--fs-h2); font-weight: 700;
    color: var(--gold-bright); text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    margin: 0 0 var(--sp-2); width: min(360px, 96cqw); text-align: left;
  }
  .mastery-grid {
    display: grid; grid-template-columns: repeat(2, 1fr); gap: var(--sp-3);
    width: min(360px, 96cqw); margin-bottom: var(--sp-3);
  }
  .mastery-card {
    background: none; border-style: solid; border-width: 12px 14px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-2h) var(--sp-2); display: flex; flex-direction: column;
    gap: var(--sp-1h); position: relative;
  }
  .m-head { display: flex; justify-content: space-between; align-items: baseline; }
  .m-name { font-size: var(--fs-caption); font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
  .m-lvl { font-size: var(--fs-caption); font-weight: 700; color: var(--text); }
  .m-effect { font-size: var(--fs-tiny); color: var(--text-dim); line-height: 1.3; min-height: 2.2em; }
  .upgrade-btn {
    width: 100%; min-height: 40px;
    background: none; border-style: solid; border-width: 8px 18px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    cursor: pointer; font-family: var(--font-body); font-size: var(--fs-caption);
    font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px var(--shadow-hard); letter-spacing: 0.04em;
    -webkit-tap-highlight-color: transparent; touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .upgrade-btn:hover:not(:disabled)  { filter: brightness(1.15); }
  .upgrade-btn:active:not(:disabled) { transform: scale(0.96); }
  .upgrade-btn:disabled, .upgrade-btn.cannot-afford {
    filter: saturate(0.25) brightness(0.65); cursor: default;
  }
  @container (min-width: 480px) {
    .mastery-grid { grid-template-columns: repeat(3, 1fr); }
  }
</style>
