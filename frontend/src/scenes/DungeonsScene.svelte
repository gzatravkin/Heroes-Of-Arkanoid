<script lang="ts">
  import type { DungeonDef } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";
  import { buffIcon } from "./battle/overlays";
  import { getRelicName } from "../net/relicCache";

  let dungeons = $state<DungeonDef[]>([]);
  let error    = $state(false);
  let pending  = $state<Record<string, boolean>>({});

  async function load() {
    try {
      const data = await metaApi.getDungeons();
      dungeons = data.dungeons ?? [];
    } catch {
      error = true;
    }
  }

  async function descend(d: DungeonDef) {
    pending[d.id] = true;
    try {
      await metaApi.startDungeon(d.id);
      navigateTo("/?scene=dungeon");
    } catch {
      pending[d.id] = false;
    }
  }

  load().catch(console.error);
</script>

<div id="dungeons-scene" class="root">
  <div class="topbar">
    <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
    <h1 class="ui-title">Dungeons</h1>
    <div class="ui-topbar-spacer"></div>
  </div>
  <div class="flavor">A rift has opened — descend? Death here is permanent.</div>
  <div id="dungeon-list" class="list">
    {#if error}
      <div class="err">Failed to load dungeons.</div>
    {:else}
      {#each dungeons as d}
        <div data-dungeon={d.id} class="card">
          <div class="card-titlebar"><span class="card-name">{d.name}</span></div>
          <div class="card-meta">
            <span>{d.floors.length} floors</span>
            <div class="reward-row">
              <img src={buffIcon(d.rewardRelic)} style="width:20px;height:20px;image-rendering:pixelated" alt=""
                   onerror={(e) => ((e.currentTarget as HTMLImageElement).style.display = 'none')} />
              <span>{getRelicName(d.rewardRelic)} + {d.rewardCrystals} Souls</span>
            </div>
          </div>
          <button class="descend-btn" disabled={!!pending[d.id]} onclick={() => descend(d)}>
            {pending[d.id] ? "Starting…" : "Descend"}
          </button>
        </div>
      {/each}
    {/if}
  </div>
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
  .flavor {
    background: none; border-style: solid; border-width: 12px 14px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-3) var(--sp-4);
    margin: var(--sp-2) auto var(--sp-4); max-width: 520px;
    text-align: center; color: var(--text); font-size: var(--fs-body);
    letter-spacing: 0.03em; line-height: 1.5;
    transition: filter var(--dur-normal);
  }
  .flavor:hover { filter: brightness(1.08); }
  .list {
    display: flex; flex-direction: column; gap: var(--sp-3);
    width: 100%; max-width: 520px; flex: 1; justify-content: center;
  }
  .err { color: var(--danger-light); }
  .card {
    background: none; border-style: solid; border-width: 12px 14px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
    padding: var(--sp-3) var(--sp-3h);
    display: flex; flex-direction: column; gap: var(--sp-2);
    transition: filter var(--dur-normal);
  }
  .card:hover { filter: brightness(1.08); }
  .card-titlebar { display: flex; align-items: center; }
  .card-name {
    font-size: var(--fs-body); font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
  }
  .card-meta {
    display: flex; flex-direction: column; gap: var(--sp-1h);
    align-items: flex-start; font-size: var(--fs-caption); color: var(--text-dim);
  }
  .reward-row { display: flex; align-items: center; gap: var(--sp-1h); }
  .descend-btn {
    min-height: 44px; padding: 2px 14px;
    background: none; border-style: solid; border-width: 8px 18px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    cursor: pointer; font-family: var(--font-body); font-size: var(--fs-caption);
    font-weight: 700; letter-spacing: 0.04em; touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); color: var(--gold-bright);
  }
  .descend-btn:hover:not(:disabled)  { filter: brightness(1.18); }
  .descend-btn:active:not(:disabled) { transform: scale(0.96); }
  .descend-btn:disabled { filter: saturate(0.25) brightness(0.6); cursor: default; }
  .descend-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
</style>
