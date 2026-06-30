<script lang="ts">
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";
  import { log } from "../log";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import type { CampaignNode, FeatureInfo } from "../net/metaApi";

  const FALLBACK_LEVEL = "hell-1";

  interface DockEntry { id: string; label: string; scene: string; icon: string; }
  // Grouped into clear sections (IA pass) instead of a flat 12-icon grid.
  const GROUPS: { title: string; entries: DockEntry[] }[] = [
    { title: "Build", entries: [
      { id: "btn-loadout",   label: "Loadout", scene: "loadout",   icon: "/art/SpellFireball.png" },
      { id: "btn-cards",     label: "Items",   scene: "cards",     icon: "/ui/BonusRandomSpell.png" },
      { id: "btn-modules",   label: "Modules", scene: "modules",   icon: "/ui/BonusRock.png" },
    ]},
    { title: "Hero", entries: [
      { id: "btn-characters", label: "Heroes",    scene: "characters", icon: "/ui/FireHeroIco.png" },
      { id: "btn-masteries",  label: "Masteries", scene: "masteries",  icon: "/ui/FireHeroIco.png" },
    ]},
    { title: "Events", entries: [
      { id: "btn-daily",        label: "Daily",  scene: "daily",        icon: "/ui/BonusHP.png" },
      { id: "btn-league",       label: "League", scene: "league",       icon: "/ui/Gem.png" },
      { id: "btn-season",       label: "Season", scene: "season",       icon: "/ui/BonusFire.png" },
      { id: "btn-achievements", label: "Awards", scene: "achievements", icon: "/achievements/achievementLvl2Eng.png" },
    ]},
  ];

  const EMBER_PARTICLES = [
    { left: 10, delay:  0.0, dur:  9, size: 3, bottom: 22 },
    { left: 22, delay:  2.5, dur: 12, size: 4, bottom: 26 },
    { left: 35, delay:  5.0, dur: 10, size: 3, bottom: 20 },
    { left: 48, delay:  1.0, dur: 14, size: 5, bottom: 18 },
    { left: 60, delay:  7.0, dur:  8, size: 3, bottom: 30 },
    { left: 72, delay:  3.5, dur: 11, size: 4, bottom: 24 },
    { left: 83, delay:  9.0, dur: 13, size: 3, bottom: 28 },
    { left: 18, delay:  6.0, dur: 10, size: 4, bottom: 21 },
    { left: 54, delay:  4.0, dur:  9, size: 3, bottom: 25 },
    { left: 90, delay:  8.0, dur: 12, size: 4, bottom: 23 },
  ];

  function furthestNode(nodes: CampaignNode[]): CampaignNode | null {
    const playable = nodes.filter(n => n.unlocked);
    if (!playable.length) return nodes[0] ?? null;
    // Continue resumes the NEXT challenge: the furthest unlocked node not yet beaten.
    // (The old code returned the last unlocked node even if completed, so clearing a
    // level left Continue pointing back at the level you just beat.)
    const unbeaten = playable.filter(n => !n.completed);
    return (unbeaten.length ? unbeaten : playable)[(unbeaten.length ? unbeaten : playable).length - 1];
  }

  let continueLevel = $state(FALLBACK_LEVEL);
  let continueLabel = $state("Hell I");

  function play() {
    log("menu", "continue", { level: continueLevel });
    navigateTo(`/?scene=battle&level=${continueLevel}&from=campaign`);
  }

  metaApi.getCampaign()
    .then(camp => {
      const node = furthestNode(camp.nodes);
      if (!node) return;
      continueLevel = node.id;
      continueLabel = node.label;
      log("menu", "furthest-node", { level: node.id, label: node.label });
    })
    .catch(err => log("menu", "campaign-load-failed", { err: String(err) }));

  // ── Progressive feature unlocks (campaign-gated) ────────────────────────────
  // Feature enum name → dock scene id. Anything not here is always available.
  const FEATURE_SCENE: Record<string, string> = {
    Cards: "cards", Modules: "modules", Daily: "daily", League: "league", Season: "season",
  };
  let lockByScene = $state<Record<string, FeatureInfo>>({}); // scene → info, only when LOCKED
  let toast = $state("");
  let toastTimer: ReturnType<typeof setTimeout> | null = null;

  metaApi.getFeatures()
    .then(res => {
      const m: Record<string, FeatureInfo> = {};
      for (const f of res.features) {
        const scene = FEATURE_SCENE[f.feature];
        if (scene && !f.unlocked) m[scene] = f;
      }
      lockByScene = m;
    })
    .catch(err => log("menu", "features-load-failed", { err: String(err) }));

  // Show "Get it on Google Play" only on Android and only when NOT already running inside the TWA.
  const isAndroid = typeof navigator !== "undefined" && /Android/i.test(navigator.userAgent);
  const isInTwa   = typeof document  !== "undefined" && document.referrer.startsWith("android-app://");
  const showPlayBadge = isAndroid && !isInTwa;

  function openDock(entry: DockEntry) {
    const lock = lockByScene[entry.scene];
    if (lock) {
      toast = `🔒 ${lock.name} unlocks after: ${lock.requiredLabel}`;
      if (toastTimer) clearTimeout(toastTimer);
      toastTimer = setTimeout(() => (toast = ""), 2600);
      return;
    }
    log("menu", "open-scene", { scene: entry.scene });
    navigateTo(`/?scene=${entry.scene}`);
  }
