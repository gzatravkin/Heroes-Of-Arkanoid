<script lang="ts">
  import type { DungeonRunState } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";
  import { buffName, buffIcon } from "./battle/overlays";

  function levelLabel(id: string): string {
    return id.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
  }

  type ActiveState = Required<Pick<DungeonRunState, "floors" | "floorIndex" | "relics" | "ballCores" | "dungeonId">>
    & Pick<DungeonRunState, "paddleMods" | "draftedSpells">;

  let loading  = $state(true);
  let error    = $state("");
  let cleared  = $state(false);
  let inactive = $state(false);
  let active   = $state<ActiveState | null>(null);

  async function load() {
    try {
      const state = await metaApi.getDungeonState();
      if (state.cleared) {
        cleared = true;
      } else if (state.active && state.floors) {
        active = state as ActiveState;
      } else {
        inactive = true;
      }
    } catch {
      error = "Failed to load dungeon state.";
    }
    loading = false;
  }

  load().catch(console.error);

  // Every run acquisition the player should see: relics, ball-cores, paddle-mods, and drafted
  // spells (the latter tagged "spell:" so buffName/buffIcon render them correctly). Previously
  // paddle-mods and drafted spells were silently omitted from the run summary.
  let allBuffs = $derived(active
    ? [
        ...active.relics,
        ...active.ballCores,
        ...(active.paddleMods ?? []),
        ...(active.draftedSpells ?? []).map(s => `spell:${s}`),
      ]
    : []);
  let currentFloor = $derived(active ? active.floors[active.floorIndex] : "");
</script>

<div id="dungeon" class="root">
  <div class="topbar">
    <a href="/?scene=campaign" class="ui-back" aria-label="Back to campaign"></a>
    <h1 id="dngrun-title" class="ui-title">
      {cleared ? "Dungeon Cleared!" : active ? "Active Run" : "Dungeon"}
    </h1>
    <div class="ui-topbar-spacer"></div>
  </div>

  {#if error}
    <div class="msg-error">{error}</div>
  {:else if loading}
    <!-- waiting -->
  {:else if cleared}
    <div class="msg-cleared">Dungeon Cleared!</div>
  {:else if inactive}
    <div class="msg-inactive">No active run.</div>
  {:else if active}
    <div class="run-body">
    <div id="dungeon-floor-progress" class="progress">
      Floor {active.floorIndex + 1} / {active.floors.length}
    </div>
    <div class="floor-name">{levelLabel(currentFloor)}</div>
    <div class="buffs-label">Collected Buffs</div>
    <div id="dungeon-buffs" class="buffs">
      {#if allBuffs.length === 0}
        <span class="empty">None yet</span>
      {:else}
        {#each allBuffs as buffId}
          <div class="buff-chip">
            <img src={buffIcon(buffId)} style="width:20px;height:20px;image-rendering:pixelated" alt="" />
            <span>{buffName(buffId)}</span>
          </div>
        {/each}
      {/if}
    </div>
    <button id="btn-enter-floor" class="enter-btn"
            onclick={() => navigateTo(`/?scene=battle&level=${encodeURIComponent(currentFloor)}&from=dungeon`)}>
      Enter Floor
    </button>
    </div>
  {/if}
</div>

<style>
  .root {
    position: relative; min-height: 100cqh; width: 100%;
    color: var(--text); font-family: var(--font-body);
    display: flex; flex-direction: column; align-items: center;
    box-sizing: border-box;
    padding: max(12px, env(safe-area-inset-top,0px)) 12px
             max(12px, env(safe-area-inset-bottom,0px)) 12px;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
  }
  .topbar {
    display: flex; align-items: center; gap: var(--sp-2);
    align-self: stretch;
    margin: calc(-1 * max(12px, env(safe-area-inset-top,0px))) -12px var(--sp-2);
    padding: max(12px, env(safe-area-inset-top,0px)) var(--sp-3) var(--sp-2) var(--sp-3);
    background: linear-gradient(180deg, rgba(46,34,16,0.96), rgba(24,18,10,0.92));
    border-bottom: 2px solid rgba(180,140,60,0.45);
  }
  /* Centre the active-run content in the remaining height (was top-aligned with a huge empty gap). */
  .run-body { flex: 1; align-self: stretch; display: flex; flex-direction: column;
    align-items: center; justify-content: center; gap: var(--sp-1); }
  .msg-error   { color: var(--danger-light); margin-top: var(--sp-7); }
  .msg-cleared { margin-top: var(--sp-7); font-size: var(--fs-title); font-weight: 700; color: var(--ok-bright); text-shadow: 0 0 16px rgba(50,220,100,0.5); }
  .msg-inactive { margin-top: var(--sp-7); font-size: var(--fs-large); color: var(--color-dungeon-label); }
  .progress  { font-size: var(--fs-body); color: var(--text-dim); margin-bottom: var(--sp-1h); letter-spacing: 0.03em; }
  .floor-name { font-size: var(--fs-section); font-weight: 700; color: var(--gold-bright); margin-bottom: var(--sp-4); text-align: center; text-shadow: 0 1px 2px rgba(0,0,0,0.9); }
  .buffs-label { font-size: var(--fs-body); color: var(--color-dungeon-label); margin-bottom: var(--sp-2); align-self: center; }
  .buffs { display: flex; gap: var(--sp-2); flex-wrap: wrap; margin-bottom: var(--sp-4); min-height: 36px; max-width: 480px; justify-content: center; }
  .empty { color: var(--color-empty); font-size: var(--fs-body); }
  .buff-chip {
    background: none; border-style: solid; border-width: 8px 10px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    display: flex; align-items: center; justify-content: center;
    gap: 5px; padding: var(--sp-1) var(--sp-2);
    font-size: var(--fs-caption); color: var(--text-dim);
    transition: filter var(--dur-normal);
  }
  .buff-chip:hover { filter: brightness(1.08); }
  .enter-btn {
    min-height: 44px; padding: 2px 14px;
    background: none; border-style: solid; border-width: 8px 18px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    cursor: pointer; font-family: var(--font-body); font-size: var(--fs-caption);
    font-weight: 700; letter-spacing: 0.04em; touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); color: var(--gold-bright);
  }
  .enter-btn:hover:not(:disabled)  { filter: brightness(1.18); }
  .enter-btn:active:not(:disabled) { transform: scale(0.96); }
  .enter-btn:disabled { filter: var(--filter-disabled); cursor: default; }
  .enter-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
</style>
