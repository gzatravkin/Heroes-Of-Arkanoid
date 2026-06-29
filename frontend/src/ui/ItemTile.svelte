<script lang="ts">
  import type { Snippet } from "svelte";
  // Compact collection tile: small icon + name, with Lv and ×copies badges. Click opens the detail modal.
  // `iconRender` overrides the <img> (spells render their atlas icon via an action instead of a URL).
  let { icon = "", name, level = 0, copies = 0, owned = true, equipped = false, locked = false,
        tone = "#9fb0c8", selected = false, onclick, iconRender } = $props<{
    icon?: string; name: string; level?: number; copies?: number; owned?: boolean;
    equipped?: boolean; locked?: boolean; tone?: string; selected?: boolean;
    onclick?: () => void; iconRender?: Snippet;
  }>();
</script>

<button class="tile" class:locked class:equipped class:selected style="--tone:{tone}"
        onclick={() => onclick?.()} title={name}>
  <div class="ico-wrap">
    {#if iconRender}
      <div class="ico ico-custom">{@render iconRender()}</div>
    {:else}
      <img class="ico" src={icon} alt={name}
           onerror={function (this: HTMLImageElement) { this.src = "/items/ItemGem.png"; this.onerror = null; }} />
    {/if}
    {#if copies > 0}<span class="copies">×{copies}</span>{/if}
    {#if owned && level > 0}<span class="lv">L{level}</span>{/if}
    {#if equipped}<span class="eq-dot" aria-label="equipped"></span>{/if}
  </div>
  <div class="nm">{name}</div>
</button>

<style>
  .tile {
    display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 5px;
    padding: 10px 5px 8px; min-height: 92px; cursor: pointer; position: relative;
    background: rgba(28,22,14,0.7); border: 1px solid color-mix(in srgb, var(--tone) 45%, rgba(120,90,40,0.4));
    border-radius: 11px; -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-fast), transform var(--dur-fast), box-shadow var(--dur-fast);
  }
  .tile:hover { filter: brightness(1.12); transform: translateY(-1px); }
  .tile:active { transform: scale(0.96); }
  .tile.selected { box-shadow: 0 0 0 2px var(--tone), 0 0 12px color-mix(in srgb, var(--tone) 60%, transparent); }
  .tile.equipped { border-color: var(--tone); box-shadow: inset 0 0 12px color-mix(in srgb, var(--tone) 25%, transparent); }
  .tile.locked { filter: grayscale(0.85) brightness(0.6); }
  .ico-wrap { position: relative; width: 100%; display: flex; align-items: center; justify-content: center; }
  .ico { width: 56px; height: 56px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6)); }
  .ico-custom { display: flex; align-items: center; justify-content: center; font-size: 26px; font-weight: 800; color: var(--gold-bright); }
  .ico-custom :global(img) { width: 52px; height: 52px; object-fit: contain; filter: drop-shadow(0 2px 3px rgba(0,0,0,0.6)); }
  .copies {
    position: absolute; top: -4px; right: 4px; font-size: 10px; font-weight: 900; line-height: 1;
    color: #fff; background: rgba(40,90,160,0.95); border: 1px solid #6cc0ff; border-radius: 6px; padding: 2px 4px;
  }
  .lv {
    position: absolute; bottom: -4px; left: 4px; font-size: 10px; font-weight: 900; line-height: 1;
    color: #1a1208; background: var(--tone); border-radius: 6px; padding: 2px 4px;
  }
  .eq-dot {
    position: absolute; bottom: -2px; right: -1px; width: 8px; height: 8px; border-radius: 50%;
    background: #7fe3a0; border: 1px solid #0a1a10; box-shadow: 0 0 5px rgba(120,240,160,0.8);
  }
  .nm {
    font-size: 11px; line-height: 1.15; color: var(--text); text-align: center; font-weight: 600;
    max-width: 100%; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2;
    -webkit-box-orient: vertical; overflow: hidden; word-break: break-word;
  }
  .tile.locked .nm { color: var(--text-dim); }
</style>
