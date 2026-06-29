<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import type { Snapshot } from "../net/Connection";
  import type { SpellDef } from "../net/metaApi";
  import { WasmConnection as Connection } from "../net/WasmConnection";
  import { Renderer } from "../render/Renderer";
  import { attachPaddleInput } from "../input/PaddleInput";
  import { installTestHooks } from "../testhooks";
  import { installBridge, teardownBridge } from "../testBridge";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { createCampaignFlow } from "./battle/campaignFlow";
  import { createDungeonFlow } from "./battle/dungeonFlow";
  import { createTrialFlow } from "./battle/trialFlow";
  import { maybeShowTutorial } from "./TutorialOverlay";
  import Hud from "../ui/Hud.svelte";

  const _q    = new URLSearchParams(location.search.startsWith("?") ? location.search.slice(1) : location.search);
  const level = _q.get("level") ?? "hell-1";
  const seed  = Number(_q.get("seed") ?? "1");
  const run   = _q.get("run") ?? `dev-${Date.now()}`;
  const from  = _q.get("from") ?? "";
  const mode  = _q.get("mode") ?? ""; // "trial" → server scores it to the leaderboard

  let conn          = $state<Connection | null>(null);
  let snap          = $state<Snapshot | null>(null);
  let spells        = $state<SpellDef[]>([]);

  // The hotbar is driven by the equipped loadout from the snapshot (signature + drafted picks),
  // so it matches exactly what CastSlot indexes. We fall back to the full /characters kit only
  // until the first snapshot with a loadout arrives (older server, or pre-serve).
  let fallbackSpells: SpellDef[] = [];
  let loadoutKey = "";

  let container: HTMLElement;

  onMount(() => {
    const r = new Renderer(container);
    const c = new Connection(level, seed, run, mode);
    conn = c;

    const flow =
      from === "campaign" ? createCampaignFlow(level) :
      from === "dungeon"  ? createDungeonFlow(c)       :
      from === "trial"    ? createTrialFlow()          :
      null;

    c.onSnapshot = (s) => {
      r.draw(s);
      snap = s;
      // Rebuild the hotbar only when the loadout's id-list actually changes (it's stable across
      // most ticks, but can grow mid-run via in-run drafting), to avoid re-rendering every frame.
      const lo = s.loadout;
      if (lo && lo.length) {
        const key = lo.map(x => x.id).join(",");
        if (key !== loadoutKey) {
          loadoutKey = key;
          spells = lo.map(x => ({ id: x.id, name: x.name, icon: x.icon, manaCost: x.manaCost }));
        }
      } else if (spells.length === 0 && fallbackSpells.length) {
        spells = fallbackSpells;
      }
      if (flow) flow.handlePhase(s);
    };

    metaApi.getCharacters()
      .then((data) => {
        const selected = data.characters.find(ch => ch.id === data.selected);
        if (selected) {
          r.setClass(selected.id);
          // Display fallback only; the snapshot loadout is authoritative once it arrives.
          if (selected.spells?.length > 0) {
            fallbackSpells = selected.spells;
            if (spells.length === 0 && loadoutKey === "") spells = selected.spells;
          }
        }
      })
      .catch(() => {});

    const detachInput = attachPaddleInput(r.app.view as HTMLCanvasElement, c, () => c.latest);
    installBridge({ renderer: r, conn: c, detachInput });
    installTestHooks(c);

    const forceTutorial = _q.get("tutorial") === "1";
    const tutorialSeen = typeof localStorage !== "undefined" && localStorage.getItem("arkanoid_tutorial_seen") === "1";
    const isAutomated = (!!(navigator as any).webdriver || tutorialSeen) && !forceTutorial;

    c.whenReady(() => {
      if (isAutomated) {
        setTimeout(() => c.serve(), 300);
      } else {
        maybeShowTutorial(container, forceTutorial).then(() => {
          setTimeout(() => c.serve(), 300);
        });
      }
    });
  });

  onDestroy(() => { teardownBridge(); });
</script>

<div style="position:relative;width:100%;height:100%;overflow:hidden" bind:this={container}>
  {#if conn}
    <Hud {snap} {spells} {conn} {level} />
  {/if}
</div>
