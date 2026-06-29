<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { ACHIEVEMENTS, badgeSrc } from "./AchievementsScene";

  let unlocked = $state(new Set<string>());
  let loading   = $state(true);

  let unlockedCount = $derived(ACHIEVEMENTS.filter(a => unlocked.has(a.id)).length);

  async function load() {
    const profile = await metaApi.getProfile();
    unlocked = new Set(profile.achievements ?? []);
    loading = false;
  }

  load().catch(console.error);
</script>

<div id="achievements-scene" class="root">
  <div class="bg"></div>
  <div class="inner">
    <div class="topbar">
      <a href="/?scene=menu" class="back" aria-label="Back to menu"></a>
      <h1 class="title">Achievements</h1>
      <div class="spacer"></div>
    </div>
    <div id="ach-summary" class="summary">
      {#if loading}Loading…{:else}{unlockedCount} / {ACHIEVEMENTS.length} unlocked{/if}
    </div>
    <div id="ach-grid" class="grid">
      {#each ACHIEVEMENTS as ach}
        {@const isUnlocked = unlocked.has(ach.id)}
        <div data-achievement={ach.id} class="card {isUnlocked ? 'unlocked' : 'locked'}">
          <img src={badgeSrc(ach.tier, isUnlocked)} alt={ach.name} class="badge" />
          <div class="name">{ach.name}</div>
          {#if isUnlocked}
            <div class="desc">{ach.description}</div>
          {:else}
            <div class="desc criteria">{ach.criteria}</div>
          {/if}
        </div>
      {/each}
    </div>
  </div>
</div>

<style>
  .root {
    position: relative; min-height: 100cqh; width: 100%;
    color: var(--text); font-family: var(--font-body);
    display: flex; flex-direction: column; box-sizing: border-box; overflow-x: hidden;
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
    display: flex; flex-direction: column; align-items: stretch;
    padding: 0 0 max(env(safe-area-inset-bottom,0px),24px);
  }
  .topbar {
    display: flex; align-items: center; gap: var(--sp-2);
    padding: max(12px, env(safe-area-inset-top,0px)) 12px 8px 12px;
    width: 100%; box-sizing: border-box;
  }
  .back {
    flex: none; width: 44px; height: 44px; padding: 0;
    display: flex; align-items: center; justify-content: center;
    text-decoration: none; cursor: pointer;
    -webkit-tap-highlight-color: transparent; touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    background: none; border-style: solid; border-width: 8px 14px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
  }
  .back::before {
    content: ""; width: 20px; height: 20px;
    background: url('/ui/BackArrow.png') no-repeat center / contain;
    filter: drop-shadow(0 1px 2px rgba(0,0,0,0.8));
  }
  .back:hover  { filter: brightness(1.18); }
  .back:active { transform: scale(0.94); }
  .back:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .title {
    flex: 1; text-align: center; margin: 0;
    font-family: var(--font-display); font-size: var(--fs-title);
    font-weight: 700; letter-spacing: 0.05em; color: var(--gold-bright);
    text-shadow: 0 2px 4px rgba(0,0,0,0.9), 0 0 18px rgba(255,180,60,0.25);
  }
  .spacer { width: 44px; flex: none; }
  .summary {
    text-align: center; color: var(--text-dim); font-size: var(--fs-body);
    letter-spacing: 0.04em; margin-bottom: var(--sp-3h); padding: 0 var(--sp-4);
  }
  .grid {
    display: grid; grid-template-columns: repeat(2, 1fr);
    gap: var(--sp-3); width: min(360px, 96cqw);
    padding-bottom: var(--sp-5); align-self: center;
  }
  .card {
    background: none; border-style: solid; border-width: 13px 15px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-2h) var(--sp-2);
    display: flex; flex-direction: column; align-items: center;
    gap: var(--sp-1h); position: relative;
    transition: filter var(--dur-normal), transform var(--dur-normal);
  }
  .card.unlocked { filter: drop-shadow(0 0 7px rgba(255,190,80,0.35)); }
  .card.unlocked:hover { filter: drop-shadow(0 0 10px rgba(255,190,80,0.55)) brightness(1.08); }
  .card.unlocked:active { transform: scale(0.96); }
  .card.locked:hover { filter: brightness(1.06); }
  .badge {
    width: 60px; height: 60px; object-fit: contain;
    filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
  }
  .card.locked .badge { filter: var(--filter-locked) drop-shadow(0 2px 4px rgba(0,0,0,0.6)); }
  .name {
    font-size: var(--fs-caption); font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); text-align: center; line-height: 1.3;
  }
  .card.locked .name { color: var(--text-dim); text-shadow: none; }
  .desc { font-size: var(--fs-tiny); color: var(--text-dim); text-align: center; line-height: 1.4; }
  /* Locked: show the actionable objective legibly (not faint flavour). */
  .card.locked .desc.criteria { color: var(--text-dim); }
  .card.locked .desc.criteria::before { content: "🔒 "; opacity: 0.7; }
  @container (min-width: 480px) {
    .grid { grid-template-columns: repeat(3, 1fr); }
  }
</style>
