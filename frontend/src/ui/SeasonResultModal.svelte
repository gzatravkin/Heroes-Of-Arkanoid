<script lang="ts">
  interface Props {
    tier:    "Diamond" | "Gold" | "Silver" | "Bronze";
    rank:    number;
    week:    number;
    onClose: () => void;
  }
  const { tier, rank, week, onClose }: Props = $props();

  const TIER_STYLE: Record<string, { color: string; glow: string; emoji: string }> = {
    Diamond: { color: "#a8d8ff", glow: "rgba(168,216,255,0.5)", emoji: "💎" },
    Gold:    { color: "#ffd56a", glow: "rgba(255,213,106,0.5)", emoji: "🥇" },
    Silver:  { color: "#c8c8d8", glow: "rgba(200,200,216,0.4)", emoji: "🥈" },
    Bronze:  { color: "#d4905c", glow: "rgba(212,144,92,0.4)",  emoji: "🥉" },
  };
  const style = $derived(TIER_STYLE[tier] ?? TIER_STYLE.Bronze);
</script>

<!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
<div class="sr-overlay" role="dialog" aria-modal="true" aria-label="Season result">
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="sr-modal" onclick={e => e.stopPropagation()} onkeydown={() => {}}>
    <div class="sr-emoji">{style.emoji}</div>
    <div class="sr-tier"
      style="color:{style.color};text-shadow:0 0 24px {style.glow},0 0 48px {style.glow}">
      {tier}
    </div>
    <div class="sr-label">Season Result · Week {week}</div>
    <div class="sr-rank">Rank <strong>#{rank}</strong></div>
    <button class="sr-close" onclick={onClose}>Continue</button>
  </div>
</div>

<style>
  .sr-overlay {
    position: fixed; inset: 0; background: rgba(0,0,0,0.82); z-index: 900;
    display: flex; align-items: center; justify-content: center;
    animation: sr-fade-in 0.25s ease;
  }
  @keyframes sr-fade-in { from { opacity: 0; } to { opacity: 1; } }

  .sr-modal {
    background: linear-gradient(180deg, rgba(18,12,30,0.99), rgba(8,6,16,0.99));
    border: 2px solid rgba(180,140,60,0.35); border-radius: 20px;
    padding: var(--sp-6) var(--sp-8); text-align: center;
    min-width: min(280px, 90cqw); font-family: var(--font-body);
    box-shadow: 0 0 60px rgba(0,0,0,0.9), 0 0 100px rgba(80,40,160,0.18);
    animation: sr-pop-in 0.35s cubic-bezier(0.2,1.2,0.4,1);
  }
  @keyframes sr-pop-in {
    from { transform: scale(0.7); opacity: 0; }
    to   { transform: scale(1);   opacity: 1; }
  }

  .sr-emoji { font-size: 3rem; margin-bottom: var(--sp-2); line-height: 1; }
  .sr-tier  {
    font-size: 2.4rem; font-weight: 900; letter-spacing: 0.06em;
    margin-bottom: var(--sp-1h); transition: color 0.2s;
  }
  .sr-label {
    font-size: var(--fs-small); color: rgba(255,255,255,0.4);
    text-transform: uppercase; letter-spacing: 0.12em; margin-bottom: var(--sp-3);
  }
  .sr-rank {
    font-size: var(--fs-section); color: var(--text); margin-bottom: var(--sp-6);
  }
  .sr-rank strong { color: var(--gold-bright); font-weight: 900; }

  .sr-close {
    padding: 12px 36px;
    background: rgba(180,140,60,0.18); border: 1px solid rgba(180,140,60,0.4);
    border-radius: 10px; color: var(--gold-bright);
    font-family: var(--font-body); font-weight: 700; font-size: var(--fs-body);
    cursor: pointer; transition: background var(--dur-normal), transform var(--dur-fast);
    min-height: 48px;
  }
  .sr-close:hover  { background: rgba(180,140,60,0.32); }
  .sr-close:active { transform: scale(0.96); }
  .sr-close:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 10px; }
</style>
