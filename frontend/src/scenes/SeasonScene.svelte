<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { SeasonResponse, SeasonTierView } from "../net/metaApi";

  let data = $state<SeasonResponse | null>(null);
  let busy = $state(false);

  async function load() { data = await metaApi.getSeason(); }
  load();

  let countdown = $derived.by(() => {
    if (!data) return "";
    const ms = new Date(data.seasonEndsAt).getTime() - Date.now();
    if (ms <= 0) return "soon";
    const d = Math.floor(ms / 8.64e7), h = Math.floor((ms % 8.64e7) / 3.6e6);
    return `${d}d ${h}h`;
  });

  function reward(t: SeasonTierView): string {
    const parts: string[] = [];
    if (t.rewardGems) parts.push(`${t.rewardGems}◆`);
    if (t.rewardCardDust) parts.push(`${t.rewardCardDust}✦`);
    if (t.rewardModuleCores) parts.push(`${t.rewardModuleCores}✦`);
    return parts.join("  ");
  }
  const EFFECT: Record<string, string> = {
    ball_damage: "+Ball Damage", max_mana: "+Max Mana", crystal_bonus: "+Crystals", start_life: "+Starting HP",
  };

  async function claimTier(tier: number) { if (busy) return; busy = true; try { await metaApi.claimSeasonTier(tier); await load(); } finally { busy = false; } }
  async function claimEvent() { if (busy) return; busy = true; try { await metaApi.claimEvent(); await load(); } finally { busy = false; } }
</script>

<div class="se-root">
  <div class="ui-topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">Season</h1>
    <span class="ends">Ends in {countdown}</span>
  </div>

  {#if data}
    <div class="banner">
      <div class="theme">{data.theme}</div>
      <div class="sub">★ {data.tokens} Season Tokens · Rank #{data.seasonRank}</div>
    </div>

    {#if data.ev}
      <div class="event">
        <div class="ev-head">
          <span class="ev-name">⚔ {data.ev.name}</span>
          <span class="ev-eff">{EFFECT[data.ev.effect] ?? data.ev.effect} this week</span>
        </div>
        <div class="ev-bar"><div class="ev-fill" style="width:{Math.min(100, Math.round(data.ev.tokens / Math.max(1, data.ev.milestoneTokens) * 100))}%"></div></div>
        <div class="ev-foot">
          <span>{data.ev.tokens}/{data.ev.milestoneTokens} event tokens</span>
          {#if data.ev.claimed}
            <span class="done">Claimed ✓</span>
          {:else if data.ev.claimable}
            <button class="claim" onclick={claimEvent} disabled={busy}>Claim {data.ev.rewardModuleCores ? data.ev.rewardModuleCores + '✦' : data.ev.rewardGems + '◆'}</button>
          {:else}
            <span class="locked">Play battles to progress</span>
          {/if}
        </div>
      </div>
    {/if}

    <h3 class="track-title">Reward Track</h3>
    <div class="track">
      {#each data.track as t}
        <div class="tier {t.claimed ? 'claimed' : t.claimable ? 'claimable' : 'locked'}">
          <div class="t-num">Tier {t.tier}</div>
          <div class="t-req">{t.tokens} tokens</div>
          <div class="t-reward">{reward(t)}</div>
          {#if t.claimed}
            <span class="t-state done">Claimed ✓</span>
          {:else if t.claimable}
            <button class="t-claim" data-tier={t.tier} onclick={() => claimTier(t.tier)} disabled={busy}>Claim</button>
          {:else}
            <span class="t-state lock">🔒</span>
          {/if}
        </div>
      {/each}
    </div>
    <p class="hint">Every battle earns Season + Event tokens. The event modifier is live in all battles this week.</p>
  {:else}
    <div class="loading">Loading…</div>
  {/if}
</div>

<style>
  .se-root { min-height: 100cqh; display: flex; flex-direction: column;
    background: radial-gradient(ellipse at 50% 0%, rgba(120,80,20,.30), transparent 60%), linear-gradient(180deg,#140d06,#0a0a12 55%,#050406);
    color: #efe6d6; font-family: var(--font-body); padding-bottom: 28px; }
  .ends { margin-left: auto; color: var(--text-dim,#a99fce); font-size: 13px; }
  .banner { text-align: center; padding: 14px; }
  .theme { font-size: 24px; font-weight: 800; background: linear-gradient(90deg,#ffd56a,#ffce5a,#6cc0ff); -webkit-background-clip: text; background-clip: text; color: transparent; }
  .sub { color: var(--gold-bright,#ffd970); font-size: 13px; margin-top: 2px; }
  .event { margin: 4px 16px 10px; padding: 12px 14px; border-radius: 12px;
    background: linear-gradient(180deg, rgba(30,55,100,.55), rgba(18,30,60,.7)); border: 1px solid #4f8fd0; }
  .ev-head { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 4px; }
  .ev-name { font-weight: 800; color: #ffd970; }
  .ev-eff { font-size: 12px; color: #bfe0ff; }
  .ev-bar { height: 9px; background: rgba(255,255,255,.1); border-radius: 5px; overflow: hidden; margin: 8px 0; }
  .ev-fill { height: 100%; background: linear-gradient(90deg,#6cc0ff,#2a78c0); transition: width .4s; }
  .ev-foot { display: flex; justify-content: space-between; align-items: center; font-size: 12px; color: var(--text-dim,#a99fce); }
  .track-title { margin: 6px 16px; color: var(--gold,#ffce5a); font-size: 16px; }
  .track { display: flex; flex-direction: column; gap: 8px; padding: 0 16px; }
  .tier { display: grid; grid-template-columns: 70px 1fr auto; align-items: center; gap: 10px;
    padding: 10px 14px; border-radius: 10px; background: rgba(255,255,255,.03); border: 1px solid rgba(180,140,60,.3); }
  .tier.claimable { border-color: var(--gold-bright,#ffd970); box-shadow: 0 0 10px rgba(255,200,60,.3); }
  .tier.claimed { opacity: .6; }
  .t-num { font-weight: 700; }
  .t-req { color: var(--text-dim,#a99fce); font-size: 12px; }
  .t-reward { color: var(--gold-bright,#ffd970); font-size: 13px; font-weight: 700; grid-column: 2; justify-self: end; }
  .t-claim, .claim { font-family: var(--font-body); font-weight: 800; font-size: 12px; border-radius: 8px; cursor: pointer;
    border: 1px solid #ffd970; background: linear-gradient(180deg, #ffe06a, #d89a2e); color: #2a1c08; padding: 6px 12px; min-height: 36px; }
  .t-claim:hover:not(:disabled), .claim:hover:not(:disabled) { filter: brightness(1.25); }
  .t-claim:disabled, .claim:disabled { opacity: .5; }
  .t-state { font-size: 12px; } .done { color: var(--ok-bright,#7fe3a0); font-weight: 700; }
  .lock, .locked { color: var(--faint,#6f6690); }
  .hint { text-align: center; color: var(--faint,#6f6690); font-size: 12px; padding: 8px 18px; }
  .loading { padding: 40px; text-align: center; color: var(--text-dim,#a99fce); }
</style>
