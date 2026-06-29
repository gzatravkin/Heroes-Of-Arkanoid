<script lang="ts">
  import type { Snippet } from "svelte";
  import { copiesForNextLevel } from "./collection";

  // Detail modal for a collection entry: description, level progression (copies/threshold), Level Up + Equip.
  export interface DetailEntry {
    id: string; name: string; icon: string; tone: string;
    meta: string;            // e.g. "Core · EPIC" or "35 mana" or "RARE"
    description: string;
    owned: boolean;
    level: number;
    copies: number;
    maxLevel: number;
    equipped: boolean;
    equippable: boolean;     // false for signature spells / non-equippables
    equipLabel?: string;     // override ("Always slot 0" etc.)
    locked?: boolean;        // not owned yet
  }

  let { entry, busy = false, onClose, onLevelUp, onEquip, iconRender } = $props<{
    entry: DetailEntry; busy?: boolean;
    onClose: () => void; onLevelUp: (id: string) => void; onEquip: (id: string) => void;
    iconRender?: Snippet;
  }>();

  const atMax     = $derived(entry.level >= entry.maxLevel);
  const nextCost  = $derived(atMax ? 0 : copiesForNextLevel(entry.level));
  const canLevel  = $derived(entry.owned && !atMax && entry.copies >= nextCost);
  const pct       = $derived(nextCost > 0 ? Math.min(100, (entry.copies / nextCost) * 100) : 100);
</script>

