<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { showTutorial } from "./TutorialOverlay";
  import { navigateTo } from "../ui/transition";
  import { setSfxVolume, consumeSfx } from "../audio/Sfx";
  import { onAuthStateChanged } from "firebase/auth";
  import { fbAuth, isFirebaseConfigured } from "../net/firebase";
  import { linkGoogle, isLinkedGoogle, currentNickname } from "../net/FirebaseAuth";

  let audioEnabled = $state(localStorage.getItem("arkanoid_audio") !== "0");
  let musicOn      = $state(localStorage.getItem("arkanoid_music") === "1");
  let fxEnabled    = $state(localStorage.getItem("arkanoid_fx") !== "0");
  let sfxVolume    = $state(((): number => {
    const raw = localStorage.getItem("arkanoid_sfx_volume");
    return raw === null ? 100 : Math.max(0, Math.min(100, Number(raw) || 0));
  })());

  function setAudio(v: boolean)  { audioEnabled = v; localStorage.setItem("arkanoid_audio", v ? "1" : "0"); }
  function setMusic(v: boolean)  { musicOn = v;      localStorage.setItem("arkanoid_music", v ? "1" : "0"); }
  function setFx(v: boolean)     { fxEnabled = v;    localStorage.setItem("arkanoid_fx",    v ? "1" : "0"); }
  function setVolume(v: number)  { sfxVolume = v;    setSfxVolume(v); }
  // Play a sample tone so the player hears the level they're setting (only if audio is on).
  function previewVolume()       { if (audioEnabled) consumeSfx([{ type: "deflect" }]); }

  let gpgsLinked  = $state(isLinkedGoogle());
  let gpgsLinking = $state(false);
  let gpgsError   = $state("");
  let gpgsNick    = $state(currentNickname() ?? "");

  $effect(() => {
    if (!isFirebaseConfigured()) return;
    const unsub = onAuthStateChanged(fbAuth(), () => {
      gpgsLinked = isLinkedGoogle();
      gpgsNick   = currentNickname() ?? "";
    });
    return unsub;
  });

  async function connectGPGS() {
    gpgsLinking = true; gpgsError = "";
    const res = await linkGoogle();
    if (res.ok) {
      gpgsLinked = true;
      gpgsNick   = currentNickname() ?? gpgsNick;
    } else {
      gpgsError = res.error === "already_linked"
        ? "Already linked to a different account."
        : "Could not connect. Try again.";
    }
    gpgsLinking = false;
  }

  async function resetProgress() {
    if (!window.confirm("Reset all progress? This cannot be undone.")) return;
    await metaApi.reset();
    localStorage.removeItem("arkanoid_tutorial_seen");
    navigateTo("/?scene=menu");
  }
</script>

