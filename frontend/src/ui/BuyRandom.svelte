<script lang="ts">
  import type { RollKind, RollResult, RollState } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { log } from "../log";

  // The single "buy a random one" action, shared by Items / Modules / Spells / Heroes.
  // Same button + same reveal ceremony everywhere (design: one consistent appearance of what you bought).
  let { kind, noun, onrolled } = $props<{
    kind: RollKind;
    noun: string;           // "Item", "Module", "Spell", "Hero" — for button + reveal copy
    onrolled?: () => void;  // parent reloads its collection after a successful roll
  }>();

  const COIN: Record<RollKind, "sparks" | "souls"> = {
    card: "sparks", module: "sparks", spell: "souls", hero: "souls",
  };
  const coin = $derived(COIN[kind as RollKind]);
  const coinName = $derived(coin === "souls" ? "Souls" : "Sparks");

  let state   = $state<RollState | null>(null);
  let rolling = $state(false);
  let reveal  = $state<RollResult | null>(null);

  async function load() { try { state = await metaApi.rollState(); } catch { /* keep */ } }
  load();

  const pool = $derived(state ? state[kind] : null);
  const balance = $derived(coin === "souls" ? state?.souls ?? 0 : state?.sparks ?? 0);
  const canAfford = $derived(!!pool && pool.canRoll && balance >= (pool?.cost ?? Infinity));

  async function doRoll() {
    if (rolling || !canAfford) return;
    rolling = true;
    reveal = null;
    try {
      const res = await metaApi.roll(kind);
      await new Promise((r) => setTimeout(r, 420)); // brief anticipation before the reveal
      if (res.ok && res.result) { reveal = res.result; log("buy-random", kind, res.result); }
      await load();
      window.dispatchEvent(new CustomEvent("currency-changed"));
      onrolled?.();
    } finally {
      rolling = false;
    }
  }

  function revealText(r: RollResult): string {
    if (kind === "hero") {
      // Heroes don't auto-ascend: a first pull UNLOCKS the hero; a duplicate banks a pip (ascend manually).
      if (r.wasNew) return `New hero unlocked: ${r.id}!`;
      if (r.wasted) return `${r.id} is already ★6 (maxed)`;
      return `${r.id} — duplicate! ${r.copies} pip${r.copies === 1 ? "" : "s"} toward next ★`;
    }
    if (r.wasted)  return `Duplicate — ${r.id} already maxed`;
    if (r.wasNew)  return `New ${noun}: ${r.id}`;
    return `${r.id} — duplicate! ${r.copies} ${r.copies === 1 ? "copy" : "copies"} banked`;
  }
  function tone(r: RollResult) { return r.wasted ? "wasted" : r.wasNew ? "new" : "level"; }
</script>

<button class="buyrand coin-{coin}"
        disabled={rolling || !canAfford}
        onclick={doRoll}
        title={pool && !pool.canRoll ? `All ${noun.toLowerCase()}s collected` : `Buy a random ${noun.toLowerCase()}`}>
  {#if pool && !pool.canRoll}
    All collected
  {:else}
    <span class="br-glyph">{coin === "souls" ? "◆" : "✦"}</span>
    Buy Random {noun} · {pool?.cost ?? "…"} {coinName}
  {/if}
</button>

{#if rolling || reveal}
  <div class="br-overlay" role="button" tabindex="-1"
       onclick={() => { if (!rolling) reveal = null; }}
       onkeydown={(e) => { if (!rolling && (e.key === "Enter" || e.key === "Escape" || e.key === " ")) reveal = null; }}>
    {#if rolling}
      <div class="br-spinner">{coin === "souls" ? "◆" : "✦"}</div>
    {:else if reveal}
      <div class="br-card {tone(reveal)}">
        <div class="br-kind">{noun.toUpperCase()}</div>
        <div class="br-line">{revealText(reveal)}</div>
        <div class="br-tap">tap to continue</div>
      </div>
    {/if}
  </div>
{/if}

<style>
  .buyrand {
    width: 100%; min-height: 48px; border: none; border-radius: 10px; cursor: pointer;
    display: inline-flex; align-items: center; justify-content: center; gap: 7px;
    font-family: var(--font-body); font-weight: 800; font-size: var(--fs-body);
    color: #1a1020; letter-spacing: 0.02em;
    box-shadow: 0 2px 10px rgba(0,0,0,0.45);
    transition: filter var(--dur-fast), transform var(--dur-fast);
  }
  .br-glyph { font-size: 1.1em; line-height: 1; }
  .coin-sparks { background: linear-gradient(180deg, #ffe06a, #d89a2e); }
  .coin-souls  { background: linear-gradient(180deg, #6fc2f2, #2a78c0); color: #fff; }
  .buyrand:hover:not(:disabled)  { filter: brightness(1.12); }
  .buyrand:active:not(:disabled) { transform: scale(0.97); }
  .buyrand:disabled { filter: grayscale(0.7) brightness(0.55); cursor: default; }

  .br-overlay {
    position: fixed; inset: 0; z-index: 250; display: flex; align-items: center; justify-content: center;
    background: radial-gradient(ellipse at center, rgba(40,28,10,0.66), rgba(0,0,0,0.84));
    animation: br-fade 0.2s ease;
  }
  @keyframes br-fade { from { opacity: 0; } to { opacity: 1; } }
  .br-spinner { font-size: 64px; color: #ffd56a; text-shadow: 0 0 24px rgba(255,200,80,0.85); animation: br-spin 0.7s linear infinite; }
  @keyframes br-spin { to { transform: rotate(360deg) scale(1.06); } }
  .br-card {
    text-align: center; min-width: 240px; max-width: 86cqw; padding: var(--sp-5) var(--sp-4);
    border-radius: 16px; border: 2px solid; animation: br-pop 0.4s cubic-bezier(0.2,1.3,0.4,1);
  }
  .br-card.new    { border-color: #ffd56a; background: radial-gradient(ellipse at center, rgba(120,85,20,0.6), rgba(40,28,8,0.92)); box-shadow: 0 0 40px rgba(220,170,60,0.6); }
  .br-card.level  { border-color: #6cc0ff; background: radial-gradient(ellipse at center, rgba(30,70,130,0.55), rgba(10,24,44,0.92)); box-shadow: 0 0 36px rgba(90,160,240,0.5); }
  .br-card.wasted { border-color: #7a6e54; background: radial-gradient(ellipse at center, rgba(60,52,38,0.7), rgba(24,20,14,0.92)); }
  .br-kind { font-family: var(--font-display); font-weight: 800; letter-spacing: 0.22em; font-size: var(--fs-small); color: #c8b890; margin-bottom: var(--sp-2); }
  .br-line { font-weight: 800; font-size: var(--fs-large); line-height: 1.3; }
  .br-card.new    .br-line { color: #fff4c2; }
  .br-card.level  .br-line { color: #bfe4ff; }
  .br-card.wasted .br-line { color: #b3a890; }
  .br-tap { margin-top: var(--sp-3); font-size: var(--fs-tiny); color: var(--text-dim); letter-spacing: 0.08em; }
  @keyframes br-pop { from { transform: scale(0.6) translateY(10px); opacity: 0; } to { transform: scale(1) translateY(0); opacity: 1; } }
</style>