</script>

<div id="menu" class="root">
  <h1 style="position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap">ARKANOID RPG</h1>
  <div class="bg"></div>
  <div class="keyart"></div>
  <div class="ember-glow"></div>
  <div class="particles">
    {#each EMBER_PARTICLES as p}
      <div class="ember" style="left:{p.left}cqw;bottom:{p.bottom}cqh;animation-delay:-{p.delay}s;animation-duration:{p.dur}s;width:{p.size}px;height:{p.size}px"></div>
    {/each}
  </div>
  <div class="col">
    <div class="logo"></div>
    <div class="cta-wrap">
      <button id="btn-continue" data-level={continueLevel} class="art-btn btn-continue" onclick={play}>
        <span class="btn-kicker">Continue</span>
        <span id="continue-node-label" class="btn-node">{continueLabel}</span>
      </button>
      <button id="btn-campaign" class="art-btn btn-map"
              onclick={() => { log("menu", "open-map"); navigateTo("/?scene=campaign"); }}>
        <span class="btn-label">Campaign Map</span>
      </button>
    </div>
    <div class="dock-groups">
      {#each GROUPS as group}
        <div class="dock-section">
          <div class="dock-section-label">{group.title}</div>
          <div class="dock">
            {#each group.entries as entry}
              {@const locked = !!lockByScene[entry.scene]}
              <button id={entry.id} class="dock-btn" class:locked aria-label={entry.label}
                      data-locked={locked} onclick={() => openDock(entry)}>
                <span class="dock-ico" style="background-image:url('{entry.icon}')"></span>
                <span class="dock-label">{entry.label}</span>
                {#if locked}<span class="dock-lock">🔒</span>{/if}
              </button>
            {/each}
          </div>
        </div>
      {/each}
    </div>
  </div>
  {#if showPlayBadge}
    <a class="play-badge" href="https://play.google.com/store/apps/details?id=com.herosofarkanoid.twa"
       target="_blank" rel="noopener noreferrer" aria-label="Get it on Google Play">
      <svg class="play-icon" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M3 20.5v-17c0-.83 1-.13 1-.13l14 8.63L4 20.63s-1 .7-1-.13z" fill="#4fc3f7"/>
        <path d="M3 3.5l10.5 8.5L3 20.5V3.5z" fill="#29b6f6"/>
        <path d="M13.5 12L17 9.5 4.5 2.5 13.5 12z" fill="#b2ebf2"/>
        <path d="M13.5 12l-9 8.5L17 14.5 13.5 12z" fill="#80deea"/>
      </svg>
      <span class="play-text"><span class="play-sub">Get it on</span><span class="play-name">Google Play</span></span>
    </a>
  {/if}
  <div class="menu-coins"><CurrencyBar /></div>
  <button id="btn-settings" class="menu-settings" aria-label="Settings"
          onclick={() => navigateTo('/?scene=settings')}>
    <span class="dock-ico" style="background-image:url('/ui/SettingsGear.svg')"></span>
  </button>
  {#if toast}<div class="menu-toast">{toast}</div>{/if}
</div>

<style>
  .root {
    position: relative; min-height: 100cqh; width: 100%;
    overflow: hidden; display: flex; align-items: stretch; font-family: var(--font-body);
  }
  .bg {
    position: absolute; inset: 0;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
      linear-gradient(180deg, var(--bg-0) 0%, var(--bg-1) 40%, var(--bg-2) 100%);
    z-index: 0;
  }
  .keyart { position: absolute; inset: 0; z-index: 1; pointer-events: none; }
  .ember-glow {
    position: absolute; top: 28cqh; left: 50%; transform: translateX(-50%);
    width: 92cqw; height: 55cqh;
    background: radial-gradient(ellipse at 50% 50%, rgba(200,100,20,0.13) 0%, rgba(160,70,10,0.06) 45%, transparent 72%);
    z-index: 2; pointer-events: none;
  }
  .particles { position: absolute; inset: 0; z-index: 2; pointer-events: none; overflow: hidden; }
  .ember {
    position: absolute; border-radius: 50%;
    background: radial-gradient(circle, rgba(255,190,60,0.9) 0%, rgba(255,140,30,0.45) 55%, transparent 100%);
    filter: blur(1.5px); opacity: 0;
  }
  @keyframes ember-rise {
    0%   { transform: translateY(0) translateX(0);    opacity: 0;    }
    8%   { opacity: 0.30; }
    50%  { transform: translateY(-34cqh) translateX(7px);  opacity: 0.25; }
    88%  { opacity: 0.10; }
    100% { transform: translateY(-64cqh) translateX(-6px); opacity: 0;    }
  }
  @media (prefers-reduced-motion: no-preference) {
    .ember { animation-name: ember-rise; animation-timing-function: linear; animation-iteration-count: infinite; }
  }
  .col {
    position: relative; z-index: 3;
    display: flex; flex-direction: column; align-items: center;
    width: 100%; min-height: 100cqh;
    padding: max(env(safe-area-inset-top,0px), 5cqh) 0 env(safe-area-inset-bottom, 16px) 0;
  }
  .logo {
    width: min(340px, 88cqw); height: 80px;
    background: url('/ui/LogoArkanoid.png') no-repeat center / contain; flex-shrink: 0;
  }
  .cta-wrap { display: flex; flex-direction: column; align-items: center; gap: var(--sp-3h); margin: var(--sp-5) 0 var(--sp-4h); flex-shrink: 0; }
  .art-btn {
    position: relative; width: min(320px, 88cqw); background: none;
    border-style: solid; border-width: 9px 34px;
    border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
    cursor: pointer; display: flex; flex-direction: column; align-items: center; justify-content: center;
    transition: filter var(--dur-normal), transform var(--dur-fast);
    -webkit-tap-highlight-color: transparent; touch-action: manipulation;
  }
  .art-btn:hover  { filter: var(--filter-hover); }
  .art-btn:active { transform: scale(0.97); filter: brightness(0.9); }
  .art-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .btn-continue { height: 76px; gap: 2px; }
  .btn-kicker { color: var(--gold-bright); font-size: var(--fs-body); font-weight: 700; letter-spacing: 0.18em; text-transform: uppercase; text-shadow: 0 1px 3px rgba(0,0,0,0.9); }
  .btn-node   { color: var(--text); font-size: var(--fs-xl); font-weight: 800; letter-spacing: 0.04em; text-shadow: 0 1px 4px rgba(0,0,0,0.95), 0 0 10px rgba(255,180,60,0.4); }
  .btn-map    { height: 54px; }
  .btn-label  { color: var(--text); font-size: var(--fs-large); font-weight: 700; letter-spacing: 0.06em; text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 8px rgba(0,0,0,0.6); pointer-events: none; }
  /* Grouped dock (IA pass): Build / Hero / Events sections with labels. */
  .dock-groups {
    display: flex; flex-direction: column; gap: var(--sp-3h);
    width: min(440px, 96cqw); flex-shrink: 0;
    padding: var(--sp-2) 8px calc(env(safe-area-inset-bottom,0px) + 12px) 8px;
  }
  .dock-section { display: flex; flex-direction: column; align-items: center; gap: var(--sp-1h); }
  .dock-section-label {
    font-family: var(--font-display); font-weight: 800; letter-spacing: 0.22em; text-transform: uppercase;
    font-size: var(--fs-tiny); color: var(--gold); opacity: 0.9; text-align: center;
    text-shadow: 0 0 8px rgba(220,160,60,0.35);
  }
  .dock {
    display: flex; flex-wrap: wrap; justify-content: center; gap: var(--sp-2h);
    width: 100%;
  }
  .menu-coins {
    position: absolute; top: max(env(safe-area-inset-top,0px), 12px); left: 12px; z-index: 5;
  }
  .menu-settings {
    position: absolute; top: max(env(safe-area-inset-top,0px), 10px); right: 12px; z-index: 5;
    width: 46px; height: 46px; display: flex; align-items: center; justify-content: center;
    background: none; border-style: solid; border-width: 8px 14px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    border-radius: 8px; cursor: pointer; -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .menu-settings:hover  { filter: brightness(1.18); }
  .menu-settings:active { transform: scale(0.94); }
  .menu-settings:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 8px; }
  .dock-btn {
    position: relative;
    flex: 0 1 76px; display: flex; flex-direction: column; align-items: center;
    gap: var(--sp-1); min-width: 70px; min-height: 62px; padding: 6px 2px;
    background: none; border-style: solid; border-width: 8px 22px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    border-radius: 10px; cursor: pointer; touch-action: manipulation;
    -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .dock-btn:hover  { filter: brightness(1.18); }
  .dock-btn:active { transform: scale(0.94); }
  .dock-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  /* Campaign-gated features render locked until their milestone is cleared. */
  .dock-btn.locked { filter: grayscale(0.9) brightness(0.55); }
  .dock-btn.locked:hover { filter: grayscale(0.7) brightness(0.7); }
  .dock-lock { position: absolute; top: 2px; right: 6px; font-size: 13px; filter: none; }
  .menu-toast {
    position: fixed; left: 50%; top: 50%;
    transform: translate(-50%, -50%); z-index: 300; max-width: 86cqw;
    background: linear-gradient(180deg, rgba(50,30,12,.98), rgba(30,18,8,.99));
    border: 1px solid var(--gold,#ffce5a); border-radius: 12px;
    padding: 14px 20px; color: #ffe9bf; font-size: var(--fs-large); font-weight: 700;
    box-shadow: 0 6px 28px rgba(0,0,0,.7), 0 0 18px rgba(255,200,80,.25); text-align: center;
    animation: toast-in .25s ease-out;
  }
  @keyframes toast-in { from { opacity: 0; transform: translate(-50%, calc(-50% + 8px)); } to { opacity: 1; transform: translate(-50%, -50%); } }
  .dock-ico { width: 32px; height: 32px; background-repeat: no-repeat; background-position: center; background-size: contain; filter: drop-shadow(0 1px 2px rgba(0,0,0,0.7)); }
  .dock-label { color: var(--text-dim); font-size: var(--fs-tiny); font-weight: 600; letter-spacing: 0.03em; text-shadow: 0 1px 2px rgba(0,0,0,0.9); }

  /* Google Play badge — only rendered on Android */
  .play-badge {
    display: flex; align-items: center; gap: 10px;
    padding: 9px 18px; border-radius: 10px; text-decoration: none;
    background: rgba(0,0,0,0.55); border: 1px solid rgba(255,255,255,0.14);
    transition: border-color var(--dur-normal), background var(--dur-normal);
  }
  .play-badge:hover { background: rgba(255,255,255,0.07); border-color: rgba(255,255,255,0.28); }
  .play-icon { width: 22px; height: 22px; flex-shrink: 0; }
  .play-text { display: flex; flex-direction: column; line-height: 1.15; }
  .play-sub  { font-size: 0.65rem; color: rgba(255,255,255,0.55); letter-spacing: 0.04em; }
  .play-name { font-size: 0.92rem; font-weight: 700; color: #fff; letter-spacing: 0.01em; }
</style>
