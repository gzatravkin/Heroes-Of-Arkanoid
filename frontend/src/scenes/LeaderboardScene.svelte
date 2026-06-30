<script lang="ts">
  import { getBoard, type BoardRow, type BoardWindow, type BoardType } from "../net/FirestoreLeaderboard";
  import { isFirebaseConfigured } from "../net/firebase";
  import { currentUid } from "../net/FirebaseAuth";
  import AuthBadge from "../ui/AuthBadge.svelte";

  type MainTab = "souls" | "progression";
  let mainTab    = $state<MainTab>("souls");
  let soulsWin   = $state<BoardWindow>("week");
  let rows       = $state<BoardRow[]>([]);
  let playerRank = $state<number | null>(null);
  let loading    = $state(true);
  let error      = $state(false);
  let myUid      = $state(currentUid());

  async function load() {
    loading = true; error = false;
    try {
      const result = await getBoard(mainTab, soulsWin);
      rows       = result.rows;
      playerRank = result.playerRank;
      myUid      = currentUid();
    } catch { error = true; }
    finally   { loading = false; }
  }

  $effect(() => {
    // re-runs whenever mainTab or soulsWin change
    void mainTab; void soulsWin;
    load();
  });

  function rankLabel(r: number): string {
    if (r === 1) return "🥇";
    if (r === 2) return "🥈";
    if (r === 3) return "🥉";
    return `#${r}`;
  }

  function scoreLabel(row: BoardRow): string {
    if (mainTab === "souls") return `${row.score.toLocaleString()} ◆`;
    let s = `Lv ${row.score}`;
    if (row.maxSpellLevel)  s += ` · ✦${row.maxSpellLevel}`;
    if ((row.maxHeroStars ?? 0) > 0) s += ` · ${"★".repeat(Math.min(row.maxHeroStars!, 6))}`;
    return s;
  }
</script>

