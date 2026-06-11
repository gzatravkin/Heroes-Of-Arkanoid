import type { CompleteResult, FloorClearedResult } from "../../net/metaApi";

// ── Shared helpers ────────────────────────────────────────────────────────────

export function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

const RELIC_NAMES: Record<string, string> = {
  glass_cannon: "Glass Cannon",
  flint_core: "Flint Core",
  pyroclasm: "Pyroclasm",
  mana_battery: "Mana Battery",
  // G2 relic web
  conductor: "Conductor",
  overcharge: "Overcharge",
  split_shot: "Split Shot",
  souljar: "Souljar",
  lodestone: "Lodestone",
  ember_heart: "Ember Heart",
  second_wind: "Second Wind",
  midas: "Midas Touch",
  lead_paddle: "Lead Paddle",
  sapper: "Sapper's Charge",
  hellwalker: "Hellwalker",
  ghost_lens: "Ghost Lens",
  pillar_doctrine: "Pillar Doctrine",
};

const RELIC_ICONS: Record<string, string> = {
  glass_cannon: "/art/ItemHummer.png",
  flint_core: "/art/ItemDrill.png",
  pyroclasm: "/art/ItemTorch.png",
  mana_battery: "/art/ItemGem.png",
  // G2 relic web
  conductor: "/items/ItemMotor.png",
  overcharge: "/items/ItemOrb.png",
  split_shot: "/items/ItemJadeBall.png",
  souljar: "/items/ItemMark.png",
  lodestone: "/items/ItemForceRing.png",
  ember_heart: "/items/ItemPhoenix.png",
  second_wind: "/items/ItemHelm.png",
  midas: "/items/ItemMagicCrown.png",
  lead_paddle: "/items/ItemStaff.png",
  sapper: "/items/ItemSun.png",
  hellwalker: "/items/ItemFlask.png",
  ghost_lens: "/items/ItemRing.png",
  pillar_doctrine: "/items/ItemTomOfKnowladge.png",
};

const BALL_CORE_NAMES: Record<string, string> = {
  heavy: "Heavy Core",
  split: "Split Core",
  ember: "Ember Core",
  ghost: "Ghost Core",
  echo: "Echo Core",
  frost: "Frost Core",
  // Paddle mods — the fourth build axis (docs/04 §4.4)
  mod_wide: "Wide Frame",
  mod_grip: "Grip Tape",
  mod_cannons: "Side Cannons",
};

const BALL_CORE_ICONS: Record<string, string> = {
  heavy: "/ui/BonusRock.png",
  split: "/ui/BonusSplit.png",
  ember: "/ui/BonusFire.png",
  ghost: "/ui/BonusProtection.png",
  echo: "/ui/BonusRandomSpell.png",
  frost: "/ui/BonusMana.png",
  // Paddle mods
  mod_wide: "/ui/BonusLargerBita.png",
  mod_grip: "/ui/BonusLargerBall.png",
  mod_cannons: "/art/FireHeroTurret.png",
};

// Synergy web (docs/04 §7): an offered pick highlights when it combos with what
// you already hold. Pairs are symmetric; fusions are the strongest hints.
const SYNERGIES: Record<string, string[]> = {
  heavy:       ["ember"],            // Molten fusion
  ember:       ["heavy", "pyroclasm", "ember_heart"],
  ghost:       ["split", "ghost_lens"], // Phantom fusion
  split:       ["ghost", "split_shot"],
  echo:        ["frost"],            // Stasis fusion
  frost:       ["echo"],
  pyroclasm:   ["ember", "ember_heart"],
  ember_heart: ["ember", "pyroclasm"],
  ghost_lens:  ["ghost"],
  split_shot:  ["split"],
  flint_core:  ["pillar_doctrine"],
  pillar_doctrine: ["flint_core"],
  lodestone:   ["midas"],
  midas:       ["lodestone"],
};

/** The first owned id this choice combos with, or null. */
export function synergyWith(choiceId: string, owned: string[]): string | null {
  const partners = SYNERGIES[choiceId] ?? [];
  for (const p of partners) if (owned.includes(p)) return p;
  return null;
}

export function buffName(id: string): string {
  return RELIC_NAMES[id] ?? BALL_CORE_NAMES[id] ?? id;
}

export function buffIcon(id: string): string {
  return RELIC_ICONS[id] ?? BALL_CORE_ICONS[id] ?? "/art/ItemGem.png";
}

