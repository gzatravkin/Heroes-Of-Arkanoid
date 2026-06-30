import type { CompleteResult, FloorClearedResult, RiftFinishResult, ShopItem, ShopItemsResult, ShopBuyResult } from "../../net/metaApi";
import { getRelicIcon, getRelicName, getRelicDesc } from "../../net/relicCache";
import { getSpellName, getSpellBlurb, getSpellIcon } from "../../net/spellCache";
import { buildSpellIcon } from "../../ui/hud/spellIcon";

/** Floor-clear spell picks are tagged with this prefix (matches DungeonService.SpellPrefix). */
const SPELL_PREFIX = "spell:";

// ── Shared helpers ────────────────────────────────────────────────────────────

export function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

/** Animate a reward number ticking up from 0 to target (easeOutCubic) — satisfying reveal. */
function countUp(el: HTMLElement, target: number, prefix: string, suffix: string, durMs = 650) {
  const start = performance.now();
  const tick = (now: number) => {
    const t = Math.min((now - start) / durMs, 1);
    const eased = 1 - Math.pow(1 - t, 3);
    el.textContent = `${prefix}${Math.round(target * eased)}${suffix}`;
    if (t < 1) requestAnimationFrame(tick);
  };
  tick(start); // set the initial (+0) synchronously so there is no flash of empty text
}


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
  // Heal pick (docs/04 §5)
  heal: "Heal",
  // Shop floor pick (docs/04 §6.2)
  shop: "Visit Shop",
  // Campaign shop: skill-points pack (docs/04 §6.1)
  points: "Skill Points",
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
  // Heal pick (docs/04 §5) — the old game's HP-bonus heart reads instantly as "restore health".
  heal: "/ui/BonusHP.png",
  // Shop floor pick (docs/04 §6.2) — a gem-filled treasure chest.
  shop: "/art/BlueChest.png",
  // Campaign shop: skill-points pack (docs/04 §6.1) — the upgrades button glyph.
  points: "/ui/InterfaceSkillsButton.png",
};

// Ball-core / paddle-mod effect blurbs. Relics carry their own description from the
// catalog (getRelicDesc); cores live in code, so these are transcribed straight from
// the implementation (BallSystem/Modifiers/GameInstance.Commands) — not the design doc.
const BALL_CORE_DESCS: Record<string, string> = {
  heavy:       "+1 ball damage on every hit.",
  split:       "Serves an extra ball each launch.",
  ember:       "Ball serves ignited — burns the blocks it hits.",
  ghost:       "Ball phases through a block each serve.",
  echo:        "Arms a bonus-damage strike on the next block hit.",
  frost:       "Freezes enemy emitters & shield statues on hit.",
  mod_wide:    "Widens the paddle by 20%.",
  mod_grip:    "Greater deflect-angle control (+10°).",
  mod_cannons: "Paddle auto-fires slow side-cannon volleys.",
  heal:        "Restores +2 HP for the rest of the run.",
  shop:        "Spend Gold on relics, spells, or heals.",
  points:      "+2 skill points to spend on spell upgrades.",
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
  if (id.startsWith(SPELL_PREFIX)) return getSpellName(id.slice(SPELL_PREFIX.length));
  return BALL_CORE_NAMES[id] ?? getRelicName(id);
}

export function buffIcon(id: string): string {
  // Spell icons are atlas keys (not /ui/ paths); for small <img> chips use a generic spell glyph.
  if (id.startsWith(SPELL_PREFIX)) return "/art/SpellFireball.png";
  return getRelicIcon(id) ?? BALL_CORE_ICONS[id] ?? "/art/ItemGem.png";
}