<div id="settings-scene" class="root">
  <div class="bg"></div>
  <div class="inner">
    <div class="ui-topbar">
      <a href="/?scene=menu" class="ui-back" aria-label="Back to menu"></a>
      <h1 class="ui-title">Settings</h1>
      <div class="ui-topbar-spacer"></div>
    </div>
    <div class="panel">
      <div class="row">
        <div class="row-text">
          <div class="row-label">Tutorial</div>
          <div class="row-desc">Re-play the how-to-play slides</div>
        </div>
        <button id="set-btn-replay" class="action-btn" onclick={() => showTutorial(document.body)}>Replay</button>
      </div>
      <div class="row">
        <div class="row-text">
          <div class="row-label">Audio</div>
          <div class="row-desc">Sound effects (synthesized — impacts, spells, bosses)</div>
        </div>
        <label class="toggle">
          <input id="set-toggle-audio" type="checkbox" checked={audioEnabled}
                 onchange={e => setAudio((e.target as HTMLInputElement).checked)} />
          <span class="toggle-slider"></span>
        </label>
      </div>
      <div class="row" class:row-disabled={!audioEnabled}>
        <div class="row-text">
          <div class="row-label">SFX Volume</div>
          <div class="row-desc">Loudness of sound effects</div>
        </div>
        <div class="vol-control">
          <input id="set-slider-volume" class="vol-slider" type="range"
                 min="0" max="100" step="5" value={sfxVolume} disabled={!audioEnabled}
                 aria-label="Sound effects volume"
                 oninput={e => setVolume(Number((e.target as HTMLInputElement).value))}
                 onchange={previewVolume} />
          <span class="vol-value">{sfxVolume}%</span>
        </div>
      </div>
      <div class="row">
        <div class="row-text">
          <div class="row-label">Music</div>
          <div class="row-desc">Per-biome ambient music (experimental)</div>
        </div>
        <label class="toggle">
          <input id="set-toggle-music" type="checkbox" checked={musicOn}
                 onchange={e => setMusic((e.target as HTMLInputElement).checked)} />
          <span class="toggle-slider"></span>
        </label>
      </div>
      <div class="row">
        <div class="row-text">
          <div class="row-label">FX Effects</div>
          <div class="row-desc">Screen shake and particle effects</div>
        </div>
        <label class="toggle">
          <input id="set-toggle-fx" type="checkbox" checked={fxEnabled}
                 onchange={e => setFx((e.target as HTMLInputElement).checked)} />
          <span class="toggle-slider"></span>
        </label>
      </div>
      <hr class="divider" />

      {#if isFirebaseConfigured()}
      <div class="row">
        <div class="row-text">
          <div class="row-label">
            <svg class="gpgs-logo" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 14H9V8h2v8zm4 0h-2V8h2v8z" fill="currentColor"/>
            </svg>
            Google Play Games
          </div>
          {#if gpgsLinked}
            <div class="row-desc gpgs-ok">✓ Connected{gpgsNick ? ` as ${gpgsNick}` : ""}</div>
          {:else if gpgsError}
            <div class="row-desc gpgs-err">{gpgsError}</div>
          {:else}
            <div class="row-desc">Link your Google account to sync scores &amp; earn achievements on any device.</div>
          {/if}
        </div>
        {#if gpgsLinked}
          <span class="gpgs-badge">Connected</span>
        {:else}
          <button class="action-btn" onclick={connectGPGS} disabled={gpgsLinking}>
            {gpgsLinking ? "Connecting…" : "Connect"}
          </button>
        {/if}
      </div>
      {/if}

      <hr class="divider" />
      <div class="row">
        <div class="row-text">
          <div class="row-label">Reset</div>
          <div class="row-desc">Wipe all progress and start fresh</div>
        </div>
        <button id="set-btn-reset" class="action-btn action-danger" onclick={resetProgress}>Reset Progress</button>
      </div>
    </div>
    <div class="version">Heroes of Arkanoid II</div>
  </div>
</div>

<style>
  .root {
    position: relative; min-height: 100cqh;
    overflow-x: hidden;
    color: var(--text);
    font-family: var(--font-body);
    display: flex; flex-direction: column;
    box-sizing: border-box;
    padding: env(safe-area-inset-top,0px) env(safe-area-inset-right,0px)
             env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px);
  }
  .bg {
    position: absolute; inset: 0;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    z-index: 0;
  }
  .inner {
    position: relative; z-index: 1;
    display: flex; flex-direction: column;
    padding: max(12px, env(safe-area-inset-top,0px)) 16px 24px 16px;
    gap: var(--sp-4h);
  }
  .panel {
    width: min(360px, 96cqw); display: flex; flex-direction: column;
    gap: var(--sp-3); margin: 0 auto;
  }
  .row {
    display: flex; align-items: center; justify-content: space-between;
    gap: var(--sp-4); padding: var(--sp-3) var(--sp-3h);
    background: none; border-style: solid; border-width: 12px 14px;
    border-image: url('/ui/BarGoods.png') 26 30 26 30 fill stretch;
  }
  .row-text { flex: 1; }
  .row-label { font-size: var(--fs-body); font-weight: 700; color: var(--gold-bright); }
  .row-desc  { font-size: var(--fs-caption); color: var(--text-dim); margin-top: 3px; line-height: 1.3; }
  .row-disabled { filter: saturate(0.3) brightness(0.7); }
  .divider   { border: none; border-top: 1px solid var(--gold-dim); margin: var(--sp-1) 0; }

  /* SFX volume slider — themed gold, touch-friendly track + thumb. */
  .vol-control { display: flex; align-items: center; gap: var(--sp-2h); flex-shrink: 0; }
  .vol-value {
    min-width: 44px; text-align: right; font-variant-numeric: tabular-nums;
    font-size: var(--fs-caption); font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
  }
  .vol-slider {
    -webkit-appearance: none; appearance: none; width: 110px; height: 44px;
    background: transparent; cursor: pointer; touch-action: manipulation;
  }
  .vol-slider:disabled { cursor: default; }
  .vol-slider::-webkit-slider-runnable-track {
    height: 8px; border-radius: 999px;
    background: #241a0d; border: 1px solid var(--gold-dim);
  }
  .vol-slider::-moz-range-track {
    height: 8px; border-radius: 999px;
    background: #241a0d; border: 1px solid var(--gold-dim);
  }
  .vol-slider::-webkit-slider-thumb {
    -webkit-appearance: none; appearance: none; margin-top: -8px;
    height: 24px; width: 24px; border-radius: 50%;
    background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);
    box-shadow: 0 1px 3px rgba(0,0,0,0.7);
  }
  .vol-slider::-moz-range-thumb {
    height: 24px; width: 24px; border: none; border-radius: 50%;
    background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);
    box-shadow: 0 1px 3px rgba(0,0,0,0.7);
  }
  .vol-slider:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 4px; border-radius: 6px; }
  .action-btn {
    height: 40px; min-width: 100px; padding: 0 16px; flex-shrink: 0;
    background: none; border-style: solid; border-width: 8px 18px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    cursor: pointer; font-family: var(--font-body); font-size: var(--fs-body);
    font-weight: 700; color: var(--gold-bright);
    text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    -webkit-tap-highlight-color: transparent; touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .action-btn:hover  { filter: brightness(1.18); }
  .action-btn:active { transform: scale(0.96); }
  .action-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .action-btn:disabled { filter: saturate(0.25) brightness(0.65); cursor: default; }
  .action-danger { color: var(--color-unequip); }
  .toggle {
    position: relative; display: inline-flex; align-items: center;
    width: 48px; height: 44px; flex-shrink: 0; cursor: pointer;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .toggle:hover  { filter: brightness(1.15); }
  .toggle:active { transform: scale(0.96); }
  .toggle input { opacity: 0; width: 0; height: 0; }
  .toggle-slider {
    position: absolute; left: 0; right: 0;
    top: 50%; transform: translateY(-50%); height: 28px;
    background: #241a0d; border: 1px solid var(--gold-dim); border-radius: 999px;
    transition: background var(--dur-normal), box-shadow var(--dur-normal);
  }
  .toggle-slider::before {
    content: ''; position: absolute;
    height: 20px; width: 20px; left: 3px; bottom: 3px;
    background: radial-gradient(circle at 38% 32%, #ffe9b0, #d8a84e 70%);
    border-radius: 50%; transition: transform var(--dur-normal);
  }
  .toggle input:checked + .toggle-slider {
    background: #3a2a10; box-shadow: inset 0 0 8px rgba(255,190,80,0.5);
  }
  .toggle input:checked + .toggle-slider::before { transform: translateX(20px); }
  .gpgs-logo {
    width: 16px; height: 16px; vertical-align: middle;
    margin-right: 5px; margin-bottom: 2px; color: #4caf50;
  }
  .gpgs-ok  { color: #4caf50; }
  .gpgs-err { color: #ef5350; }
  .gpgs-badge {
    flex-shrink: 0; padding: 6px 12px; border-radius: 20px;
    background: rgba(76,175,80,0.15); border: 1px solid rgba(76,175,80,0.4);
    color: #4caf50; font-size: var(--fs-caption); font-weight: 700;
    white-space: nowrap;
  }
  .version {
    margin-top: var(--sp-4h); text-align: center;
    font-size: var(--fs-caption); color: var(--text-faint);
    font-family: var(--font-display); letter-spacing: 0.05em;
  }
</style>
