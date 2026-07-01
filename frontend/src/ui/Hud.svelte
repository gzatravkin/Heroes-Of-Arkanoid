<script lang="ts">
  import type { Connection, Snapshot } from "../net/Connection";
  import type { SpellDef } from "../net/metaApi";
  import { buildSpellIcon } from "./hud/spellIcon";
  import { inferBossType, bossLabel } from "../render/Boss";

  const SLOT_KEYS = ["Q", "E", "W", "R", "T"];

  // BAR geometry (matches bars.ts constants):
  //   BAR_H=22, sprite h=41, cap_x=16, cap_y=7
  //   scaled cap_x = round(16*22/41) = 9px, cap_y = 4px
  //   inner fill width for 118px bar = 118-18 = 100px
  const BAR_H  = 22;
  const CAP_X  = 9;
  const CAP_Y  = 4;
  const FILL_W = 100; // 118 - 2*9

  let { snap, spells, conn, level = "" }: {
    snap: Snapshot | null;
    spells: SpellDef[];
    conn: Connection | null;
    level?: string;
  } = $props();

  // Running maxima (plain vars — not reactive, no $state needed)
  let _maxLives = 1;
  let _maxBalls = 1;
  let _maxBlocks = 0;

  // Level label for the status bar (e.g. "hell-5" → "Hell 5", "village-boss" → "Witchland Boss").
  const BIOME_NAME: Record<string, string> = { hell: "Hell", caverns: "Caverns", village: "Witchland", heaven: "Heaven" };
  let levelLabel = $derived.by(() => {
    if (!level) return "";
    const [b, ...rest] = level.split("-");
    const name = BIOME_NAME[b] ?? (b ? b[0].toUpperCase() + b.slice(1) : "");
    const tail = rest.join("-");
    return tail === "boss" ? `${name} Boss` : `${name} ${tail}`.trim();
  });
  // Blocks-left clear-progress (fills the status-bar centre on normal levels). Destructible = must-kill.
  let blocksLeft = $derived.by(() => {
    const n = (snap?.blocks ?? []).filter(b => !b.indestructible && !b.boss).length;
    _maxBlocks = Math.max(_maxBlocks, n);
    return n;
  });
  // Bar reads as the label says — "X left": starts FULL and DRAINS toward Clear! as blocks die.
  let blocksPct = $derived(_maxBlocks > 0 ? (blocksLeft / _maxBlocks) * 100 : 100);

  // ── Derived values ─────────────────────────────────────────────────────────
  let livesPct = $derived.by(() => {
    const v = snap?.hp ?? 0;
    _maxLives = Math.max(_maxLives, v);
    return _maxLives > 0 ? (v / _maxLives) * 100 : 0;
  });
  // HP bar swaps sprite by danger level: green (full) → amber (medium) → red (low).
  let hpFillSprite = $derived(
    livesPct > 60 ? "BattleFullHP" : livesPct > 30 ? "BattleMediumHP" : "BattleLowHP"
  );
  let ballsPct = $derived.by(() => {
    const v = snap?.spareBalls ?? 0;
    _maxBalls = Math.max(_maxBalls, v);
    return _maxBalls > 0 ? (v / _maxBalls) * 100 : 0;
  });
  let manaPct = $derived(snap ? Math.min(100, (snap.mana / (snap.manaMax || 1)) * 100) : 0);
  let bossHpPct = $derived(
    snap?.bossActive && snap.bossMaxHp > 0
      ? Math.min(100, (snap.bossHp / snap.bossMaxHp) * 100)
      : 0
  );
  let bossName = $derived.by(() => {
    if (!snap?.bossActive) return "";
    const bb = snap.blocks?.find(b => b.boss);
    return bossLabel(bb ? inferBossType(bb.sprite) : "Unknown");
  });
  let bossGradient = $derived(
    bossHpPct < 33
      ? "linear-gradient(to right,var(--color-boss-deep),var(--danger-bright))"
      : bossHpPct < 66
        ? "linear-gradient(to right,var(--color-boss-deep),var(--color-fire))"
        : "linear-gradient(to right,var(--color-boss-deep),var(--color-boss-hp))"
  );

  // Timer display
  let timerText = $derived.by(() => {
    if (!snap) return null;
    if (snap.timerMode && (snap.timeLeft ?? 0) >= 0 && snap.phase === "Playing") {
      const t = Math.ceil(snap.timeLeft ?? 0);
      const mm = Math.floor(t / 60), ss = (t % 60).toString().padStart(2, "0");
      return { text: snap.timerMode === "survive" ? `SURVIVE ${mm}:${ss}` : `TIME ${mm}:${ss}`, urgent: snap.timerMode !== "survive" && t <= 10 };
    }
    if ((snap.floorCount ?? 1) > 1 && snap.phase === "Playing") {
      return { text: `FLOOR ${snap.floor}/${snap.floorCount}`, urgent: false };
    }
    return null;
  });

  // Effects chips (wide paddle + slow ball)
  let effectChips = $derived.by(() => {
    if (!snap) return [];
    const chips: string[] = [];
    if (snap.widePaddleActive) chips.push(`↔ ${Math.ceil(snap.widePaddleTimer ?? 0)}s`);
    if (snap.slowBallActive)   chips.push(`slow ${Math.ceil(snap.slowBallTimer ?? 0)}s`);
    return chips;
  });

  // Active powerup indicators
  let powerups = $derived.by(() => {
    if (!snap) return [];
    const active: { label: string; color: string; timer?: number }[] = [];
    if (snap.widePaddleActive) active.push({ label: "W", color: "var(--color-wide)",  timer: snap.widePaddleTimer });
    if (snap.fireshotActive)   active.push({ label: "F", color: "var(--color-fire)",  timer: snap.fireshotTimer });
    if (snap.shieldActive)     active.push({ label: "◆", color: "var(--color-shield)" });
    return active;
  });

  // Combo badge
  let comboMult = $derived(snap?.comboMultiplier ?? 1);
  // Reckoning (§3 Paladin): 0..1 meter fill — only shown once the spell is armed (charge > 0 once HP lost).
  let reckoningPct = $derived(Math.round((snap?.reckoningCharge ?? 0) * 100));
  // In-run Gold (docs/04 §5) — shown only when the player has some, so it stays out of the way
  // until a shop floor makes it matter.
  let runGold = $derived(snap?.gold ?? 0);
  let comboEl: HTMLElement | undefined = $state();
  let prevCombo = 1;
  $effect(() => {
    const c = comboMult;
    if (comboEl && c > 1 && c !== prevCombo) {
      comboEl.classList.remove("combo-pop");
      void comboEl.offsetWidth;
      comboEl.classList.add("combo-pop");
    }
    prevCombo = c;
  });

  // Spell-ready flash: when mana rises past a spell's cost, flash its slot so the
  // player notices the newly-castable spell.
  let _prevAfford: boolean[] = [];
  $effect(() => {
    const mana = snap?.mana ?? 0;
    spells.forEach((sp, i) => {
      const aff = mana >= (sp.manaCost ?? 0);
      if (aff && _prevAfford[i] === false) {
        const el = document.getElementById(`hud-spell-${sp.id}`);
        if (el) { el.classList.remove("spell-ready"); void el.offsetWidth; el.classList.add("spell-ready"); }
      }
      _prevAfford[i] = aff;
    });
  });

  // Relics — only re-render when ids change
  let relicIds = $derived((snap?.activeRelics ?? []).map(r => r.id).join(","));

  // ── Keyboard handler ──────────────────────────────────────────────────────
  function onKeydown(e: KeyboardEvent) {
    if (e.repeat || !conn) return;
    const keyMap: Record<string, number> = { q: 0, e: 1, w: 2, r: 3 };
    const idx = keyMap[e.key.toLowerCase()];
    if (idx === undefined || idx >= spells.length) return;
    const mana = snap?.mana ?? 0;
    const cost = spells[idx].manaCost ?? 0;
    if (mana >= cost) conn.castSlot(idx);
  }

  function castSpell(e: Event, idx: number) {
    e.stopPropagation();
    if (!conn) return;
    const spell = spells[idx];
    if (!spell) return;
    const mana      = snap?.mana ?? 0;
    const cost      = spell.manaCost ?? 0;
    const needsFire = spell.id === "fireball" && (snap?.burningBlockCount ?? 0) === 0;
    if (mana >= cost && !needsFire) conn.castSlot(idx);
  }

  // ── Spell icon action ─────────────────────────────────────────────────────
  function spellIconAction(node: HTMLElement, spell: SpellDef) {
    buildSpellIcon(node, spell);
    return {
      update(s: SpellDef) { node.innerHTML = ""; buildSpellIcon(node, s); }
    };
  }