// Inject overlay styles once
function injectOverlayStyles() {
  const id = "overlay-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .ov-backdrop {
      position: absolute; inset: 0;
      background: rgba(0,0,0,0.82);
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      z-index: 1000;
      font-family: var(--font-body);
      color: #e8e8ff;
      gap: 0;
      padding: 20px;
      box-sizing: border-box;
    }

    /* Framed panel — LvlUpInterfacePanel art as background */
    .ov-panel {
      position: relative;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,26,0.95);
      border: 2px solid rgba(180,140,60,0.7);
      border-radius: 12px;
      padding: 24px 32px;
      min-width: min(280px, 88cqw);
      max-width: min(400px, 92cqw);
      display: flex;
      flex-direction: column;
      gap: 10px;
      align-items: center;
      box-shadow: 0 0 30px rgba(0,0,0,0.8), inset 0 0 40px rgba(10,5,30,0.5);
    }

    /* Top/bottom bar decorations */
    .ov-panel::before,
    .ov-panel::after {
      content: '';
      position: absolute;
      left: 0; right: 0;
      height: 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
    }
    .ov-panel::before { top: 0; border-radius: 10px 10px 0 0; }
    .ov-panel::after  { bottom: 0; border-radius: 0 0 10px 10px; }

    .ov-title {
      font-size: 2rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      text-shadow: 0 0 20px currentColor, 0 2px 4px rgba(0,0,0,0.9);
      margin-bottom: 8px;
    }
    .ov-title-win   { color: #ffd700; }
    .ov-title-green { color: #55ee88; }
    .ov-title-red   { color: #ff4444; }

    .ov-reward-row {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 1.1rem;
    }

    /* Art button — InterfaceButton pill, 9-sliced (fixed rounded ends + stretched middle) */
    .ov-btn {
      margin-top: 12px;
      padding: 0 16px;
      height: 52px;
      min-width: min(200px, 70cqw);
      background: none;
      border-style: solid;
      border-width: 8px 30px;
      border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
      cursor: pointer;
      font-size: 16px;
      font-family: var(--font-body);
      font-weight: 700;
      color: var(--text);
      letter-spacing: 0.05em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      transition: filter var(--dur-normal), transform var(--dur-fast);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .ov-btn:hover  { filter: brightness(1.15); }
    .ov-btn:active { transform: scale(0.97); filter: brightness(0.9); }
    .ov-btn:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 4px;
    }

    /* Chest image */
    .ov-chest {
      width: 80px;
      height: 80px;
      image-rendering: pixelated;
      filter: drop-shadow(0 4px 12px rgba(0,0,0,0.7));
      margin-bottom: 4px;
    }

    /* Bonus card */
    .ov-bonus-card {
      position: relative;
      width: min(110px, 28cqw);
      min-height: 140px;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,26,0.95);
      border: 2px solid rgba(100,80,160,0.5);
      border-radius: 10px;
      padding: 12px 8px 10px 8px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      cursor: pointer;
      transition: transform var(--dur-fast), border-color var(--dur-normal), box-shadow var(--dur-normal);
      -webkit-tap-highlight-color: transparent;
    }
    .ov-bonus-card:hover {
      transform: translateY(-4px) scale(1.04);
      border-color: rgba(220,190,80,0.8);
      box-shadow: 0 8px 20px rgba(0,0,0,0.6), 0 0 12px rgba(220,190,80,0.3);
    }
    .ov-bonus-card:active { transform: scale(0.97); }
    .ov-bonus-card:focus-visible {
      outline: 2px solid var(--gold-bright);
      outline-offset: 3px;
      border-radius: 10px;
    }

    .ov-bonus-icon {
      width: 52px;
      height: 52px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 4px rgba(0,0,0,0.7));
    }
    .ov-bonus-name {
      font-size: 11px;
      font-weight: 700;
      text-align: center;
      color: #e8d8b0;
      line-height: 1.3;
    }

    /* Synergy hint (docs/04 §7): the pick glows and names its combo partner */
    .ov-bonus-synergy {
      border-color: rgba(120, 220, 140, 0.85);
      box-shadow: 0 0 14px rgba(120, 220, 140, 0.35);
    }
    .ov-bonus-hint {
      font-size: 9px;
      font-weight: 700;
      text-align: center;
      color: #8fe3a0;
      line-height: 1.2;
      letter-spacing: 0.02em;
    }

    .ov-bonus-row {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      justify-content: center;
      max-width: min(400px, 96cqw);
    }

    .ov-pick-title {
      font-size: 1.3rem;
      font-weight: 700;
      color: #ffcc44;
      letter-spacing: 0.06em;
      margin-bottom: 12px;
      text-shadow: 0 0 12px rgba(255,200,50,0.5);
    }
  `;
  document.head.appendChild(style);
}

// ── Overlay builders ──────────────────────────────────────────────────────────

/** Campaign victory overlay (#reward-overlay). */
export function buildRewardOverlay(
  reward: CompleteResult["reward"],
  onContinue: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "reward-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Victory!";
  title.className = "ov-title ov-title-win";
  overlay.appendChild(title);

  // Chest art
  const chest = document.createElement("img");
  chest.src = "/ui/GreenChest.png";
  chest.className = "ov-chest";
  overlay.appendChild(chest);

  const panel = document.createElement("div");
  panel.className = "ov-panel";

  if (reward) {
    const expEl = document.createElement("div");
    expEl.id = "reward-exp";
    expEl.className = "ov-reward-row";
    const expIco = document.createElement("img");
    expIco.src = "/ui/ExpBarFull.png";
    css(expIco, { width: "28px", height: "12px", imageRendering: "pixelated" });
    expEl.appendChild(expIco);
    const expText = document.createElement("span");
    expText.textContent = `+${reward.expGained} EXP`;
    css(expText, { color: "#88aaff", fontSize: "1.1rem" });
    expEl.appendChild(expText);
    panel.appendChild(expEl);

    const pointsEl = document.createElement("div");
    pointsEl.id = "reward-points";
    pointsEl.className = "ov-reward-row";
    const ptIco = document.createElement("img");
    ptIco.src = "/ui/InterfaceSkillsButton.png";
    css(ptIco, { width: "22px", height: "22px", imageRendering: "pixelated" });
    pointsEl.appendChild(ptIco);
    const ptText = document.createElement("span");
    ptText.textContent = `+${reward.pointsGained} Skill Points`;
    css(ptText, { color: "#ffcc44", fontSize: "1.1rem" });
    pointsEl.appendChild(ptText);
    panel.appendChild(pointsEl);

    const crystalsEl = document.createElement("div");
    crystalsEl.id = "reward-crystals";
    crystalsEl.className = "ov-reward-row";
    const cIco = document.createElement("img");
    cIco.src = "/ui/GemBlue.png";
    css(cIco, { width: "22px", height: "22px", imageRendering: "pixelated" });
    crystalsEl.appendChild(cIco);
    const cText = document.createElement("span");
    cText.textContent = `+${reward.crystalsGained} Crystals`;
    css(cText, { color: "#44ddff", fontSize: "1.1rem" });
    crystalsEl.appendChild(cText);
    panel.appendChild(crystalsEl);

    if (reward.leveledUp) {
      const lvlUp = document.createElement("div");
      lvlUp.id = "reward-levelup";
      lvlUp.textContent = `Level Up! → Lv ${reward.newLevel}`;
      css(lvlUp, { fontSize: "1.1rem", color: "#ffd700", fontWeight: "700", marginTop: "4px" });
      panel.appendChild(lvlUp);
    }

    if (reward.firstClear) {
      const first = document.createElement("div");
      first.textContent = "First Clear!";
      css(first, { fontSize: "0.9rem", color: "#aa88ff", marginTop: "4px" });
      panel.appendChild(first);
    }
  } else {
    const msg = document.createElement("div");
    msg.textContent = "Level complete!";
    css(msg, { color: "#88aaff" });
    panel.appendChild(msg);
  }

  overlay.appendChild(panel);

  const btnContinue = document.createElement("button");
  btnContinue.id = "btn-continue";
  btnContinue.className = "ov-btn";
  btnContinue.textContent = "Continue";
  btnContinue.addEventListener("click", onContinue);
  overlay.appendChild(btnContinue);

  return overlay;
}

/** Campaign defeat overlay (#defeat-overlay). */
export function buildDefeatOverlay(
  onRetry: () => void,
  onMap: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "defeat-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Defeat";
  title.className = "ov-title ov-title-red";
  overlay.appendChild(title);

  const btnRetry = document.createElement("button");
  btnRetry.id = "btn-retry";
  btnRetry.className = "ov-btn";
  btnRetry.textContent = "Retry";
  css(btnRetry, { filter: "hue-rotate(200deg) saturate(0.7)" });
  btnRetry.addEventListener("click", onRetry);
  overlay.appendChild(btnRetry);

  const btnMap = document.createElement("button");
  btnMap.id = "btn-map";
  btnMap.className = "ov-btn";
  btnMap.textContent = "Map";
  btnMap.addEventListener("click", onMap);
  overlay.appendChild(btnMap);

  return overlay;
}

/** Dungeon pick-a-boon overlay (#pick-overlay). */
export function buildPickOverlay(
  choices: string[],
  onPick: (choiceId: string) => void,
  owned: string[] = [],
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "pick-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Choose a Boon";
  title.className = "ov-pick-title";
  overlay.appendChild(title);

  const row = document.createElement("div");
  row.className = "ov-bonus-row";

  for (const choiceId of choices) {
    const card = document.createElement("div");
    card.setAttribute("data-choice", choiceId);
    card.className = "ov-bonus-card";

    const icon = document.createElement("img");
    icon.src = buffIcon(choiceId);
    icon.alt = buffName(choiceId);
    icon.className = "ov-bonus-icon";
    card.appendChild(icon);

    const nameEl = document.createElement("div");
    nameEl.textContent = buffName(choiceId);
    nameEl.className = "ov-bonus-name";
    card.appendChild(nameEl);

    // Synergy hint (docs/04 §7): show when this pick combos with something owned.
    const partner = synergyWith(choiceId, owned);
    if (partner) {
      card.classList.add("ov-bonus-synergy");
      card.setAttribute("data-synergy", partner);
      const hint = document.createElement("div");
      hint.textContent = `Combos with ${buffName(partner)}`;
      hint.className = "ov-bonus-hint";
      card.appendChild(hint);
    }

    card.addEventListener("click", () => onPick(choiceId));
    row.appendChild(card);
  }

  overlay.appendChild(row);
  return overlay;
}

/** Dungeon all-floors-cleared overlay (#dungeon-clear-overlay). */
export function buildDungeonClearOverlay(
  data: FloorClearedResult,
  onDone: () => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "dungeon-clear-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Dungeon Cleared!";
  title.className = "ov-title ov-title-green";
  overlay.appendChild(title);

  const chest = document.createElement("img");
  chest.src = "/ui/BlueChest.png";
  chest.className = "ov-chest";
  overlay.appendChild(chest);

  const panel = document.createElement("div");
  panel.className = "ov-panel";

  const rewardTitle = document.createElement("div");
  rewardTitle.textContent = "Permanent Reward";
  css(rewardTitle, { fontSize: "0.85rem", color: "#88aaaa", letterSpacing: "0.05em" });
  panel.appendChild(rewardTitle);

  const profile = data.profile;
  if (profile?.crystals !== undefined) {
    const crystalEl = document.createElement("div");
    crystalEl.id = "dungeon-clear-crystals";
    crystalEl.className = "ov-reward-row";
    const gemImg = document.createElement("img");
    gemImg.src = "/ui/GemBlue.png";
    css(gemImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    crystalEl.appendChild(gemImg);
    const crystalText = document.createElement("span");
    crystalText.textContent = `${profile.crystals} Crystals`;
    css(crystalText, { fontSize: "1.2rem", color: "#44ddff" });
    crystalEl.appendChild(crystalText);
    panel.appendChild(crystalEl);
  }

  if (profile?.unlockedRelics && Array.isArray(profile.unlockedRelics) && profile.unlockedRelics.length > 0) {
    const lastRelic = profile.unlockedRelics[profile.unlockedRelics.length - 1];
    const relicRow = document.createElement("div");
    relicRow.className = "ov-reward-row";
    const relicImg = document.createElement("img");
    relicImg.src = RELIC_ICONS[lastRelic] ?? "/art/ItemGem.png";
    css(relicImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    relicRow.appendChild(relicImg);
    const relicText = document.createElement("span");
    relicText.id = "dungeon-clear-relic";
    relicText.textContent = RELIC_NAMES[lastRelic] ?? lastRelic;
    css(relicText, { fontSize: "1.1rem", color: "#cc88ff" });
    relicRow.appendChild(relicText);
    panel.appendChild(relicRow);
  }

  overlay.appendChild(panel);

  const doneBtn = document.createElement("button");
  doneBtn.id = "btn-dungeon-done";
  doneBtn.className = "ov-btn";
  doneBtn.textContent = "Return to Campaign";
  doneBtn.addEventListener("click", onDone);
  overlay.appendChild(doneBtn);

  return overlay;
}

/** Dungeon permadeath overlay (#dungeon-fail-overlay). */
export function buildDungeonFailOverlay(onExit: () => void): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "dungeon-fail-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Run Over";
  title.className = "ov-title ov-title-red";
  overlay.appendChild(title);

  const sub = document.createElement("div");
  sub.textContent = "The rift claims you.";
  css(sub, { color: "#aa5555", fontSize: "1.1rem", letterSpacing: "0.04em", marginBottom: "8px" });
  overlay.appendChild(sub);

  const exitBtn = document.createElement("button");
  exitBtn.id = "btn-dungeon-exit";
  exitBtn.className = "ov-btn";
  exitBtn.textContent = "Return to Campaign";
  css(exitBtn, { filter: "hue-rotate(200deg) saturate(0.7)" });
  exitBtn.addEventListener("click", onExit);
  overlay.appendChild(exitBtn);

  return overlay;
}
