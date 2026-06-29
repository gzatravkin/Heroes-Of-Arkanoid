<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { LeagueResponse } from "../net/metaApi";
  import { navigateTo } from "../ui/transition";

  let data = $state<LeagueResponse | null>(null);

  async function load() { data = await metaApi.getLeague("trial"); }
  load();

  let countdown = $derived.by(() => {
    if (!data) return "";
    const ms = new Date(data.weekEndsAt).getTime() - Date.now();
    if (ms <= 0) return "soon";
    const d = Math.floor(ms / 8.64e7), h = Math.floor((ms % 8.64e7) / 3.6e6);
    return `${d}d ${h}h`;
  });

  function zone(rank: number): string {
    if (!data) return "";
    if (rank <= data.promoteTop) return "promote";
    if (rank > data.cohortSize - data.demoteBottom) return "demote";
    return "";
  }
  function playTrial() { navigateTo(`/?scene=battle&from=trial&mode=trial`); }
</script>

<div class="lg-root">
  <div class="ui-topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">League</h1>
    <span class="reset">Week ends in {countdown}</span>
  </div>

  {#if data}
    <div class="banner">
      <span class="tier">{data.tierName} League</span>
      <span class="myrank">Your rank: <b>#{data.myRank}</b> · {data.myScore} pts</span>
    </div>

    <div class="legend">
      <span class="dot promote"></span> Top {data.promoteTop} promote
      <span class="dot demote"></span> Bottom {data.demoteBottom} demote
    </div>

    <div class="ladder">
      {#each data.entries as e}
        <div class="row {zone(e.rank)} {e.isMe ? 'me' : ''}">
          <span class="rank">#{e.rank}</span>
          <span class="name">{e.isMe ? "You" : e.displayName}{e.isBot ? "" : (e.isMe ? "" : " ")}</span>
          <span class="score">{e.score}</span>
        </div>
      {/each}
    </div>

    <button id="btn-play-trial" class="play" onclick={playTrial}>⚔ Play Weekly Trial</button>
    <p class="hint">One shared seed per week — everyone faces the same gauntlet. Higher scores climb the ladder.</p>
  {:else}
    <div class="loading">Loading…</div>
  {/if}
</div>

<style>
  .lg-root { min-height: 100cqh; display: flex; flex-direction: column;
    background: radial-gradient(ellipse at 50% 0%, rgba(120,90,40,.25), transparent 60%), linear-gradient(180deg,#0e0a12,#060410);
    color: #ece6ff; font-family: var(--font-body); }
  .topbar { display: flex; align-items: center; gap: 14px; padding: 12px 16px; flex-wrap: wrap;
    background: linear-gradient(180deg, rgba(46,34,16,0.96), rgba(24,18,10,0.92)); border-bottom: 2px solid rgba(180,140,60,.4); }
  .back { color: var(--gold-bright,#ffd970); text-decoration: none; font-weight: 700; min-height: 44px; display: flex; align-items: center; }
  .reset { margin-left: auto; color: var(--text-dim,#a99fce); font-size: 13px; }
  .banner { display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 12px; }
  .tier { font-size: 20px; font-weight: 800; color: var(--gold,#ffce5a); text-shadow: 0 0 12px rgba(255,200,50,.5); letter-spacing: .04em; }
  .myrank { color: var(--text-dim,#a99fce); font-size: 13px; }
  .legend { display: flex; gap: 14px; justify-content: center; font-size: 11.5px; color: var(--text-dim,#a99fce); margin-bottom: 6px; }
  .dot { display: inline-block; width: 10px; height: 10px; border-radius: 3px; vertical-align: middle; margin-right: 3px; }
  .dot.promote { background: #7fe3a0; } .dot.demote { background: #ff6b7a; }
  .ladder { flex: 1; overflow-y: auto; padding: 0 14px; display: flex; flex-direction: column; gap: 2px; }
  .row { display: flex; align-items: center; gap: 10px; padding: 7px 12px; border-radius: 7px;
    background: rgba(255,255,255,.03); border: 1px solid transparent; font-size: 14px; }
  .row.promote { border-left: 3px solid #7fe3a0; }
  .row.demote  { border-left: 3px solid #ff6b7a; }
  .row.me { background: rgba(80,60,160,.4); border-color: var(--gold-bright,#ffd970); font-weight: 700; }
  .rank { width: 42px; color: var(--text-dim,#a99fce); }
  .name { flex: 1; }
  .score { font-variant-numeric: tabular-nums; color: var(--gold-bright,#ffd970); }
  .play { margin: 14px auto; padding: 0 22px; height: 54px; min-width: min(280px,80cqw);
    background: none; border-style: solid; border-width: 8px 30px;
    border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch; cursor: pointer;
    font-family: var(--font-body); font-weight: 700; font-size: 17px; color: var(--text,#ece6ff); }
  .play:hover { filter: brightness(1.15); }
  .hint { text-align: center; color: var(--faint,#6f6690); font-size: 12px; padding: 0 18px 24px; }
  .loading { padding: 40px; text-align: center; color: var(--text-dim,#a99fce); }
</style>
