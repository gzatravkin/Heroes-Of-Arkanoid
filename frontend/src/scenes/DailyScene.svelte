<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { DailyResponse } from "../net/metaApi";

  let data = $state<DailyResponse | null>(null);
  let busy = $state(false);
  let flash = $state("");

  async function load() { data = await metaApi.getDaily(); }
  load();

  let countdown = $derived.by(() => {
    if (!data) return "";
    const ms = new Date(data.dayEndsAt).getTime() - Date.now();
    if (ms <= 0) return "soon";
    const h = Math.floor(ms / 3.6e6), m = Math.floor((ms % 3.6e6) / 6e4);
    return `${h}h ${m}m`;
  });

  function pct(m: { progress: number; target: number }) {
    return Math.min(100, Math.round((m.progress / Math.max(1, m.target)) * 100));
  }
  const METRIC: Record<string, string> = {
    blocks_destroyed: "Blocks", levels_won: "Wins", battles_played: "Battles",
  };

  async function claim(id: string) {
    if (busy) return;
    busy = true;
    try {
      const r = await metaApi.claimDaily(id);
      if (r.ok) {
        flash = `+${r.gems} ◆ Souls  +${r.cardDust} ✦ Sparks` + (r.streakBonus ? "  ★ STREAK CHEST!" : "");
        await load();
        setTimeout(() => (flash = ""), 2500);
      }
    } finally { busy = false; }
  }
</script>

<div class="daily-root">
  <div class="ui-topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">Daily Missions</h1>
    <span class="reset">Resets in {countdown}</span>
  </div>

  {#if data}
    <div class="streak">
      <span class="streak-label">Streak</span>
      <div class="pips">
        {#each Array(data.streakTarget) as _, i}
          <span class="pip" class:on={i < data.streak}></span>
        {/each}
      </div>
      <span class="streak-count">{data.streak}/{data.streakTarget}</span>
    </div>

    {#if flash}<div class="flash">{flash}</div>{/if}

    <div class="missions">
      {#each data.missions as m}
        <div class="mission" class:done={m.claimed}>
          <div class="m-head">
            <span class="m-name">{m.name}</span>
            <span class="m-reward">+{m.rewardGems}◆ +{m.rewardCardDust}✦</span>
          </div>
          <div class="bar"><div class="fill" style="width:{pct(m)}%"></div></div>
          <div class="m-foot">
            <span class="m-prog">{m.progress}/{m.target} {METRIC[m.metric] ?? m.metric}</span>
            {#if m.claimed}
              <span class="claimed">Claimed ✓</span>
            {:else if m.complete}
              <button class="claim" data-mission={m.id} onclick={() => claim(m.id)} disabled={busy}>Claim</button>
            {:else}
              <span class="locked">In progress</span>
            {/if}
          </div>
        </div>
      {/each}
    </div>
    <p class="hint">Play battles to advance missions. Complete all 3 daily to build your streak.</p>
  {:else}
    <div class="loading">Loading…</div>
  {/if}
</div>

<style>
  .daily-root { min-height: 100cqh; display: flex; flex-direction: column;
    background: radial-gradient(ellipse at 50% 0%, rgba(120,80,20,.28), transparent 60%), linear-gradient(180deg,#140d06,#0a0a12 55%,#050406);
    color: #efe6d6; font-family: var(--font-body); }
  .reset { margin-left: auto; color: var(--text-dim,#a99fce); font-size: 13px; }
  .streak { display: flex; align-items: center; gap: 10px; padding: 14px 18px; }
  .streak-label { font-weight: 700; color: var(--gold,#ffce5a); }
  .pips { display: flex; gap: 5px; flex: 1; }
  .pip { flex: 1; height: 10px; border-radius: 5px; background: rgba(255,255,255,.1); border: 1px solid rgba(180,140,60,.3); }
  .pip.on { background: linear-gradient(180deg,#ffe06a,#d89a2e); box-shadow: 0 0 6px rgba(255,200,60,.5); }
  .streak-count { color: var(--text-dim,#a99fce); font-size: 13px; }
  .flash { margin: 0 18px 8px; padding: 8px 14px; border-radius: 8px; text-align: center; font-weight: 700;
    color: #cfffe0; background: rgba(40,90,50,.6); border: 1px solid #7fe3a0; }
  .missions { display: flex; flex-direction: column; gap: 12px; padding: 8px 18px; }
  .mission { background: linear-gradient(180deg, rgba(24,28,40,.95), rgba(16,18,30,.95));
    border: 1px solid rgba(120,100,180,.35); border-radius: 12px; padding: 14px 16px; }
  .mission.done { opacity: .65; }
  .m-head { display: flex; justify-content: space-between; align-items: baseline; }
  .m-name { font-weight: 700; font-size: 15px; }
  .m-reward { font-size: 12px; color: var(--gold-bright,#ffd970); }
  .bar { height: 10px; background: rgba(255,255,255,.08); border-radius: 6px; overflow: hidden; margin: 8px 0; }
  .fill { height: 100%; background: linear-gradient(90deg,#5fd0ff,#3a8fd8); transition: width .4s; }
  .m-foot { display: flex; justify-content: space-between; align-items: center; }
  .m-prog { font-size: 12.5px; color: var(--text-dim,#a99fce); }
  .claim { font-family: var(--font-body); font-weight: 700; font-size: 13px; border-radius: 8px; cursor: pointer;
    border: 1px solid #7fe3a0; background: rgba(40,90,50,.7); color: #cfffe0; padding: 7px 16px; min-height: 38px; }
  .claim:hover:not(:disabled) { filter: brightness(1.25); }
  .claim:disabled { opacity: .5; }
  .claimed { color: var(--ok-bright,#7fe3a0); font-size: 13px; font-weight: 700; }
  .locked { color: var(--faint,#6f6690); font-size: 12.5px; font-style: italic; }
  .hint { text-align: center; color: var(--faint,#6f6690); font-size: 12px; padding: 6px 18px 24px; }
  .loading { padding: 40px; text-align: center; color: var(--text-dim,#a99fce); }
</style>