<div class="ovl" role="button" tabindex="-1"
     onclick={onClose}
     onkeydown={(e) => { if (e.key === "Escape" || e.key === "Enter" || e.key === " ") onClose(); }}>
  <!-- stop propagation so clicks inside the card don't dismiss -->
  <div class="card" style="--tone:{entry.tone}" role="dialog" tabindex="-1"
       onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
    <button class="x" aria-label="Close" onclick={onClose}>✕</button>
    <div class="head">
      <div class="ico-box">
        {#if iconRender}
          <div class="ico ico-custom">{@render iconRender()}</div>
        {:else}
          <img class="ico" src={entry.icon} alt={entry.name}
               onerror={function (this: HTMLImageElement) { this.src = "/items/ItemGem.png"; this.onerror = null; }} />
        {/if}
      </div>
      <div class="head-txt">
        <div class="nm">{entry.name}</div>
        <div class="meta">{entry.meta}{entry.owned ? ` · Lv ${entry.level}/${entry.maxLevel}` : ""}</div>
      </div>
    </div>

    <p class="desc">{entry.description}</p>

    {#if entry.owned}
      <div class="prog">
        {#if atMax}
          <div class="prog-max">★ MAX LEVEL</div>
        {:else}
          <div class="prog-row">
            <span class="prog-lbl">Copies to Lv {entry.level + 1}</span>
            <span class="prog-num">{entry.copies}/{nextCost}</span>
          </div>
          <div class="bar"><div class="fill" style="width:{pct}%"></div></div>
          <button class="act level" disabled={!canLevel || busy} onclick={() => onLevelUp(entry.id)}>
            {canLevel ? `Level Up → Lv ${entry.level + 1}` : `Need ${nextCost - entry.copies} more`}
          </button>
        {/if}
      </div>
    {:else}
      <div class="prog"><div class="prog-locked">🔒 Buy Random to unlock</div></div>
    {/if}

    {#if entry.equippable && entry.owned}
      <button class="act equip" class:on={entry.equipped} disabled={busy} onclick={() => onEquip(entry.id)}>
        {entry.equipped ? "Unequip" : "Equip"}
      </button>
    {:else if entry.equipLabel}
      <div class="equip-note">{entry.equipLabel}</div>
    {/if}
  </div>
</div>

<style>
  .ovl {
    position: fixed; inset: 0; z-index: 260; display: flex; align-items: center; justify-content: center;
    background: rgba(0,0,0,0.74); animation: fade 0.18s ease; padding: 20px;
  }
  @keyframes fade { from { opacity: 0; } to { opacity: 1; } }
  .card {
    position: relative; width: min(360px, 92cqw); max-height: 88cqh; overflow-y: auto;
    background: linear-gradient(180deg, rgba(34,26,14,0.98), rgba(16,12,8,0.99));
    border: 2px solid var(--tone); border-radius: 16px; padding: var(--sp-4) var(--sp-4) var(--sp-3);
    box-shadow: 0 8px 40px rgba(0,0,0,0.7), 0 0 26px color-mix(in srgb, var(--tone) 35%, transparent);
    animation: pop 0.28s cubic-bezier(0.2,1.2,0.4,1); color: var(--text); font-family: var(--font-body);
  }
  @keyframes pop { from { transform: scale(0.85) translateY(8px); opacity: 0; } to { transform: scale(1) translateY(0); opacity: 1; } }
  .x { position: absolute; top: 8px; right: 10px; background: none; border: none; color: var(--text-dim);
    font-size: 18px; cursor: pointer; line-height: 1; padding: 4px; }
  .x:hover { color: var(--gold-bright); }
  .head { display: flex; gap: var(--sp-3); align-items: center; margin-bottom: var(--sp-3); }
  .ico-box { width: 64px; height: 64px; flex-shrink: 0; display: flex; align-items: center; justify-content: center;
    background: rgba(0,0,0,0.4); border: 1px solid var(--tone); border-radius: 12px; }
  .ico { width: 52px; height: 52px; object-fit: contain; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7)); }
  .ico-custom { display: flex; align-items: center; justify-content: center; font-size: 26px; font-weight: 800; color: var(--gold-bright); }
  .ico-custom :global(img) { width: 52px; height: 52px; object-fit: contain; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7)); }
  .head-txt { min-width: 0; }
  .nm { font-family: var(--font-display); font-size: var(--fs-large); font-weight: 800; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); line-height: 1.15; }
  .meta { font-size: var(--fs-tiny); text-transform: uppercase; letter-spacing: 0.08em; color: var(--tone); margin-top: 2px; }
  .desc { font-size: var(--fs-caption); color: var(--text-dim); line-height: 1.45; margin: 0 0 var(--sp-3); }
  .prog { margin-bottom: var(--sp-2h); }
  .prog-row { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 4px; }
  .prog-lbl { font-size: var(--fs-tiny); color: var(--text-dim); }
  .prog-num { font-size: var(--fs-caption); font-weight: 800; color: #6cc0ff; }
  .bar { height: 10px; border-radius: 5px; background: rgba(0,0,0,0.5); border: 1px solid rgba(110,180,255,0.35);
    overflow: hidden; margin-bottom: var(--sp-2); }
  .fill { height: 100%; background: linear-gradient(90deg, #6cc0ff, #3a8fd8); transition: width var(--dur-normal); }
  .prog-max { text-align: center; font-weight: 800; color: var(--gold-bright); letter-spacing: 0.12em; padding: 6px 0; }
  .prog-locked { text-align: center; font-size: var(--fs-caption); color: var(--text-dim); padding: 6px 0; }
  .act {
    width: 100%; min-height: 42px; border: none; border-radius: 9px; cursor: pointer; margin-top: var(--sp-1h);
    font-family: var(--font-body); font-weight: 800; font-size: var(--fs-body); color: #1a1020;
    transition: filter var(--dur-fast), transform var(--dur-fast);
  }
  .act.level { background: linear-gradient(180deg, #ffe06a, #d89a2e); }
  .act.equip { background: linear-gradient(180deg, #6fc2f2, #2a78c0); color: #fff; }
  .act.equip.on { background: linear-gradient(180deg, #7fe3a0, #3a9a5e); color: #082; }
  .act:hover:not(:disabled) { filter: brightness(1.12); }
  .act:active:not(:disabled) { transform: scale(0.97); }
  .act:disabled { filter: grayscale(0.7) brightness(0.55); cursor: default; }
  .equip-note { text-align: center; font-size: var(--fs-tiny); color: var(--text-dim); margin-top: var(--sp-1h); font-style: italic; }
</style>
