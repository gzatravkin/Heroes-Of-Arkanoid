<script lang="ts">
  import { onAuthStateChanged } from "firebase/auth";
  import { fbAuth, isFirebaseConfigured } from "../net/firebase";
  import { linkGoogle, currentNickname, isLinkedGoogle } from "../net/FirebaseAuth";

  let nickname = $state(currentNickname() ?? "…");
  let linked   = $state(isLinkedGoogle());
  let linking  = $state(false);
  let showMenu = $state(false);

  $effect(() => {
    if (!isFirebaseConfigured()) return;
    const unsub = onAuthStateChanged(fbAuth(), () => {
      nickname = currentNickname() ?? nickname;
      linked   = isLinkedGoogle();
    });
    return unsub;
  });

  async function handleLink() {
    if (linked || linking) return;
    linking = true;
    const res = await linkGoogle();
    if (res.ok) {
      linked   = true;
      nickname = currentNickname() ?? nickname;
    }
    linking  = false;
    showMenu = false;
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === "Enter" || e.key === " ") showMenu = !showMenu;
  }
</script>

{#if isFirebaseConfigured()}
<div class="ab-wrap">
  <button class="ab-badge" onclick={() => showMenu = !showMenu} onkeydown={onKeydown}
    aria-label="Account: {nickname}" aria-expanded={showMenu}>
    <span class="ab-nick">{nickname}</span>
    <span class="ab-dot {linked ? 'ab-linked' : 'ab-anon'}" aria-hidden="true">
      {linked ? "●" : "○"}
    </span>
  </button>

  {#if showMenu}
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="ab-backdrop" onclick={() => showMenu = false} onkeydown={() => {}}></div>
  <div class="ab-menu" role="menu">
    {#if !linked}
      <button class="ab-menu-btn" onclick={handleLink} disabled={linking} role="menuitem">
        {linking ? "Linking…" : "🔗 Link with Google"}
      </button>
      <p class="ab-menu-hint">Link to sync progress across devices and join Google Play leaderboards.</p>
    {:else}
      <div class="ab-menu-ok" role="menuitem">✓ Google account linked</div>
    {/if}
  </div>
  {/if}
</div>
{/if}

<style>
  .ab-wrap { position: relative; }

  .ab-badge {
    display: flex; align-items: center; gap: 5px;
    padding: 5px 11px; border-radius: 20px;
    background: rgba(0,0,0,0.45); border: 1px solid rgba(180,140,60,0.3);
    cursor: pointer; font-family: var(--font-body); font-size: var(--fs-tiny);
    color: var(--text-dim); transition: border-color var(--dur-normal);
    min-height: 36px;
  }
  .ab-badge:hover  { border-color: rgba(180,140,60,0.6); }
  .ab-badge:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 2px; border-radius: 20px; }
  .ab-nick   { color: var(--gold-bright); font-weight: 700; }
  .ab-dot    { font-size: 9px; }
  .ab-linked { color: #4caf50; }
  .ab-anon   { color: #777; }

  .ab-backdrop {
    position: fixed; inset: 0; z-index: 498; background: transparent;
  }
  .ab-menu {
    position: absolute; top: calc(100% + 6px); right: 0;
    background: rgba(16,11,8,0.97); border: 1px solid rgba(180,140,60,0.4);
    border-radius: 10px; padding: var(--sp-2); z-index: 499; min-width: 200px;
    box-shadow: 0 4px 20px rgba(0,0,0,0.75);
  }
  .ab-menu-btn {
    width: 100%; padding: 10px 14px;
    background: rgba(180,140,60,0.15); border: 1px solid rgba(180,140,60,0.35);
    border-radius: 8px; color: var(--gold-bright);
    font-family: var(--font-body); font-size: var(--fs-body); font-weight: 700;
    cursor: pointer; transition: background var(--dur-normal);
  }
  .ab-menu-btn:hover:not(:disabled) { background: rgba(180,140,60,0.28); }
  .ab-menu-btn:disabled { opacity: 0.5; cursor: default; }
  .ab-menu-hint {
    margin: var(--sp-1h) 0 0; padding: 0 4px;
    font-size: var(--fs-tiny); color: rgba(255,255,255,0.38); line-height: 1.4;
  }
  .ab-menu-ok {
    padding: 10px 14px; color: #4caf50;
    font-size: var(--fs-body); font-weight: 700;
  }
</style>