</script>

<svelte:window onkeydown={onKeydown} />

<div class="hud-root">

  <!-- ── Top status bar (Level-UX rework 2026-06-15): vitals · level+clear-progress / boss · relics ── -->
  <div class="hud-bezel">

    <!-- Vitals: HP + balls, side by side -->
    <div class="hud-vitals">
      <div id="hud-lives" class="bar-outer" data-lives={snap?.hp ?? 0}>
        <div class="bar-track" style="border-image:url('/ui/BattleHPEmpty.png') 7 16 7 16 fill stretch"></div>
        <div class="bar-clip">
          <div id="hud-lives-fill" class="bar-fill fill-sprite"
               style="background-image:url('/ui/{hpFillSprite}.png');background-size:{FILL_W}px 100%;width:{livesPct}%"></div>
        </div>
        <span id="hud-lives-label" class="bar-label">
          <img src="/ui/BonusHP.png" alt="" class="bar-icon" />
          <span class="hud-bar-count">{snap?.hp ?? 0}</span>
        </span>
      </div>

      <div id="hud-balls" class="bar-outer" data-balls={snap?.spareBalls ?? 0}>
        <div class="bar-track" style="border-image:url('/ui/BattleMPEmpty.png') 7 16 7 16 fill stretch"></div>
        <div class="bar-clip">
          <div id="hud-balls-fill" class="bar-fill fill-sprite"
               style="background-image:url('/ui/BattleMPFull.png');background-size:{FILL_W}px 100%;width:{ballsPct}%"></div>
        </div>
        <span id="hud-balls-label" class="bar-label">
          <img src="/ui/BattleLifeBall.png" alt="" class="bar-icon" />
          <span class="hud-bar-count">{snap?.spareBalls ?? 0}</span>
        </span>
      </div>

      {#if runGold > 0}
        <div id="hud-gold" class="hud-gold">
          <img src="/ui/BonusGem.png" alt="Gold" class="hud-gold-icon" />
          <span class="hud-gold-count">{runGold}</span>
        </div>
      {/if}
    </div>

    <!-- Centre: boss HP on boss levels; otherwise level name + blocks-left clear progress -->
    <div class="hud-center">
      {#if snap?.bossActive}
        <div id="hud-boss-name" class="boss-name">{bossName}</div>
        <div id="hud-boss-bar" class="boss-bar-wrap">
          <div class="bar-track bar-track-boss" style="border-image:url('/ui/BattleHPEmpty.png') 7 16 7 16 fill stretch"></div>
          <div class="bar-clip bar-clip-boss">
            <div id="hud-boss-hp-fill" class="bar-fill" style="background:{bossGradient};width:{bossHpPct}%"></div>
          </div>
        </div>
      {:else if snap?.isRift}
        <div class="hud-level">RIFT · Floor {snap.floor}/{snap.floorCount}</div>
        <div class="rift-reward">
          <img src="/ui/GemBlue.png" alt="" class="rift-gem" />
          <span class="rift-reward-val">{snap.riftReward ?? 0}</span>
          <span class="rift-reward-lbl">banked{(snap.riftNextMilestone ?? 0) > 0 ? ` · bump @ ${snap.riftNextMilestone}` : ` · ★ jackpot`}</span>
        </div>
      {:else}
        <div class="hud-level">{levelLabel}</div>
        <div class="blocks-bar" title="Blocks left: {blocksLeft}">
          <div class="blocks-fill" style="width:{blocksPct}%"></div>
          <span class="blocks-text">{blocksLeft > 0 ? `${blocksLeft} left` : "Clear!"}</span>
        </div>
      {/if}
    </div>

    <!-- Right: relics -->
    <div id="hud-relics" class="hud-relics">
      {#key relicIds}
        {#each snap?.activeRelics ?? [] as relic}
          <div data-relic-id={relic.id} class="relic-tile" title={relic.name}>
            <img src="/art/{relic.icon}.png" alt={relic.name} class="relic-img"
                 onerror={e => { const t = e.target as HTMLImageElement; t.style.display = "none"; if (t.parentElement) t.parentElement.textContent = "?"; }} />
          </div>
        {/each}
      {/key}
    </div>
  </div>

  <!-- ── Timer ── -->
  {#if timerText}
    <div id="hud-timer" class="hud-timer" style="color:{timerText.urgent ? 'var(--danger-bright)' : 'var(--gold)'}">
      {timerText.text}
    </div>
  {/if}

  <!-- ── Dungeon miniboss floor banner ── -->
  {#if snap?.minibossFloor}
    <div id="hud-miniboss" class="hud-miniboss">⚔ Miniboss Floor</div>
  {/if}

  <!-- ── Effects row (top-left below bars) ── -->
  <div id="hud-effects" class="hud-effects">
    {#each effectChips as chip}
      <span class="effect-chip">{chip}</span>
    {/each}
  </div>

  <!-- ── Powerup panel (top-right) ── -->
  <div id="hud-powerups" class="hud-powerups" style="display:{powerups.length > 0 ? 'flex' : 'none'}">
    {#each powerups as pu}
      <div class="powerup-active" style="border-color:{pu.color};color:{pu.color}">
        {pu.label}{pu.timer !== undefined ? ` ${Math.ceil(pu.timer)}s` : ""}
      </div>
    {/each}
  </div>

  <!-- ── Combo badge ── -->
  <div id="hud-combo" class="hud-combo" bind:this={comboEl}
       style="display:{comboMult > 1 ? 'block' : 'none'}">
    ×{comboMult}
  </div>

  <!-- ── Banner ── -->
  <div id="hud-banner" class="hud-banner {snap?.phase === 'Won' ? 'win' : snap?.phase === 'Lost' ? 'lose' : ''}"
       style="display:{snap?.phase === 'Won' || snap?.phase === 'Lost' ? 'block' : 'none'}">
    {snap?.phase === "Won" ? "VICTORY" : snap?.phase === "Lost" ? "DEFEAT" : ""}
  </div>

  <!-- ── Bottom: mana bar + hotbar ── -->
  <div class="hud-bottom">

    <!-- Mana bar -->
    <div id="hud-mana" class="mana-outer">
      <div class="bar-track bar-track-mana" style="border-image:url('/ui/BattleMPEmpty.png') 7 16 7 16 fill stretch"></div>
      <div class="bar-clip bar-clip-mana">
        <div id="hud-mana-fill" class="bar-fill fill-sprite"
             style="background-image:url('/ui/BattleMPFull.png');background-size:calc(min(220px,80cqw) - 18px) 100%;width:{manaPct}%"></div>
      </div>
      <span id="hud-mana-text" class="mana-text">
        {Math.round(snap?.mana ?? 0)} / {Math.round(snap?.manaMax ?? 0)}
      </span>
    </div>

    <!-- Tesla Grid (§3 Engineer): L/R wall charge icons visible while spell is armed -->
    {#if snap?.teslaArmed}
      <div id="hud-tesla" class="tesla-indicators" title="Tesla Grid — hit L+R walls to fire curtain">
        <div id="hud-tesla-left"  class="tesla-wall {snap.teslaLeftCharged  ? 'charged' : ''}">L</div>
        <div class="tesla-separator">⚡</div>
        <div id="hud-tesla-right" class="tesla-wall {snap.teslaRightCharged ? 'charged' : ''}">R</div>
      </div>
    {/if}

    <!-- Reckoning charge meter (§3): visible only while Reckoning is armed -->
    {#if reckoningPct > 0}
      <div id="hud-reckoning" class="reckoning-outer" title="Reckoning — charges as you lose HP">
        <div class="reckoning-track"></div>
        <div id="hud-reckoning-fill" class="reckoning-fill" style="width:{reckoningPct}%"></div>
        <span class="reckoning-label">⚔ {reckoningPct}%</span>
      </div>
    {/if}

    <!-- Spell hotbar -->
    <div id="hud-hotbar" class="hud-hotbar">
      {#each spells as spell, i}
        {@const mana = snap?.mana ?? 0}
        {@const cost = spell.manaCost ?? 0}
        {@const affordable = mana >= cost}
        {@const needsFire = spell.id === "fireball" && (snap?.burningBlockCount ?? 0) === 0}
        {@const castable = affordable && !needsFire}
        <div id="hud-spell-{spell.id}"
             class="hud-spell-slot {castable ? 'affordable' : 'unaffordable'} {needsFire ? 'needs-fire' : ''}"
             role="button" tabindex="0"
             aria-label="Cast {spell.name} — key {SLOT_KEYS[i] ?? String(i+1)}{cost > 0 ? `, costs ${cost} mana` : ''}{ needsFire ? ' (ignite blocks first)' : ''}"
             aria-disabled={castable ? "false" : "true"}
             onpointerdown={e => castSpell(e, i)}
             onkeydown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); castSpell(e, i); } }}>
          <div class="hud-spell-frame">
            <div class="hud-spell-key">{SLOT_KEYS[i] ?? i+1}</div>
            <div class="hud-spell-icon" use:spellIconAction={spell}></div>
            {#if cost > 0}
              <div class="hud-spell-cost">{cost}</div>
            {/if}
            {#if needsFire}
              <div class="hud-needs-fire-badge" title="Ignite blocks first">🔥</div>
            {/if}
          </div>
          <div class="hud-spell-name">{spell.name}</div>
        </div>
      {/each}
    </div>

  </div>

</div>

<style>
  .hud-root {
    position: absolute; inset: 0; pointer-events: none;
    font-family: var(--font-body); z-index: 10; user-select: none;
    padding: env(safe-area-inset-top,0px) env(safe-area-inset-right,0px)
             env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px);
  }

  /* Top bezel: a stone HUD strip above the playfield (matches the arena wall frame). Must match
     Renderer HUD_TOP_INSET so the walled board starts right below it. */
  .hud-bezel {
    position: absolute; top: 0; left: 0; right: 0; height: 54px;
    display: flex; align-items: center; gap: 10px; padding: 0 10px; box-sizing: border-box;
    background: linear-gradient(180deg, #1a1109 0%, #120b06 70%, #0c0804 100%);
    border-bottom: 2px solid var(--gold, #d8a84e);
    box-shadow: 0 2px 10px rgba(0,0,0,0.6), inset 0 -6px 14px rgba(0,0,0,0.5);
  }

  /* ── Vitals: HP + balls side by side ── */
  .hud-vitals { display: flex; flex-direction: row; align-items: center; gap: 6px; flex-shrink: 0; }

  /* ── Centre: level name + blocks-left progress (or boss HP on boss levels) ── */
  .hud-center {
    flex: 1; min-width: 0; display: flex; flex-direction: column;
    align-items: center; justify-content: center; gap: 3px; pointer-events: none;
  }
  .hud-level {
    font-family: var(--font-display); font-size: var(--fs-caption); font-weight: 800;
    color: var(--gold-bright); letter-spacing: 0.06em; line-height: 1;
    text-shadow: 0 1px 2px rgba(0,0,0,0.9); white-space: nowrap;
    overflow: hidden; text-overflow: ellipsis; max-width: 100%;
  }
  .blocks-bar {
    position: relative; width: min(220px, 100%); height: 13px; border-radius: 7px;
    background: rgba(0,0,0,0.5); border: 1px solid var(--gold-dim); overflow: hidden;
  }
  .blocks-fill {
    position: absolute; left: 0; top: 0; bottom: 0;
    background: linear-gradient(90deg, #ffe06a, #d89a2e); transition: width var(--dur-normal);
  }
  .blocks-text {
    position: relative; z-index: 1; display: block; text-align: center;
    font-size: var(--fs-tiny); font-weight: 800; line-height: 13px; color: var(--text);
    text-shadow: 0 1px 2px rgba(0,0,0,0.95);
  }

  /* ── Rift depth-reward chip (continuous rift) ── */
  .rift-reward { display: flex; align-items: center; gap: 4px; line-height: 1; }
  .rift-gem { width: 13px; height: 13px; object-fit: contain; image-rendering: pixelated; }
  .rift-reward-val { font-size: var(--fs-tiny); font-weight: 800; color: var(--color-crystal, #7fd0ff); text-shadow: 0 1px 2px var(--shadow-black); }
  .rift-reward-lbl { font-size: var(--fs-tiny); color: var(--text-dim); text-shadow: 0 1px 2px var(--shadow-black); white-space: nowrap; }

  /* ── Value bars (compact for the slim status bar) ── */
  .bar-outer { position: relative; width: 90px; height: 24px; }
  .bar-track {
    position: absolute; inset: 0; box-sizing: border-box;
    border-style: solid; border-width: 4px 9px;
  }
  .bar-clip {
    position: absolute; left: 9px; right: 9px; top: 4px; bottom: 4px;
    overflow: hidden; border-radius: 2px;
  }
  .bar-fill {
    position: absolute; left: 0; top: 0; bottom: 0; width: 100%;
    transition: width var(--dur-normal) linear;
  }
  .fill-sprite {
    background-position: left center; background-repeat: no-repeat;
  }
  .bar-label {
    position: absolute; top: 50%; left: 8px; transform: translateY(-50%);
    display: flex; align-items: center; gap: 4px;
    font-size: var(--fs-body); color: var(--text-oncolor); font-weight: 800;
    text-shadow: 0 0 4px var(--shadow-black), 0 1px 2px var(--shadow-black);
    pointer-events: none; white-space: nowrap;
  }
  .bar-icon { width: 16px; height: 16px; object-fit: contain; image-rendering: pixelated; }

  /* ── In-run Gold (docs/04 §5) ── */
  .hud-gold {
    display: flex; align-items: center; gap: 4px; padding: 1px 6px;
    font-size: var(--fs-small); font-weight: 700;
    color: var(--gold-bright);
    text-shadow: 0 0 4px var(--shadow-black), 0 1px 2px var(--shadow-black);
  }
  /* The gem sprite is grayscale — warm it to gold so it reads as currency. */
  .hud-gold-icon { width: 14px; height: 14px; object-fit: contain;
    filter: sepia(1) saturate(4) hue-rotate(-12deg) brightness(1.05); }

  /* ── Relics (right of the status bar) ── */
  .hud-relics {
    display: flex; flex-direction: row; gap: 5px; flex-shrink: 0;
    align-items: center; justify-content: flex-end; min-width: 34px;
  }
  .relic-tile {
    width: 32px; height: 32px;
    background: url('/ui/BattleSpellBarActive.png') no-repeat center / cover;
    display: flex; align-items: center; justify-content: center;
    pointer-events: none; cursor: default;
  }
  .relic-img { width: 20px; height: 20px; object-fit: contain; image-rendering: pixelated; }

  /* ── Boss bar (in the status-bar centre) ── */
  .boss-name {
    font-size: var(--fs-tiny); font-weight: 900; color: var(--danger-bright);
    letter-spacing: 2px; text-shadow: 0 0 6px var(--danger-bright), 0 1px 3px var(--shadow-black);
    text-align: center; white-space: nowrap;
  }
  .boss-bar-wrap { position: relative; width: min(220px, 100%); height: 16px; }
  .bar-track-boss {
    border-width: 3px 7px;
  }
  .bar-clip-boss { left: 7px; right: 7px; top: 3px; bottom: 3px; }

  /* ── Timer ── */
  .hud-timer {
    position: absolute; top: 44px; left: 50%; transform: translateX(-50%);
    padding: 2px 14px; border-radius: 10px;
    font-size: var(--fs-xl); font-weight: 800; letter-spacing: 1px;
    background: var(--overlay-light); pointer-events: none;
  }

  /* ── Dungeon miniboss floor banner ── */
  .hud-miniboss {
    position: absolute; top: 72px; left: 50%; transform: translateX(-50%);
    padding: 3px 16px; border-radius: 10px; white-space: nowrap;
    font-family: var(--font-display); font-size: var(--fs-body); font-weight: 800;
    letter-spacing: 0.08em; color: #ffd0d0; pointer-events: none;
    background: rgba(120,20,20,0.78); border: 1px solid var(--danger-bright, #e44);
    text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 10px rgba(255,60,60,0.4);
  }

  /* ── Effects row ── */
  .hud-effects {
    position: absolute; top: 72px; left: 8px;
    display: flex; flex-direction: row; gap: 4px;
    align-items: center; pointer-events: none;
  }
  .effect-chip {
    background: var(--overlay-mid); border: 1px solid var(--color-effect);
    border-radius: 4px; padding: 1px 5px;
    font-size: var(--fs-tiny); color: var(--color-shield);
  }

  /* ── Powerup panel ── */
  .hud-powerups {
    position: absolute; top: 90px; right: 8px;
    flex-direction: column; gap: 3px; align-items: flex-end; pointer-events: none;
  }
  .powerup-active {
    background: var(--overlay-mid); border: 1px solid;
    border-radius: 5px; padding: 2px 7px;
    font-size: var(--fs-small); font-weight: 700; letter-spacing: .5px;
  }

  /* ── Combo badge ── */
  .hud-combo {
    position: absolute; top: 155px; right: 8px;
    color: var(--gold-bright); text-shadow: 0 0 8px var(--gold-glow-mid);
    font-family: var(--font-display); font-size: var(--fs-xl); font-weight: bold;
    background: var(--overlay-mid); border: 1px solid var(--gold-glow-lo);
    border-radius: 6px; padding: 3px 10px; pointer-events: none;
  }

  /* ── Banner ── */
  .hud-banner {
    position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);
    padding: 18px 48px; border-radius: 8px;
    font-size: clamp(28px,8cqw,48px); font-weight: 900; letter-spacing: 4px;
    text-shadow: 0 0 20px currentColor;
  }
  .hud-banner.win {
    background: var(--hud-win-bg); border: 2px solid var(--ok-bright); color: var(--ok-bright);
  }
  .hud-banner.lose {
    background: var(--hud-lose-bg); border: 2px solid var(--danger-bright); color: var(--danger-bright);
  }

  /* ── Bottom zone ── */
  /* A stone HUD strip mirroring .hud-bezel, holding the mana bar + spell hotbar. Its min-height
     matches Renderer HUD_BOTTOM_INSET so the walled playfield ends right above this band — the mana
     bar no longer floats in the dead space below the arena frame / over the pit (battle HUD fix). */
  .hud-bottom {
    position: absolute; bottom: 0; left: 0; right: 0;
    box-sizing: border-box; min-height: 124px;
    display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
    padding-bottom: max(12px, env(safe-area-inset-bottom,12px));
    padding-top: 8px;
    background: linear-gradient(0deg, #1a1109 0%, #120b06 70%, #0c0804 100%);
    border-top: 2px solid var(--gold, #d8a84e);
    box-shadow: 0 -2px 10px rgba(0,0,0,0.6), inset 0 6px 14px rgba(0,0,0,0.5);
    pointer-events: none;
  }

  /* Mana bar */
  .mana-outer { position: relative; width: min(220px,80cqw); height: 22px; }
  .bar-track-mana { border-width: 4px 9px; }
  .bar-clip-mana { left: 9px; right: 9px; top: 4px; bottom: 4px; }
  .mana-text {
    position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);
    font-size: var(--fs-tiny); color: var(--text-oncolor); font-weight: 600;
    text-shadow: 0 1px 2px var(--shadow-black); pointer-events: none;
    white-space: nowrap; z-index: 1;
  }

  /* ── Tesla Grid wall-charge indicators (§3 Engineer) ── */
  .tesla-indicators {
    display: flex; align-items: center; gap: 6px; margin-top: 4px;
    font-size: 11px; font-weight: 700; letter-spacing: 1px;
  }
  .tesla-wall {
    display: flex; align-items: center; justify-content: center;
    width: 24px; height: 24px; border-radius: 4px;
    border: 2px solid rgba(90, 180, 255, 0.4);
    background: rgba(20, 40, 80, 0.7);
    color: rgba(100, 170, 255, 0.5);
    text-shadow: none;
    transition: all 150ms ease;
  }
  .tesla-wall.charged {
    border-color: #5abfff;
    background: rgba(20, 80, 180, 0.85);
    color: #c8eeff;
    text-shadow: 0 0 8px #5abfff;
    box-shadow: 0 0 10px rgba(90, 190, 255, 0.5);
  }
  .tesla-separator {
    font-size: 14px; color: rgba(100, 170, 255, 0.5); line-height: 1;
  }

  /* ── Reckoning charge meter (§3) ── */
  .reckoning-outer {
    position: relative; width: min(220px,80cqw); height: 12px; margin-top: 4px;
    border-radius: 6px; overflow: hidden; box-shadow: 0 0 0 1px var(--shadow-black);
  }
  .reckoning-track { position: absolute; inset: 0; background: rgba(0,0,0,0.55); }
  .reckoning-fill {
    position: absolute; inset: 0 auto 0 0; height: 100%;
    background: linear-gradient(90deg, #c2402a, #ff7a3c);
    box-shadow: 0 0 6px rgba(255,120,60,0.6); transition: width 120ms linear;
  }
  .reckoning-label {
    position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%);
    font-size: 9px; font-weight: 700; color: #ffe6c8;
    text-shadow: 0 1px 2px var(--shadow-black); pointer-events: none; white-space: nowrap; z-index: 1;
  }

  /* ── Hotbar ── */
  .hud-hotbar { display: flex; gap: 6px; pointer-events: none; }

  .hud-spell-slot {
    display: flex; flex-direction: column; align-items: center; gap: 3px;
    min-width: 60px; touch-action: manipulation; cursor: pointer;
    pointer-events: auto;
    transition: filter var(--dur-normal), transform var(--dur-normal);
    -webkit-tap-highlight-color: transparent;
  }
  .hud-spell-frame {
    position: relative; width: 52px; height: 52px; box-sizing: border-box;
    display: flex; align-items: center; justify-content: center;
    background: none; border-style: solid; border-width: 7px;
    border-image: url('/ui/Kvadrat.png') 14 14 14 14 fill stretch;
  }
  .hud-spell-slot.affordable { filter: drop-shadow(0 0 6px var(--gold-glow-lo)); }
  .hud-spell-slot.affordable:hover { filter: drop-shadow(0 0 8px var(--gold-glow-mid)) brightness(1.15); }
  .hud-spell-slot.unaffordable { filter: saturate(.3) brightness(.6); cursor: default; }
  .hud-spell-slot.needs-fire { filter: sepia(.7) hue-rotate(-15deg) brightness(.65); cursor: default; }
  .hud-spell-slot:active { transform: scale(1.06); }
  .hud-spell-slot.affordable:active { filter: drop-shadow(0 0 12px var(--gold-glow-hi)); transform: scale(1.06); }
  .hud-spell-slot:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .hud-spell-key {
    position: absolute; top: 2px; left: 3px;
    font-size: var(--fs-tiny); font-weight: 700; color: var(--gold);
    line-height: 1; text-shadow: 0 1px 2px var(--shadow-hard); pointer-events: none; z-index: 1;
  }
  .hud-spell-cost {
    position: absolute; bottom: 2px; right: 3px;
    font-size: var(--fs-tiny); font-weight: 700; color: var(--gold);
    line-height: 1; text-shadow: 0 1px 2px var(--shadow-hard); pointer-events: none; z-index: 1;
  }
  .hud-needs-fire-badge {
    position: absolute; top: 1px; right: 2px;
    font-size: 9px; pointer-events: none; z-index: 2; line-height: 1;
  }
  .hud-spell-icon {
    font-size: var(--fs-xl); line-height: 1;
    display: flex; align-items: center; justify-content: center;
    width: 32px; height: 32px;
  }
  .hud-spell-name {
    font-size: var(--fs-tiny); color: var(--text-dim); text-align: center; line-height: 1.05;
    text-shadow: 0 1px 2px var(--shadow-hard);
    width: 64px; min-height: 2.1em; overflow-wrap: anywhere;
    display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
  }

  /* Combo pop animation */
  @keyframes combo-pop {
    0%   { transform: scale(0.7); }
    60%  { transform: scale(1.1); }
    100% { transform: scale(1.0); }
  }
  :global(#hud-combo.combo-pop) {
    animation: combo-pop 0.2s ease-out forwards;
  }

  /* Spell-ready flash: a slot pops + glows when it first becomes castable. */
  @keyframes spell-ready {
    0%   { transform: scale(1);    filter: drop-shadow(0 0 5px var(--gold-glow-lo)) brightness(1); }
    45%  { transform: scale(1.13); filter: drop-shadow(0 0 14px var(--gold-glow-hi)) brightness(1.5); }
    100% { transform: scale(1);    filter: drop-shadow(0 0 6px var(--gold-glow-lo)) brightness(1); }
  }
  :global(.hud-spell-slot.spell-ready) {
    animation: spell-ready 0.45s ease-out;
  }

  /* Landscape: shrink hotbar a bit */
  @media (orientation: landscape) and (max-height: 500px) {
    .hud-spell-frame { width: 44px; height: 44px; border-width: 6px; }
    .hud-spell-icon { width: 26px; height: 26px; }
  }
</style>