/** One-line effect for a boon: relic catalog description, else the core/mod blurb, else spell blurb. */
export function buffDesc(id: string): string | undefined {
  if (id.startsWith(SPELL_PREFIX)) return getSpellBlurb(id.slice(SPELL_PREFIX.length));
  return getRelicDesc(id) ?? BALL_CORE_DESCS[id];
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
      padding: var(--sp-4h);
      box-sizing: border-box;
    }

    /* Framed panel — LvlUpInterfacePanel art as background */
    .ov-panel {
      position: relative;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(10,8,26,0.95);
      border: 2px solid rgba(180,140,60,0.7);
      border-radius: 12px;
      padding: var(--sp-5) var(--sp-6);
      min-width: min(280px, 88cqw);
      max-width: min(400px, 92cqw);
      display: flex;
      flex-direction: column;
      gap: var(--sp-2h);
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
      font-size: var(--fs-2xl);
      font-weight: 700;
      letter-spacing: 0.1em;
      text-shadow: 0 0 20px currentColor, 0 2px 4px rgba(0,0,0,0.9);
      margin-bottom: var(--sp-2);
    }
    .ov-title-win   { color: var(--gold-bright); }
    .ov-title-green { color: var(--ok-bright); }
    .ov-title-red   { color: var(--danger-bright); }

    /* Level-up — the key progression beat. Pops in AFTER the reward count-ups
       (700ms delay), then glows to hold the eye. Reduced-motion = plain text. */
    .ov-levelup {
      font-size: var(--fs-large); color: var(--color-levelup);
      font-weight: 700; letter-spacing: 0.04em; margin-top: var(--sp-1);
      animation: ov-levelup-in 520ms cubic-bezier(0.2,1.4,0.5,1) 700ms both,
                 ov-levelup-glow 1.6s ease-in-out 1220ms infinite;
    }
    @keyframes ov-levelup-in {
      0%   { transform: scale(0.4); opacity: 0; }
      60%  { transform: scale(1.18); opacity: 1; }
      100% { transform: scale(1);   opacity: 1; }
    }
    @keyframes ov-levelup-glow {
      0%,100% { text-shadow: 0 0 8px var(--color-levelup), 0 2px 4px rgba(0,0,0,0.9); }
      50%     { text-shadow: 0 0 20px var(--color-levelup), 0 0 32px var(--color-levelup), 0 2px 4px rgba(0,0,0,0.9); }
    }
    @media (prefers-reduced-motion: reduce) {
      .ov-levelup { animation: none; }
    }

    /* ── Level star reveal ── */
    .ov-stars-section {
      display: flex; flex-direction: column; align-items: center;
      gap: 6px; margin-bottom: var(--sp-2);
    }
    .ov-stars-row {
      display: flex; gap: 6px; align-items: center;
    }
    .ov-star-earned, .ov-star-empty {
      font-size: 52px; line-height: 1; display: inline-block;
      animation: ov-star-pop 0.55s cubic-bezier(0.2, 1.45, 0.4, 1) both;
    }
    .ov-star-earned {
      color: #ffd56a;
      filter: drop-shadow(0 0 8px rgba(255,200,70,0.85));
    }
    .ov-star-empty { color: rgba(255,255,255,0.13); }
    @keyframes ov-star-pop {
      0%   { transform: scale(0.05) rotate(-18deg); opacity: 0; }
      65%  { transform: scale(1.45) rotate(6deg);   opacity: 1; }
      82%  { transform: scale(0.86) rotate(-3deg); }
      100% { transform: scale(1)    rotate(0deg);   opacity: 1; }
    }
    /* Earned stars pulse after popping in */
    .ov-star-earned { animation:
        ov-star-pop  0.55s cubic-bezier(0.2, 1.45, 0.4, 1) both,
        ov-star-glow 2.4s ease-in-out 1.5s infinite;
    }
    @keyframes ov-star-glow {
      0%,100% { filter: drop-shadow(0 0 7px rgba(255,200,70,0.75)); }
      50%     { filter: drop-shadow(0 0 18px rgba(255,210,80,1)) drop-shadow(0 0 30px rgba(255,140,30,0.65)); }
    }
    .ov-star-label {
      font-size: var(--fs-caption); font-weight: 700; letter-spacing: 0.09em;
      color: #c8a040; opacity: 0;
      animation: ov-star-label-in 0.4s ease-out both;
    }
    @keyframes ov-star-label-in {
      from { opacity: 0; transform: translateY(5px); }
      to   { opacity: 1; transform: none; }
    }
    @media (prefers-reduced-motion: reduce) {
      .ov-star-earned, .ov-star-empty, .ov-star-label { animation: none; opacity: 1; }
    }

    /* Defeat-screen teaching tip — a death is a moment to learn something. */
    .ov-tip {
      max-width: min(360px, 84cqw);
      margin: var(--sp-1) 0 var(--sp-1);
      text-align: center;
      font-size: var(--fs-caption);
      line-height: 1.45;
      color: var(--text-dim);
      text-shadow: 0 1px 2px rgba(0,0,0,0.9);
    }
    .ov-tip-label {
      display: inline-block;
      margin-right: 6px;
      font-weight: 900;
      letter-spacing: 0.14em;
      color: var(--gold-bright);
    }

    .ov-reward-row {
      display: flex;
      align-items: center;
      gap: var(--sp-2);
      font-size: var(--fs-large);
    }

    /* Art button — InterfaceButton pill, 9-sliced (fixed rounded ends + stretched middle) */
    .ov-btn {
      margin-top: var(--sp-3);
      padding: 0 16px;
      height: 52px;
      min-width: min(200px, 70cqw);
      background: none;
      border-style: solid;
      border-width: 8px 30px;
      border-image: url('/ui/InterfaceButton.png') 26 92 26 92 fill stretch;
      cursor: pointer;
      font-size: var(--fs-large);
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
      margin-bottom: var(--sp-1);
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
      padding: var(--sp-3) var(--sp-2) var(--sp-2h) var(--sp-2);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--sp-1h);
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
      font-size: var(--fs-small);
      font-weight: 700;
      text-align: center;
      color: var(--text);
      line-height: 1.3;
    }
    .ov-bonus-desc {
      font-size: var(--fs-tiny);
      text-align: center;
      color: var(--text-dim);
      line-height: 1.3;
    }

    /* Synergy hint (docs/04 §7): the pick glows and names its combo partner */
    .ov-bonus-synergy {
      border-color: rgba(120, 220, 140, 0.85);
      box-shadow: 0 0 14px rgba(120, 220, 140, 0.35);
    }
    .ov-bonus-hint {
      font-size: var(--fs-tiny);
      font-weight: 700;
      text-align: center;
      color: var(--ok-bright);
      line-height: 1.2;
      letter-spacing: 0.02em;
    }

    .ov-bonus-row {
      display: flex;
      gap: var(--sp-2h);
      flex-wrap: wrap;
      justify-content: center;
      max-width: min(400px, 96cqw);
    }

    .ov-pick-title {
      font-size: var(--fs-section);
      font-weight: 700;
      color: var(--gold);
      letter-spacing: 0.06em;
      margin-bottom: var(--sp-3);
      text-shadow: 0 0 12px rgba(255,200,50,0.5);
    }

    /* ── Shop floor (docs/04 §6.2) ── */
    /* The gem sprite is grayscale; warm it to gold so currency reads as Gold, not a stray gem. */
    .ov-shop-balance img, .ov-shop-price img {
      image-rendering: auto;
      filter: sepia(1) saturate(4) hue-rotate(-12deg) brightness(1.05);
    }
    .ov-shop-balance {
      display: flex; align-items: center; gap: 6px;
      font-size: var(--fs-large); font-weight: 700;
      color: var(--gold-bright);
      margin-bottom: var(--sp-3);
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    }
    .ov-shop-balance img { width: 22px; height: 22px; }
    .ov-shop-price {
      display: flex; align-items: center; gap: 4px;
      margin-top: var(--sp-1h);
      font-size: var(--fs-caption); font-weight: 700;
      color: var(--gold-bright);
    }
    .ov-shop-price img { width: 14px; height: 14px; }
    .ov-shop-buy {
      margin-top: var(--sp-1h);
      padding: 5px 14px;
      border: 1px solid rgba(180,140,60,0.7);
      border-radius: 7px;
      background: rgba(60,40,10,0.75);
      color: var(--text);
      font-family: var(--font-body);
      font-size: var(--fs-caption);
      font-weight: 700;
      cursor: pointer;
      transition: filter var(--dur-fast), transform var(--dur-fast);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .ov-shop-buy:hover:not(:disabled)  { filter: brightness(1.35); }
    .ov-shop-buy:active:not(:disabled) { transform: scale(0.96); }
    .ov-shop-buy:disabled { opacity: 0.35; cursor: not-allowed; }
    .ov-shop-bought { opacity: 0.6; }
    .ov-shop-bought .ov-shop-buy { color: var(--ok-bright); border-color: var(--ok-bright); }
  `;
  document.head.appendChild(style);
}

// ── Overlay builders ──────────────────────────────────────────────────────────

/** Campaign victory overlay (#reward-overlay). */
export function buildRewardOverlay(
  reward: CompleteResult["reward"],
  onContinue: () => void,
  heroXp?: CompleteResult["heroXp"],
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
    // ── Animated star reveal (top of panel — most prominent element) ─────────
    const earnedStars = reward.levelStars ?? 0;
    if (earnedStars > 0) {
      const starSec = document.createElement("div");
      starSec.id = "level-stars";
      starSec.className = "ov-stars-section";

      const starsRow = document.createElement("div");
      starsRow.className = "ov-stars-row";
      for (let i = 1; i <= 3; i++) {
        const star = document.createElement("div");
        star.className = i <= earnedStars ? "ov-star-earned" : "ov-star-empty";
        star.textContent = "★";
        star.style.animationDelay = `${(i - 1) * 0.32}s`;
        starsRow.appendChild(star);
      }
      starSec.appendChild(starsRow);

      if (earnedStars >= 2) {
        const lbl = document.createElement("div");
        lbl.className = "ov-star-label";
        lbl.textContent = earnedStars === 3 ? "Perfect run!" : "Nice run!";
        lbl.style.animationDelay = "1.1s";
        starSec.appendChild(lbl);
      }
      panel.appendChild(starSec);
    }

    if (reward.expGained > 0) {
      const expEl = document.createElement("div");
      expEl.id = "reward-exp";
      expEl.className = "ov-reward-row";
      const expIco = document.createElement("img");
      expIco.src = "/ui/ExpBarFull.png";
      css(expIco, { width: "28px", height: "12px", imageRendering: "pixelated" });
      expEl.appendChild(expIco);
      const expText = document.createElement("span");
      countUp(expText, reward.expGained, "+", " EXP");
      css(expText, { color: "var(--color-xp)", fontSize: "var(--fs-large)" });
      expEl.appendChild(expText);
      panel.appendChild(expEl);
    }

    if ((reward.insightGained ?? 0) > 0) {
      const pointsEl = document.createElement("div");
      pointsEl.id = "reward-points";
      pointsEl.className = "ov-reward-row";
      const ptIco = document.createElement("span");
      ptIco.textContent = "◇";
      css(ptIco, { fontSize: "22px", color: "#e8a64c", lineHeight: "1" });
      pointsEl.appendChild(ptIco);
      const ptText = document.createElement("span");
      countUp(ptText, reward.insightGained, "+", " Insight");
      css(ptText, { color: "#e8a64c", fontSize: "var(--fs-large)" });
      pointsEl.appendChild(ptText);
      panel.appendChild(pointsEl);
    }

    if ((reward.sparksGained ?? 0) > 0) {
      const crystalsEl = document.createElement("div");
      crystalsEl.id = "reward-crystals";
      crystalsEl.className = "ov-reward-row";
      const cIco = document.createElement("span");
      cIco.textContent = "✦";
      css(cIco, { fontSize: "22px", color: "#ffd56a", lineHeight: "1" });
      crystalsEl.appendChild(cIco);
      const cText = document.createElement("span");
      countUp(cText, reward.sparksGained, "+", " Sparks");
      css(cText, { color: "#ffd56a", fontSize: "var(--fs-large)" });
      crystalsEl.appendChild(cText);
      panel.appendChild(crystalsEl);
    }

    // Souls (economy rework) — the spell/hero coin; bosses pay it and star bonuses add to it.
    if ((reward.soulsGained ?? 0) > 0) {
      const goldEl = document.createElement("div");
      goldEl.id = "reward-souls";
      goldEl.className = "ov-reward-row";
      const gIco = document.createElement("span");
      gIco.textContent = "◆";
      css(gIco, { fontSize: "22px", color: "#6cc0ff", lineHeight: "1" });
      goldEl.appendChild(gIco);
      const gText = document.createElement("span");
      countUp(gText, reward.soulsGained ?? 0, "+", " Souls");
      css(gText, { color: "#6cc0ff", fontSize: "var(--fs-large)" });
      goldEl.appendChild(gText);
      panel.appendChild(goldEl);
    } else if ((reward.starBonusSouls ?? 0) > 0) {
      // Non-boss re-clear that earned a new star tier — show the star bonus by itself.
      const sbEl = document.createElement("div");
      sbEl.id = "reward-star-bonus";
      sbEl.className = "ov-reward-row";
      const sbIco = document.createElement("span");
      sbIco.textContent = "◆";
      css(sbIco, { fontSize: "22px", color: "#6cc0ff", lineHeight: "1" });
      sbEl.appendChild(sbIco);
      const sbText = document.createElement("span");
      countUp(sbText, reward.starBonusSouls ?? 0, "+", " Souls");
      css(sbText, { color: "#6cc0ff", fontSize: "var(--fs-large)" });
      sbEl.appendChild(sbText);
      panel.appendChild(sbEl);
    }

    if (reward.leveledUp) {
      const lvlUp = document.createElement("div");
      lvlUp.id = "reward-levelup";
      lvlUp.className = "ov-levelup";
      lvlUp.textContent = `Level Up! → Lv ${reward.newLevel}`;
      panel.appendChild(lvlUp);
    }

    // Hero level-up beat (§5.3): the selected hero gained a level this battle.
    if (heroXp?.leveledUp) {
      const heroUp = document.createElement("div");
      heroUp.id = "reward-hero-levelup";
      heroUp.className = "ov-levelup";
      heroUp.textContent = `Hero Level Up! → Lv ${heroXp.newLevel}`;
      css(heroUp, { color: "var(--gold-bright)" });
      panel.appendChild(heroUp);
    }

    // Show hero star tier and any ascension tokens earned this battle.
    if (heroXp && heroXp.stars > 0) {
      const starRow = document.createElement("div");
      starRow.id = "reward-hero-stars";
      const filled = "★".repeat(heroXp.stars);
      const empty  = "☆".repeat(Math.max(0, 6 - heroXp.stars));
      starRow.textContent = filled + empty;
      css(starRow, { fontSize: "var(--fs-subhead)", color: "var(--gold)", letterSpacing: "0.1em", marginTop: "var(--sp-1)" });
      panel.appendChild(starRow);
    }
    if (heroXp && heroXp.tokensGranted > 0) {
      const tok = document.createElement("div");
      tok.id = "reward-hero-tokens";
      tok.className = "ov-reward-row";
      const tIco = document.createElement("span");
      tIco.textContent = "◆";
      css(tIco, { fontSize: "18px", color: "#6cc0ff", lineHeight: "1" });
      tok.appendChild(tIco);
      const tText = document.createElement("span");
      tText.textContent = `+${heroXp.tokensGranted} Soul${heroXp.tokensGranted === 1 ? "" : "s"}`;
      css(tText, { fontSize: "var(--fs-small)", color: "#6cc0ff" });
      tok.appendChild(tText);
      panel.appendChild(tok);
    }

    if (reward.firstClear) {
      const first = document.createElement("div");
      first.textContent = "First Clear!";
      css(first, { fontSize: "var(--fs-subhead)", color: "var(--color-first-clear)", marginTop: "var(--sp-1)" });
      panel.appendChild(first);
    } else if (reward.expGained === 0 && (reward.starBonusSouls ?? 0) === 0) {
      const reclear = document.createElement("div");
      reclear.textContent = "Re-clear — no new rewards";
      css(reclear, { fontSize: "var(--fs-small)", color: "var(--text-dim)", marginTop: "var(--sp-1)" });
      panel.appendChild(reclear);
    }

    // Boss-clear progression payoff (docs/04 §4.1/§5): newly unlocked spells + a grown hotbar slot.
    if (reward.slotsUnlocked && reward.slotsUnlocked > 0) {
      const slot = document.createElement("div");
      slot.id = "reward-slot";
      slot.textContent = "+1 Spell Slot!";
      css(slot, { fontSize: "var(--fs-subhead)", color: "var(--color-mana, #6cf)", fontWeight: "700", marginTop: "var(--sp-1)", textShadow: "0 0 8px rgba(120,180,255,0.4)" });
      panel.appendChild(slot);
    }
    if (reward.spellsUnlocked && reward.spellsUnlocked.length) {
      const names = reward.spellsUnlocked.map(id => getSpellName(id)).join(", ");
      const sp = document.createElement("div");
      sp.id = "reward-spells";
      sp.textContent = `New spells: ${names}`;
      css(sp, { fontSize: "var(--fs-caption)", color: "var(--gold-bright)", textAlign: "center", marginTop: "var(--sp-1)", lineHeight: "1.35", maxWidth: "260px", textShadow: "0 1px 2px rgba(0,0,0,0.9)" });
      panel.appendChild(sp);
      const hint = document.createElement("div");
      hint.textContent = "Equip them in Loadout";
      css(hint, { fontSize: "var(--fs-tiny)", color: "var(--text-dim)", textAlign: "center", marginTop: "1px" });
      panel.appendChild(hint);
    }
    // Progressive feature unlocks — a clear "🔓 Unlocked" beat when a campaign milestone opens a system.
    if (reward.featuresUnlocked && reward.featuresUnlocked.length) {
      for (const name of reward.featuresUnlocked) {
        const fu = document.createElement("div");
        fu.className = "reward-feature";
        fu.textContent = `🔓 Unlocked: ${name}`;
        css(fu, { fontSize: "var(--fs-subhead)", color: "var(--ok-bright, #7fe3a0)", fontWeight: "700",
                  marginTop: "var(--sp-1h)", textAlign: "center", textShadow: "0 0 10px rgba(127,227,160,0.5)" });
        panel.appendChild(fu);
      }
    }
  } else {
    const msg = document.createElement("div");
    msg.textContent = "Level complete!";
    css(msg, { color: "var(--color-xp)" });
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
// Universal (class-agnostic) gameplay tips — true for every hero, so a defeat
// always teaches something accurate regardless of which class died.
const DEFEAT_TIPS = [
  "Deflect with the paddle's centre for a perfect hit — it rewards bonus mana.",
  "Spells light up the instant you have enough mana to cast them — watch the bar.",
  "When your HP is critical the screen edges pulse red. Play it safe.",
  "Catch the ball off-centre to angle your next bounce where you need it.",
  "Don't chase the ball — move to where it's about to land.",
  "Break blocks to drop power-ups, then catch them with your paddle.",
];

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

  // A death screen is a teaching moment — show one universal (class-agnostic) tip.
  const tip = document.createElement("div");
  tip.className = "ov-tip";
  const label = document.createElement("span");
  label.className = "ov-tip-label";
  label.textContent = "TIP";
  tip.appendChild(label);
  tip.append(DEFEAT_TIPS[Math.floor(Math.random() * DEFEAT_TIPS.length)]);
  overlay.appendChild(tip);

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

    if (choiceId.startsWith(SPELL_PREFIX)) {
      // Spell icons are atlas keys, not /ui/ paths — resolve via buildSpellIcon (atlas→art→letter).
      const spellId = choiceId.slice(SPELL_PREFIX.length);
      const iconWrap = document.createElement("div");
      iconWrap.className = "ov-bonus-icon";
      css(iconWrap, { display: "flex", alignItems: "center", justifyContent: "center" });
      buildSpellIcon(iconWrap, { id: spellId, name: getSpellName(spellId), icon: getSpellIcon(spellId) ?? "", manaCost: 0 });
      card.appendChild(iconWrap);
    } else {
      const icon = document.createElement("img");
      icon.src = buffIcon(choiceId);
      icon.alt = buffName(choiceId);
      icon.className = "ov-bonus-icon";
      card.appendChild(icon);
    }

    const nameEl = document.createElement("div");
    nameEl.textContent = buffName(choiceId);
    nameEl.className = "ov-bonus-name";
    card.appendChild(nameEl);

    // Effect blurb so the pick is an informed choice, not a guess from the name.
    const desc = buffDesc(choiceId);
    if (desc) {
      const descEl = document.createElement("div");
      descEl.textContent = desc;
      descEl.className = "ov-bonus-desc";
      card.appendChild(descEl);
    }

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

/** Gold currency icon — the same treasure-gem sprite the player catches as a coins pickup. */
const GOLD_ICON = "/ui/BonusGem.png";

function shopItemName(it: ShopItem): string {
  if (it.kind === "spell") return getSpellName(it.id);
  if (it.kind === "spell_level") return `Level Up: ${getSpellName(it.id)}`;
  if (it.kind === "points") return "Skill Points";
  return buffName(it.id);
}
function shopItemDesc(it: ShopItem): string | undefined {
  if (it.kind === "spell") return getSpellBlurb(it.id);
  if (it.kind === "spell_level") return `Raise ${getSpellName(it.id)} by one level (paid in Gold, not Points).`;
  if (it.kind === "points") return "+2 skill points to spend on spell upgrades.";
  return buffDesc(it.id);
}
/** A spell-art icon is used for both castable spells and spell level-up purchases. */
function shopItemUsesSpellIcon(it: ShopItem): boolean {
  return it.kind === "spell" || it.kind === "spell_level";
}

/** A small gem icon + amount, used for the gold balance and per-item prices. */
function goldChip(amount: number, cls: string): HTMLElement {
  const wrap = document.createElement("div");
  wrap.className = cls;
  const img = document.createElement("img");
  img.src = GOLD_ICON;
  img.alt = "Gold";
  wrap.appendChild(img);
  const span = document.createElement("span");
  span.textContent = String(amount);
  wrap.appendChild(span);
  return wrap;
}

/**
 * Shared shop overlay used by the dungeon shop floor (docs/04 §6.2) and the campaign shop node
 * (docs/04 §6.1). Browse boons priced in Gold; buy what you can afford (deducts from the live balance),
 * then Leave Shop. onBuy hits the server (the source of truth for the new balance) and returns the
 * remaining gold; onLeave closes the shop and returns to the caller's flow.
 */
function buildShopOverlayCore(opts: {
  overlayId: string;
  title: string;
  data: ShopItemsResult;
  onBuy: (itemId: string) => Promise<{ ok: boolean; gold: number }>;
  onLeave: () => void;
}): HTMLElement {
  injectOverlayStyles();
  let currentGold = opts.data.gold;

  const overlay = document.createElement("div");
  overlay.id = opts.overlayId;
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = opts.title;
  title.className = "ov-pick-title";
  overlay.appendChild(title);

  // Live gold balance.
  const balance = goldChip(currentGold, "ov-shop-balance");
  balance.id = "shop-gold";
  overlay.appendChild(balance);
  const balanceSpan = balance.querySelector("span")!;

  const row = document.createElement("div");
  row.className = "ov-bonus-row";

  // Track each card's buy button so a purchase can re-evaluate affordability across the shelf.
  const buyButtons: { item: ShopItem; btn: HTMLButtonElement }[] = [];

  for (const item of opts.data.items) {
    const card = document.createElement("div");
    card.setAttribute("data-shop-item", item.id);
    card.className = "ov-bonus-card";

    if (shopItemUsesSpellIcon(item)) {
      const iconWrap = document.createElement("div");
      iconWrap.className = "ov-bonus-icon";
      css(iconWrap, { display: "flex", alignItems: "center", justifyContent: "center" });
      buildSpellIcon(iconWrap, { id: item.id, name: getSpellName(item.id), icon: getSpellIcon(item.id) ?? "", manaCost: 0 });
      card.appendChild(iconWrap);
    } else {
      const icon = document.createElement("img");
      icon.src = buffIcon(item.id);
      icon.alt = shopItemName(item);
      icon.className = "ov-bonus-icon";
      card.appendChild(icon);
    }

    const nameEl = document.createElement("div");
    nameEl.textContent = shopItemName(item);
    nameEl.className = "ov-bonus-name";
    card.appendChild(nameEl);

    const desc = shopItemDesc(item);
    if (desc) {
      const descEl = document.createElement("div");
      descEl.textContent = desc;
      descEl.className = "ov-bonus-desc";
      card.appendChild(descEl);
    }

    // Price badge.
    card.appendChild(goldChip(item.price, "ov-shop-price"));

    // Buy button.
    const btn = document.createElement("button");
    btn.className = "ov-shop-buy";
    btn.textContent = "Buy";
    btn.disabled = currentGold < item.price;
    btn.addEventListener("click", async () => {
      btn.disabled = true;
      try {
        const res = await opts.onBuy(item.id);
        currentGold = res.gold;
        balanceSpan.textContent = String(currentGold);
        if (res.ok) {
          btn.textContent = "Purchased";
          card.classList.add("ov-shop-bought");
        }
      } catch (e) {
        console.error("buy failed", e);
      }
      // Re-evaluate affordability for the rest of the shelf (a bought card stays disabled).
      for (const bb of buyButtons) {
        if (bb.btn.classList.contains("ov-shop-buy-done")) continue;
        if (bb.btn.textContent === "Purchased") { bb.btn.classList.add("ov-shop-buy-done"); continue; }
        bb.btn.disabled = currentGold < bb.item.price;
      }
    });
    buyButtons.push({ item, btn });
    card.appendChild(btn);

    row.appendChild(card);
  }

  overlay.appendChild(row);

  const leave = document.createElement("button");
  leave.id = "btn-leave-shop";
  leave.className = "ov-btn";
  leave.textContent = "Leave Shop";
  leave.addEventListener("click", opts.onLeave);
  overlay.appendChild(leave);

  return overlay;
}

/** Dungeon shop floor overlay (#shop-overlay, docs/04 §6.2). */
export function buildShopOverlay(
  data: ShopItemsResult,
  onBuy: (itemId: string) => Promise<ShopBuyResult>,
  onLeave: () => void,
): HTMLElement {
  return buildShopOverlayCore({ overlayId: "shop-overlay", title: "Shop", data, onBuy, onLeave });
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
  css(rewardTitle, { fontSize: "var(--fs-body)", color: "var(--color-label-muted)", letterSpacing: "0.05em" });
  panel.appendChild(rewardTitle);

  const profile = data.profile;
  // Show the crystals THIS run granted (run.rewardCrystals), not the total balance — the old
  // "{total} Crystals" under "Permanent Reward" read as if the run gave you your whole balance.
  const gained = data.run?.rewardCrystals;
  if (gained !== undefined || profile?.crystals !== undefined) {
    const crystalEl = document.createElement("div");
    crystalEl.id = "dungeon-clear-crystals";
    crystalEl.className = "ov-reward-row";
    const gemImg = document.createElement("img");
    gemImg.src = "/ui/GemBlue.png";
    css(gemImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    crystalEl.appendChild(gemImg);
    const crystalText = document.createElement("span");
    crystalText.textContent = gained !== undefined ? `+${gained} Crystals` : `${profile!.crystals} Crystals`;
    css(crystalText, { fontSize: "var(--fs-xl)", color: "var(--color-crystal)" });
    crystalEl.appendChild(crystalText);
    panel.appendChild(crystalEl);

    // Running total as a quiet sub-line so the "+N" above is unambiguously the run's grant.
    if (gained !== undefined && profile?.crystals !== undefined) {
      const totalEl = document.createElement("div");
      totalEl.id = "dungeon-clear-total";
      totalEl.textContent = `Total: ${profile.crystals}`;
      css(totalEl, { fontSize: "var(--fs-tiny)", color: "var(--text-dim)", marginTop: "-2px" });
      panel.appendChild(totalEl);
    }
  }

  if (profile?.unlockedRelics && Array.isArray(profile.unlockedRelics) && profile.unlockedRelics.length > 0) {
    const lastRelic = profile.unlockedRelics[profile.unlockedRelics.length - 1];
    const relicRow = document.createElement("div");
    relicRow.className = "ov-reward-row";
    const relicImg = document.createElement("img");
    relicImg.src = getRelicIcon(lastRelic) ?? "/art/ItemGem.png";
    css(relicImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    relicRow.appendChild(relicImg);
    const relicText = document.createElement("span");
    relicText.id = "dungeon-clear-relic";
    relicText.textContent = getRelicName(lastRelic);
    css(relicText, { fontSize: "var(--fs-large)", color: "var(--color-relic)" });
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

/**
 * Continuous-Rift end card (2026-06-16). Shares the same overlay shell (ov-backdrop / ov-chest / ov-panel /
 * ov-reward-row / ov-btn + countUp) as the campaign + dungeon reward cards — the rift variant adds the
 * depth-reached line and the depth/milestone reward breakdown. Used for both a full clear and a death/bail.
 */
export function buildRiftRewardCard(data: RiftFinishResult, onDone: () => void): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "rift-reward-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = data.won ? "Rift Conquered!" : "Rift Ended";
  title.className = data.won ? "ov-title ov-title-green" : "ov-title";
  overlay.appendChild(title);

  const chest = document.createElement("img");
  chest.src = "/ui/BlueChest.png";
  chest.className = "ov-chest";
  overlay.appendChild(chest);

  const panel = document.createElement("div");
  panel.className = "ov-panel";

  const depthEl = document.createElement("div");
  depthEl.id = "rift-reward-depth";
  css(depthEl, { fontSize: "var(--fs-xl)", color: "var(--gold-bright)", letterSpacing: "0.04em" });
  depthEl.textContent = data.won ? `★ FULL CLEAR — Depth ${data.depth}/${data.totalFloors}`
                                 : `Reached Depth ${data.depth}/${data.totalFloors}`;
  panel.appendChild(depthEl);

  const soulsEl = document.createElement("div");
  soulsEl.id = "rift-reward-souls";
  soulsEl.className = "ov-reward-row";
  const sIco = document.createElement("img");
  sIco.src = "/ui/GemBlue.png";
  css(sIco, { width: "24px", height: "24px", imageRendering: "pixelated" });
  soulsEl.appendChild(sIco);
  const sText = document.createElement("span");
  css(sText, { fontSize: "var(--fs-xl)", color: "var(--color-crystal)" });
  soulsEl.appendChild(sText);
  panel.appendChild(soulsEl);
  countUp(sText, data.soulsGained, "+", " Souls");

  const note = document.createElement("div");
  css(note, { fontSize: "var(--fs-tiny)", color: "var(--text-dim)", marginTop: "2px", textAlign: "center" });
  note.textContent = "Reward scales with depth — milestone bumps at 3 / 5 / 7 / 10.";
  panel.appendChild(note);

  overlay.appendChild(panel);

  const doneBtn = document.createElement("button");
  doneBtn.id = "btn-rift-done";
  doneBtn.className = "ov-btn";
  doneBtn.textContent = "Return to Campaign";
  doneBtn.addEventListener("click", onDone);
  overlay.appendChild(doneBtn);

  return overlay;
}

/**
 * §8 mid-rift modifier draft (owner 2026-06-16): a small "choose a boon" overlay shown between rift floors
 * (the sim is frozen server-side while it's up). Picking sends the choice and the next floor slides in.
 */
export function buildRiftDraftOverlay(
  choices: { id: string; name: string; desc: string }[],
  onPick: (id: string) => void,
): HTMLElement {
  injectOverlayStyles();

  const overlay = document.createElement("div");
  overlay.id = "rift-draft-overlay";
  overlay.className = "ov-backdrop";

  const title = document.createElement("div");
  title.textContent = "Choose a Boon";
  title.className = "ov-title ov-title-green";
  overlay.appendChild(title);

  const sub = document.createElement("div");
  sub.textContent = "Rift modifier — lasts the rest of the run";
  css(sub, { fontSize: "var(--fs-tiny)", color: "var(--text-dim)", marginTop: "-6px", marginBottom: "8px" });
  overlay.appendChild(sub);

  const row = document.createElement("div");
  css(row, { display: "flex", gap: "10px", flexWrap: "wrap", justifyContent: "center", maxWidth: "440px" });
  for (const c of choices) {
    const card = document.createElement("button");
    card.className = "ov-btn rift-draft-card";
    card.dataset.modId = c.id;
    css(card, {
      display: "flex", flexDirection: "column", gap: "5px", width: "128px", minHeight: "92px",
      padding: "10px 12px", textAlign: "left", whiteSpace: "normal", lineHeight: "1.2",
    });
    const n = document.createElement("div");
    n.textContent = c.name;
    css(n, { fontWeight: "800", fontSize: "var(--fs-body)" });
    const d = document.createElement("div");
    d.textContent = c.desc;
    css(d, { fontSize: "var(--fs-tiny)", color: "var(--color-label-muted)", fontWeight: "400" });
    card.appendChild(n);
    card.appendChild(d);
    card.addEventListener("click", () => onPick(c.id));
    row.appendChild(card);
  }
  overlay.appendChild(row);

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
  css(sub, { color: "var(--color-fail-muted)", fontSize: "var(--fs-large)", letterSpacing: "0.04em", marginBottom: "var(--sp-2)" });
  overlay.appendChild(sub);

  // Same teaching-moment treatment as the campaign defeat overlay (a death is a death).
  const tip = document.createElement("div");
  tip.className = "ov-tip";
  const tipLabel = document.createElement("span");
  tipLabel.className = "ov-tip-label";
  tipLabel.textContent = "TIP";
  tip.appendChild(tipLabel);
  tip.append(DEFEAT_TIPS[Math.floor(Math.random() * DEFEAT_TIPS.length)]);
  overlay.appendChild(tip);

  const exitBtn = document.createElement("button");
  exitBtn.id = "btn-dungeon-exit";
  exitBtn.className = "ov-btn";
  exitBtn.textContent = "Return to Campaign";
  css(exitBtn, { filter: "hue-rotate(200deg) saturate(0.7)" });
  exitBtn.addEventListener("click", onExit);
  overlay.appendChild(exitBtn);

  return overlay;
}
