<script lang="ts">
  import type { RollState } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";

  // The single, shared player-currency display. Drop it in any meta screen's header.
  // It self-refreshes whenever a buy/roll dispatches the global "currency-changed" event.
  let s = $state<RollState | null>(null);

  async function load() { try { s = await metaApi.rollState(); } catch { /* keep last values */ } }
  load();

  $effect(() => {
    const on = () => load();
    window.addEventListener("currency-changed", on);
    return () => window.removeEventListener("currency-changed", on);
  });
</script>

<div class="ui-coins" data-currency-bar>
  <span class="ui-coin ui-coin-sparks"  title="Sparks — buy items & modules">{s?.sparks ?? 0}</span>
  <span class="ui-coin ui-coin-souls"   title="Souls — buy spells & heroes">{s?.souls ?? 0}</span>
  <span class="ui-coin ui-coin-insight" title="Insight — level masteries">{s?.insight ?? 0}</span>
</div>
