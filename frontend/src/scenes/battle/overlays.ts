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
};

const RELIC_ICONS: Record<string, string> = {
  glass_cannon: "/art/ItemHummer.png",
  flint_core: "/art/ItemDrill.png",
  pyroclasm: "/art/ItemTorch.png",
  mana_battery: "/art/ItemGem.png",
};

const BALL_CORE_NAMES: Record<string, string> = {
  heavy: "Heavy Core",
  split: "Split Core",
  ember: "Ember Core",
};

const BALL_CORE_ICONS: Record<string, string> = {
  heavy: "/art/BonusRock.png",
  split: "/art/BonusSplit.png",
  ember: "/art/BonusFire.png",
};

export function buffName(id: string): string {
  return RELIC_NAMES[id] ?? BALL_CORE_NAMES[id] ?? id;
}

export function buffIcon(id: string): string {
  return RELIC_ICONS[id] ?? BALL_CORE_ICONS[id] ?? "/art/ItemGem.png";
}

// ── Overlay builders ──────────────────────────────────────────────────────────

/** Campaign victory overlay (#reward-overlay). */
export function buildRewardOverlay(
  reward: CompleteResult["reward"],
  onContinue: () => void,
): HTMLElement {
  const overlay = document.createElement("div");
  overlay.id = "reward-overlay";
  css(overlay, {
    position: "fixed", inset: "0",
    background: "rgba(0,0,0,0.82)",
    display: "flex", flexDirection: "column",
    alignItems: "center", justifyContent: "center",
    zIndex: "1000",
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    gap: "12px",
  });

  const title = document.createElement("div");
  title.textContent = "Victory!";
  css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#ffd700", letterSpacing: "0.1em" });
  overlay.appendChild(title);

  const card = document.createElement("div");
  css(card, {
    background: "#12122a",
    border: "1px solid #334466",
    borderRadius: "12px",
    padding: "24px 36px",
    minWidth: "280px",
    display: "flex",
    flexDirection: "column",
    gap: "8px",
    alignItems: "center",
  });

  if (reward) {
    const expEl = document.createElement("div");
    expEl.id = "reward-exp";
    expEl.textContent = `+${reward.expGained} EXP`;
    css(expEl, { fontSize: "1.2rem", color: "#88aaff" });
    card.appendChild(expEl);

    const pointsEl = document.createElement("div");
    pointsEl.id = "reward-points";
    pointsEl.textContent = `+${reward.pointsGained} Skill Points`;
    css(pointsEl, { fontSize: "1.1rem", color: "#ffcc44" });
    card.appendChild(pointsEl);

    const crystalsEl = document.createElement("div");
    crystalsEl.id = "reward-crystals";
    crystalsEl.textContent = `+${reward.crystalsGained} Crystals`;
    css(crystalsEl, { fontSize: "1.1rem", color: "#44ddff" });
    card.appendChild(crystalsEl);

    if (reward.leveledUp) {
      const lvlUp = document.createElement("div");
      lvlUp.id = "reward-levelup";
      lvlUp.textContent = `Level Up! → Lv ${reward.newLevel}`;
      css(lvlUp, { fontSize: "1.1rem", color: "#ffd700", fontWeight: "700", marginTop: "4px" });
      card.appendChild(lvlUp);
    }

    if (reward.firstClear) {
      const first = document.createElement("div");
      first.textContent = "First Clear!";
      css(first, { fontSize: "0.9rem", color: "#aa88ff", marginTop: "4px" });
      card.appendChild(first);
    }
  } else {
    const msg = document.createElement("div");
    msg.textContent = "Level complete!";
    css(msg, { color: "#88aaff" });
    card.appendChild(msg);
  }

  overlay.appendChild(card);

  const btnContinue = document.createElement("button");
  btnContinue.id = "btn-continue";
  btnContinue.textContent = "Continue";
  css(btnContinue, {
    marginTop: "8px",
    padding: "12px 36px",
    background: "#1a3a2a",
    color: "#55ee88",
    border: "2px solid #33aa66",
    borderRadius: "8px",
    cursor: "pointer",
    fontSize: "16px",
    fontFamily: "sans-serif",
    letterSpacing: "0.05em",
  });
  btnContinue.addEventListener("click", onContinue);
  overlay.appendChild(btnContinue);

  return overlay;
}