<div class="lb-root">

  <!-- Header -->
  <div class="lb-header">
    <a href="/?scene=campaign" class="lb-back" aria-label="Back to campaign">← Campaign</a>
    <span class="lb-title">Leaderboard</span>
    <div class="lb-auth-wrap"><AuthBadge /></div>
  </div>

  <!-- Main tabs -->
  <div class="lb-tabs" role="tablist">
    <button class="lb-tab {mainTab === 'souls'       ? 'lb-tab-active' : ''}"
      role="tab" aria-selected={mainTab === "souls"}
      onclick={() => mainTab = "souls"}>Active</button>
    <button class="lb-tab {mainTab === 'progression' ? 'lb-tab-active' : ''}"
      role="tab" aria-selected={mainTab === "progression"}
      onclick={() => mainTab = "progression"}>Progress</button>
  </div>

  <!-- Time-window sub-tabs for souls board -->
  {#if mainTab === "souls"}
  <div class="lb-subtabs">
    {#each ["day", "week", "month"] as win}
      <button class="lb-stab {soulsWin === win ? 'lb-stab-active' : ''}"
        onclick={() => soulsWin = win as BoardWindow}
        aria-pressed={soulsWin === win}>
        {win === "day" ? "Today" : win === "week" ? "This Week" : "This Month"}
      </button>
    {/each}
  </div>
  {/if}

  <!-- Body -->
  {#if !isFirebaseConfigured()}
    <div class="lb-empty">
      <div class="lb-empty-title">Leaderboards offline</div>
      <div class="lb-empty-sub">Add Firebase config to .env to enable social features.</div>
    </div>

  {:else if loading}
    <div class="lb-empty"><div class="lb-spinner" aria-label="Loading…"></div></div>

  {:else if error}
    <div class="lb-empty">
      <div class="lb-empty-title">Couldn't load</div>
      <button class="lb-retry" onclick={load}>Retry</button>
    </div>

  {:else if rows.length === 0}
    <div class="lb-empty">
      <div class="lb-empty-title">No scores yet</div>
      <div class="lb-empty-sub">Win levels to appear here!</div>
    </div>

  {:else}
    <ul class="lb-list" role="list">
      {#each rows as row (row.uid)}
        <li class="lb-row
          {row.rank === 1 ? 'lb-gold' : row.rank === 2 ? 'lb-silver' : row.rank === 3 ? 'lb-bronze' : ''}
          {row.uid === myUid ? 'lb-mine' : ''}"
          role="listitem">
          <span class="lb-rank">{rankLabel(row.rank)}</span>
          <span class="lb-nick">{row.nickname}</span>
          <span class="lb-score">{scoreLabel(row)}</span>
        </li>
      {/each}
    </ul>

    {#if playerRank !== null}
      <div class="lb-my-rank">
        Your rank: {playerRank === -1 ? "outside top 100" : `#${playerRank}`}
      </div>
    {/if}
  {/if}

</div>

<style>
  .lb-root {
    height: 100cqh; display: flex; flex-direction: column; overflow: hidden;
    background: linear-gradient(180deg, #12080a 0%, #070510 55%, #040308 100%);
    font-family: var(--font-body); color: #e8e8ff;
  }

  /* Header */
  .lb-header {
    display: flex; align-items: center; gap: var(--sp-3);
    padding: var(--sp-2) var(--sp-4); flex-shrink: 0;
    background: linear-gradient(180deg, rgba(46,34,16,0.96), rgba(24,18,10,0.92));
    border-bottom: 2px solid rgba(180,140,60,0.4);
    min-height: 52px;
  }
  .lb-back {
    flex-shrink: 0; min-width: 44px; min-height: 44px;
    display: flex; align-items: center; padding: 0 12px;
    text-decoration: none; color: var(--gold-bright); font-weight: 700;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .lb-back:hover { filter: brightness(1.15); }
  .lb-back:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .lb-title {
    flex: 1; font-size: var(--fs-section); font-weight: 800; color: var(--gold);
    text-align: center; letter-spacing: 0.04em;
  }
  .lb-auth-wrap { position: relative; }

  /* Main tabs */
  .lb-tabs {
    display: flex; flex-shrink: 0;
    border-bottom: 1px solid rgba(180,140,60,0.2);
  }
  .lb-tab {
    flex: 1; padding: 12px 8px; background: none; border: none;
    border-bottom: 3px solid transparent;
    color: var(--text-dim); font-family: var(--font-body);
    font-size: var(--fs-body); font-weight: 700; cursor: pointer;
    transition: color var(--dur-normal), border-color var(--dur-normal), background var(--dur-normal);
  }
  .lb-tab:hover { color: var(--text); }
  .lb-tab-active { color: var(--gold-bright); border-bottom-color: var(--gold); background: rgba(180,140,60,0.07); }

  /* Sub-tabs */
  .lb-subtabs {
    display: flex; gap: var(--sp-2); padding: var(--sp-2) var(--sp-4); flex-shrink: 0;
    background: rgba(0,0,0,0.18);
  }
  .lb-stab {
    flex: 1; padding: 8px 4px; border-radius: 8px;
    background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.1);
    color: var(--text-dim); font-family: var(--font-body);
    font-size: var(--fs-small); font-weight: 700; cursor: pointer;
    transition: all var(--dur-normal);
  }
  .lb-stab:hover { border-color: rgba(180,140,60,0.35); color: var(--text); }
  .lb-stab-active {
    background: rgba(180,140,60,0.18); border-color: rgba(180,140,60,0.5);
    color: var(--gold-bright);
  }

  /* List */
  .lb-list {
    flex: 1; overflow-y: auto; padding: var(--sp-2) var(--sp-4);
    display: flex; flex-direction: column; gap: var(--sp-1h); list-style: none; margin: 0;
    scrollbar-width: thin; scrollbar-color: rgba(180,140,60,0.3) transparent;
  }
  .lb-row {
    display: flex; align-items: center; gap: var(--sp-2h); padding: 10px 14px;
    background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.08);
    border-radius: 10px; transition: border-color var(--dur-normal);
  }
  .lb-gold   { background: rgba(255,215,0,0.1);  border-color: rgba(255,215,0,0.38); }
  .lb-silver { background: rgba(192,192,192,0.09); border-color: rgba(192,192,192,0.32); }
  .lb-bronze { background: rgba(205,127,50,0.09); border-color: rgba(205,127,50,0.32); }
  .lb-mine   { border-color: rgba(100,180,255,0.5); background: rgba(100,180,255,0.07); }
  .lb-rank {
    font-size: var(--fs-body); font-weight: 900; min-width: 38px; text-align: center;
    color: var(--text-dim); flex-shrink: 0;
  }
  .lb-nick {
    flex: 1; font-weight: 700; font-size: var(--fs-body); color: var(--text);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }
  .lb-score { font-weight: 700; color: var(--gold-bright); font-size: var(--fs-small); white-space: nowrap; }

  /* Empty / loading states */
  .lb-empty {
    flex: 1; display: flex; flex-direction: column;
    align-items: center; justify-content: center; gap: var(--sp-3);
    padding: var(--sp-6);
  }
  .lb-empty-title { font-size: var(--fs-section); font-weight: 800; color: var(--text-dim); }
  .lb-empty-sub   { font-size: var(--fs-body); color: rgba(255,255,255,0.32); text-align: center; line-height: 1.5; }
  .lb-spinner {
    width: 38px; height: 38px;
    border: 3px solid rgba(180,140,60,0.18); border-top-color: var(--gold);
    border-radius: 50%; animation: lb-spin 0.75s linear infinite;
  }
  @keyframes lb-spin { to { transform: rotate(360deg); } }
  .lb-retry {
    padding: 10px 28px; background: rgba(180,140,60,0.18);
    border: 1px solid rgba(180,140,60,0.4); border-radius: 8px;
    color: var(--gold-bright); font-family: var(--font-body);
    font-weight: 700; cursor: pointer; transition: background var(--dur-normal);
  }
  .lb-retry:hover { background: rgba(180,140,60,0.32); }

  /* Player's pinned rank */
  .lb-my-rank {
    padding: var(--sp-2h) var(--sp-4); flex-shrink: 0;
    text-align: center; color: var(--text-dim);
    font-size: var(--fs-small); font-weight: 700;
    border-top: 1px solid rgba(255,255,255,0.07);
    background: rgba(0,0,0,0.2);
  }
</style>
