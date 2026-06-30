<script lang="ts">
  import { subscribeEvent, claimEventReward, type ActiveEvent } from "../net/FirestoreEvents";
  import { isFirebaseConfigured } from "../net/firebase";

  interface Props {
    /** Called after reward is claimed so the parent can credit Souls via devCoins. */
    onClaim?: (souls: number) => void;
  }
  const { onClaim }: Props = $props();

  let event    = $state<ActiveEvent | null>(null);
  let claiming = $state(false);
  let claimed  = $state(false);

  $effect(() => {
    if (!isFirebaseConfigured()) return;
    return subscribeEvent(ev => { event = ev; });
  });

  const pct = $derived(
    event ? Math.min(100, Math.round((event.currentCount / Math.max(1, event.targetCount)) * 100)) : 0,
  );
  const hoursLeft = $derived(
    event ? Math.max(0, Math.floor((event.endsAt - Date.now()) / 3_600_000)) : 0,
  );

  async function claim() {
    if (!event || claiming || claimed) return;
    claiming = true;
    const souls = await claimEventReward(event.id);
    claiming = false;
    if (souls > 0) { claimed = true; onClaim?.(souls); }
  }
</script>

{#if event && !claimed}
<div class="ev {event.status === 'complete' ? 'ev-done' : ''}">
  <div class="ev-row">
    <span class="ev-name">{event.name}</span>
    {#if event.status === "active"}
      <span class="ev-time">{hoursLeft}h left</span>
    {:else}
      <span class="ev-badge">Complete!</span>
    {/if}
  </div>

  <div class="ev-track">
    <div class="ev-fill" style="width:{pct}%"></div>
  </div>

  <div class="ev-row ev-footer">
    <span class="ev-count">{event.currentCount.toLocaleString()} / {event.targetCount.toLocaleString()}</span>
    <span class="ev-reward">+{event.rewardSouls} ◆ for all</span>
  </div>

  {#if event.status === "complete"}
    <button class="ev-claim" onclick={claim} disabled={claiming}>
      {claiming ? "Claiming…" : "Claim Reward"}
    </button>
  {/if}
</div>
{/if}

<style>
  .ev {
    margin: var(--sp-2) var(--sp-4);
    padding: var(--sp-2) var(--sp-3h);
    background: rgba(70,35,0,0.72); border: 1px solid rgba(220,160,60,0.38);
    border-radius: 12px; font-family: var(--font-body);
  }
  .ev-done { border-color: rgba(60,200,100,0.5); background: rgba(0,50,20,0.72); }

  .ev-row   { display: flex; justify-content: space-between; align-items: center; }
  .ev-footer { margin-top: var(--sp-1); }
  .ev-name  { font-weight: 800; color: var(--gold-bright); font-size: var(--fs-body); }
  .ev-time  { font-size: var(--fs-tiny); color: var(--text-dim); }
  .ev-badge { font-size: var(--fs-tiny); font-weight: 700; color: #4caf50; }

  .ev-track {
    height: 7px; background: rgba(0,0,0,0.4); border-radius: 4px;
    overflow: hidden; margin: var(--sp-1h) 0 0;
  }
  .ev-fill {
    height: 100%; background: linear-gradient(90deg, #d8a84e, #ff9040);
    border-radius: 4px; transition: width 0.6s ease;
  }
  .ev-done .ev-fill { background: linear-gradient(90deg, #43a047, #76c442); }

  .ev-count  { font-size: var(--fs-tiny); color: var(--text-dim); }
  .ev-reward { font-size: var(--fs-tiny); color: #6cc0ff; font-weight: 700; }

  .ev-claim {
    margin-top: var(--sp-2); width: 100%; padding: 10px;
    background: linear-gradient(180deg, rgba(70,190,110,0.9), rgba(35,130,60,0.9));
    border: 1px solid rgba(70,200,110,0.5); border-radius: 8px;
    color: #fff; font-family: var(--font-body); font-weight: 700;
    font-size: var(--fs-body); cursor: pointer;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .ev-claim:hover:not(:disabled) { filter: brightness(1.15); }
  .ev-claim:active { transform: scale(0.97); }
  .ev-claim:disabled { opacity: 0.5; cursor: default; }
</style>