/** Campaign defeat overlay (#defeat-overlay). */
export function buildDefeatOverlay(
  onRetry: () => void,
  onMap: () => void,
): HTMLElement {
  const overlay = document.createElement("div");
  overlay.id = "defeat-overlay";
  css(overlay, {
    position: "fixed", inset: "0",
    background: "rgba(0,0,0,0.82)",
    display: "flex", flexDirection: "column",
    alignItems: "center", justifyContent: "center",
    zIndex: "1000",
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    gap: "16px",
  });

  const title = document.createElement("div");
  title.textContent = "Defeat";
  css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#ff4444", letterSpacing: "0.1em" });
  overlay.appendChild(title);

  const btnRetry = document.createElement("button");
  btnRetry.id = "btn-retry";
  btnRetry.textContent = "Retry";
  css(btnRetry, {
    padding: "12px 32px",
    background: "#2a1a1a",
    color: "#ff8888",
    border: "2px solid #aa3333",
    borderRadius: "8px",
    cursor: "pointer",
    fontSize: "15px",
    fontFamily: "sans-serif",
  });
  btnRetry.addEventListener("click", onRetry);
  overlay.appendChild(btnRetry);

  const btnMap = document.createElement("button");
  btnMap.id = "btn-map";
  btnMap.textContent = "Map";
  css(btnMap, {
    padding: "12px 32px",
    background: "#1a1a2a",
    color: "#aabbff",
    border: "2px solid #334488",
    borderRadius: "8px",
    cursor: "pointer",
    fontSize: "15px",
    fontFamily: "sans-serif",
  });
  btnMap.addEventListener("click", onMap);
  overlay.appendChild(btnMap);

  return overlay;
}

/** Dungeon pick-a-boon overlay (#pick-overlay). */
export function buildPickOverlay(
  choices: string[],
  onPick: (choiceId: string) => void,
): HTMLElement {
  const overlay = document.createElement("div");
  overlay.id = "pick-overlay";
  css(overlay, {
    position: "fixed", inset: "0",
    background: "rgba(0,0,0,0.88)",
    display: "flex", flexDirection: "column",
    alignItems: "center", justifyContent: "center",
    zIndex: "1000",
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    gap: "20px",
  });

  const title = document.createElement("div");
  title.textContent = "Floor Cleared — Choose a Boon";
  css(title, { fontSize: "1.6rem", fontWeight: "700", color: "#ffcc44", letterSpacing: "0.08em" });
  overlay.appendChild(title);

  const row = document.createElement("div");
  css(row, { display: "flex", gap: "16px", flexWrap: "wrap", justifyContent: "center" });

  for (const choiceId of choices) {
    const card = document.createElement("div");
    card.setAttribute("data-choice", choiceId);
    css(card, {
      background: "#12122a",
      border: "2px solid #334466",
      borderRadius: "10px",
      padding: "20px 24px",
      minWidth: "140px",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      gap: "10px",
      cursor: "pointer",
      transition: "background 0.15s, border-color 0.15s",
    });
    card.addEventListener("mouseenter", () => {
      card.style.background = "#1e1e44";
      card.style.borderColor = "#5566aa";
    });
    card.addEventListener("mouseleave", () => {
      card.style.background = "#12122a";
      card.style.borderColor = "#334466";
    });

    const icon = document.createElement("img");
    icon.src = buffIcon(choiceId);
    css(icon, { width: "40px", height: "40px", imageRendering: "pixelated" });
    card.appendChild(icon);

    const nameEl = document.createElement("div");
    nameEl.textContent = buffName(choiceId);
    css(nameEl, { fontSize: "14px", fontWeight: "700", textAlign: "center", color: "#aabbff" });
    card.appendChild(nameEl);

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
  const overlay = document.createElement("div");
  overlay.id = "dungeon-clear-overlay";
  css(overlay, {
    position: "fixed", inset: "0",
    background: "rgba(0,0,0,0.88)",
    display: "flex", flexDirection: "column",
    alignItems: "center", justifyContent: "center",
    zIndex: "1000",
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    gap: "16px",
  });

  const title = document.createElement("div");
  title.textContent = "Dungeon Cleared!";
  css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#55ee88", letterSpacing: "0.1em" });
  overlay.appendChild(title);

  const card = document.createElement("div");
  css(card, {
    background: "#0e1e14",
    border: "1px solid #226644",
    borderRadius: "12px",
    padding: "24px 36px",
    display: "flex",
    flexDirection: "column",
    gap: "10px",
    alignItems: "center",
    minWidth: "240px",
  });

  const rewardTitle = document.createElement("div");
  rewardTitle.textContent = "Permanent Reward";
  css(rewardTitle, { fontSize: "0.9rem", color: "#88aaaa", letterSpacing: "0.05em" });
  card.appendChild(rewardTitle);

  const profile = data.profile;
  if (profile?.crystals !== undefined) {
    const crystalEl = document.createElement("div");
    crystalEl.id = "dungeon-clear-crystals";
    css(crystalEl, { display: "flex", alignItems: "center", gap: "8px", fontSize: "1.2rem", color: "#44ddff" });
    const gemImg = document.createElement("img");
    gemImg.src = "/art/GemBlue.png";
    css(gemImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    crystalEl.appendChild(gemImg);
    const crystalText = document.createElement("span");
    crystalText.textContent = `${profile.crystals} Crystals`;
    crystalEl.appendChild(crystalText);
    card.appendChild(crystalEl);
  }

  if (profile?.unlockedRelics && Array.isArray(profile.unlockedRelics) && profile.unlockedRelics.length > 0) {
    const lastRelic = profile.unlockedRelics[profile.unlockedRelics.length - 1];
    const relicRow = document.createElement("div");
    css(relicRow, { display: "flex", alignItems: "center", gap: "8px", fontSize: "1.1rem", color: "#cc88ff" });
    const relicImg = document.createElement("img");
    relicImg.src = RELIC_ICONS[lastRelic] ?? "/art/ItemGem.png";
    css(relicImg, { width: "24px", height: "24px", imageRendering: "pixelated" });
    relicRow.appendChild(relicImg);
    const relicText = document.createElement("span");
    relicText.id = "dungeon-clear-relic";
    relicText.textContent = RELIC_NAMES[lastRelic] ?? lastRelic;
    relicRow.appendChild(relicText);
    card.appendChild(relicRow);
  }

  overlay.appendChild(card);

  const doneBtn = document.createElement("button");
  doneBtn.id = "btn-dungeon-done";
  doneBtn.textContent = "Return to Dungeons";
  css(doneBtn, {
    padding: "12px 36px",
    background: "#0e2a1a",
    color: "#55ee88",
    border: "2px solid #226644",
    borderRadius: "8px",
    cursor: "pointer",
    fontSize: "16px",
    fontFamily: "sans-serif",
    letterSpacing: "0.05em",
  });
  doneBtn.addEventListener("click", onDone);
  overlay.appendChild(doneBtn);

  return overlay;
}

/** Dungeon permadeath overlay (#dungeon-fail-overlay). */
export function buildDungeonFailOverlay(onExit: () => void): HTMLElement {
  const overlay = document.createElement("div");
  overlay.id = "dungeon-fail-overlay";
  css(overlay, {
    position: "fixed", inset: "0",
    background: "rgba(0,0,0,0.88)",
    display: "flex", flexDirection: "column",
    alignItems: "center", justifyContent: "center",
    zIndex: "1000",
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    gap: "16px",
  });

  const title = document.createElement("div");
  title.textContent = "Run Over";
  css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#ff4444", letterSpacing: "0.1em" });
  overlay.appendChild(title);

  const sub = document.createElement("div");
  sub.textContent = "The rift claims you.";
  css(sub, { color: "#aa5555", fontSize: "1.1rem", letterSpacing: "0.04em" });
  overlay.appendChild(sub);

  const exitBtn = document.createElement("button");
  exitBtn.id = "btn-dungeon-exit";
  exitBtn.textContent = "Return to Dungeons";
  css(exitBtn, {
    padding: "12px 36px",
    background: "#2a0a0a",
    color: "#ff8888",
    border: "2px solid #aa2222",
    borderRadius: "8px",
    cursor: "pointer",
    fontSize: "16px",
    fontFamily: "sans-serif",
    letterSpacing: "0.05em",
  });
  exitBtn.addEventListener("click", onExit);
  overlay.appendChild(exitBtn);

  return overlay;
}
