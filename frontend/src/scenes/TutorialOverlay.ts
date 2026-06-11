/**
 * TutorialOverlay.ts — First-run hint/tutorial overlay.
 *
 * Uses the HintSystem art: EducationChance, EducationGem, EducationHeroIco,
 * EducationLife, EducationSpellBunner (inline icons) + HintsScreen1/2/3
 * (full-page slides).
 *
 * Shows on first battle and is re-openable from the Settings screen.
 * Persists "seen" state via POST /tutorial/seen.
 */

import { metaApi } from "../net/metaApi";
import { btnInterface } from "../ui/nineSlice";

// ── Tutorial slide data (3 HintSystem full screens + inline icon captions) ───

interface TutorialSlide {
  screenKey: string;   // atlas key for the big background art
  screenSrc: string;   // committed public/hints/ path for <img> src
  title: string;
  caption: string;
  icons: Array<{ src: string; label: string }>;
}

// All hint PNGs are committed to public/hints/ (no /Sprites/ symlink dependency).
const SLIDES: TutorialSlide[] = [
  {
    screenKey: "hints/HintsScreen1",
    screenSrc: "/hints/HintsScreen1.png",
    title: "Move & Serve",
    caption: "Drag your paddle left/right to deflect the ball. Tap the screen to serve at the start.",
    icons: [
      { src: "/hints/EducationHeroIco.png", label: "Your hero paddle" },
    ],
  },
  {
    screenKey: "hints/HintsScreen2",
    screenSrc: "/hints/HintsScreen2.png",
    title: "Spells & Mana",
    caption: "Tap hotbar slots (Q/E/W/R) to cast spells. Each spell costs mana — watch the blue bar!",
    icons: [
      { src: "/hints/EducationSpellBunner.png", label: "Spell banner" },
      { src: "/hints/EducationLife.png",         label: "Life indicator" },
    ],
  },
  {
    screenKey: "hints/HintsScreen3",
    screenSrc: "/hints/HintsScreen3.png",
    title: "Bonuses & Boss",
    caption: "Catch falling bonuses to power up. Clear all blocks to meet the boss — and defeat it!",
    icons: [
      { src: "/hints/EducationGem.png",    label: "Gem bonus" },
      { src: "/hints/EducationChance.png", label: "Chance bonus" },
    ],
  },
];

// ── Public entry points ───────────────────────────────────────────────────────

/**
 * Mount a tutorial overlay on top of the battle/host element.
 * Resolves immediately if the tutorial has already been seen
 * (profile.tutorialSeen = true) UNLESS force = true.
 */
export async function maybeShowTutorial(host: HTMLElement, force = false): Promise<void> {
  if (!force) {
    try {
      const profile = await metaApi.getProfile();
      if (profile.tutorialSeen) return;
    } catch {
      // If backend unreachable, still show tutorial for first time
      const seen = localStorage.getItem("arkanoid_tutorial_seen");
      if (seen === "1") return;
    }
  }
  return new Promise<void>((resolve) => {
    showTutorial(host, () => {
      localStorage.setItem("arkanoid_tutorial_seen", "1");
      metaApi.markTutorialSeen().catch(() => {/* non-fatal */});
      resolve();
    });
  });
}

/** Immediately mount the tutorial overlay (used by Settings "Replay Tutorial"). */
export function showTutorial(host: HTMLElement, onDone?: () => void) {
  injectTutorialStyles();

  const overlay = document.createElement("div");
  overlay.id = "tutorial-overlay";
  overlay.className = "tut-overlay";

  let currentSlide = 0;

  function render() {
    overlay.innerHTML = "";

    const slide = SLIDES[currentSlide];
    const isLast = currentSlide === SLIDES.length - 1;

    // Backdrop image (the full HintScreen art)
    const bgImg = document.createElement("img");
    bgImg.src = slide.screenSrc;
    bgImg.className = "tut-screen-img";
    bgImg.alt = slide.title;
    overlay.appendChild(bgImg);

    // Content panel overlay
    const panel = document.createElement("div");
    panel.className = "tut-panel";
    panel.setAttribute("role", "dialog");
    panel.setAttribute("aria-modal", "true");
    panel.setAttribute("aria-labelledby", "tutorial-title");

    const title = document.createElement("h2");
    title.textContent = slide.title;
    title.className = "tut-title";
    title.id = "tutorial-title";
    panel.appendChild(title);

    const caption = document.createElement("p");
    caption.textContent = slide.caption;
    caption.className = "tut-caption";
    panel.appendChild(caption);

    // Inline icon row
    if (slide.icons.length > 0) {
      const iconRow = document.createElement("div");
      iconRow.className = "tut-icon-row";
      for (const ic of slide.icons) {
        const wrap = document.createElement("div");
        wrap.className = "tut-icon-wrap";
        const img = document.createElement("img");
        img.src = ic.src;
        img.alt = ic.label;
        img.className = "tut-icon-img";
        wrap.appendChild(img);
        const lbl = document.createElement("span");
        lbl.textContent = ic.label;
        lbl.className = "tut-icon-label";
        wrap.appendChild(lbl);
        iconRow.appendChild(wrap);
      }
      panel.appendChild(iconRow);
    }

    // Progress dots
    const dots = document.createElement("div");
    dots.className = "tut-dots";
    for (let i = 0; i < SLIDES.length; i++) {
      const dot = document.createElement("span");
      dot.className = `tut-dot ${i === currentSlide ? "active" : ""}`;
      dots.appendChild(dot);
    }
    panel.appendChild(dots);

    // Navigation buttons
    const btnRow = document.createElement("div");
    btnRow.className = "tut-btn-row";

    if (currentSlide > 0) {
      const btnBack = document.createElement("button");
      btnBack.textContent = "← Back";
      btnBack.className = "tut-btn tut-btn-secondary";
      btnBack.addEventListener("click", () => { currentSlide--; render(); });
      btnRow.appendChild(btnBack);
    } else {
      const spacer = document.createElement("div");
      spacer.style.flex = "1";
      btnRow.appendChild(spacer);
    }

    const btnNext = document.createElement("button");
    btnNext.id = isLast ? "tut-btn-done" : "tut-btn-next";
    btnNext.textContent = isLast ? "Got it!" : "Next →";
    btnNext.className = `tut-btn ${isLast ? "tut-btn-done" : "tut-btn-primary"}`;
    btnNext.addEventListener("click", () => {
      if (isLast) {
        overlay.remove();
        onDone?.();
      } else {
        currentSlide++;
        render();
      }
    });
    btnRow.appendChild(btnNext);

    panel.appendChild(btnRow);
    overlay.appendChild(panel);

    // "Skip" link
    const skip = document.createElement("button");
    skip.textContent = "Skip tutorial";
    skip.className = "tut-skip";
    skip.addEventListener("click", () => {
      overlay.remove();
      onDone?.();
    });
    overlay.appendChild(skip);
  }

  render();
  host.appendChild(overlay);

  // Focus first focusable element for keyboard/screen-reader accessibility
  const firstBtn = overlay.querySelector<HTMLElement>('button, a, input, [tabindex="0"]');
  firstBtn?.focus();
}

// ── Styles ────────────────────────────────────────────────────────────────────

function injectTutorialStyles() {
  const id = "tutorial-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .tut-overlay {
      position: absolute; inset: 0;
      background: rgba(0,0,0,0.88);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      z-index: 2000;
      font-family: sans-serif;
      padding: max(env(safe-area-inset-top,0px),12px) 16px max(env(safe-area-inset-bottom,0px),12px);
      box-sizing: border-box;
      gap: 12px;
      overflow: hidden;
    }

    .tut-screen-img {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      object-fit: cover;
      opacity: 0.22;
      pointer-events: none;
      image-rendering: pixelated;
    }

    .tut-panel {
      position: relative;
      z-index: 1;
      background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover,
                  rgba(8,6,20,0.96);
      border: 2px solid var(--gold-dim);
      border-radius: 16px;
      padding: 28px 24px 20px;
      width: min(340px, 92cqw);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      box-shadow: 0 8px 40px rgba(0,0,0,0.8), inset 0 0 40px rgba(10,5,30,0.6);
    }
    .tut-panel::before, .tut-panel::after {
      content: '';
      position: absolute;
      left: 0; right: 0; height: 16px;
      background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
    }
    .tut-panel::before { top: 0; border-radius: 14px 14px 0 0; }
    .tut-panel::after  { bottom: 0; border-radius: 0 0 14px 14px; }

    .tut-title {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 800;
      color: var(--gold-bright);
      letter-spacing: 0.07em;
      text-shadow: 0 0 16px rgba(255,200,50,0.5), 0 2px 4px rgba(0,0,0,0.9);
      text-align: center;
    }

    .tut-caption {
      margin: 0;
      font-size: 0.95rem;
      color: var(--text-dim);
      line-height: 1.55;
      text-align: center;
      max-width: 300px;
    }

    .tut-icon-row {
      display: flex;
      gap: 20px;
      justify-content: center;
      flex-wrap: wrap;
    }

    .tut-icon-wrap {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
    }

    .tut-icon-img {
      width: 56px;
      height: 56px;
      image-rendering: pixelated;
      filter: drop-shadow(0 2px 6px rgba(0,0,0,0.8));
    }

    .tut-icon-label {
      font-size: 11px;
      color: var(--text-dim);
      text-align: center;
    }

    .tut-dots {
      display: flex;
      gap: 8px;
      justify-content: center;
      align-items: center;
      min-height: 44px;
      padding: 18px 0;
    }

    .tut-dot {
      width: 8px; height: 8px;
      border-radius: 50%;
      background: rgba(200,180,100,0.3);
      border: 1px solid rgba(200,180,100,0.5);
      transition: background 0.2s;
    }
    .tut-dot.active {
      background: var(--gold-bright);
      box-shadow: 0 0 6px rgba(255,210,0,0.6);
    }

    .tut-btn-row {
      display: flex;
      width: 100%;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      margin-top: 4px;
    }

    .tut-btn {
      height: 48px;
      min-width: 100px;
      ${btnInterface()}
      cursor: pointer;
      font-family: sans-serif;
      font-size: 15px;
      font-weight: 700;
      letter-spacing: 0.04em;
      color: var(--text);
      text-shadow: 0 1px 3px rgba(0,0,0,0.9);
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
      transition: filter var(--dur-normal), transform var(--dur-fast);
    }
    .tut-btn:hover  { filter: brightness(1.15); }
    .tut-btn:active { transform: scale(0.97); filter: brightness(0.9); }

    .tut-btn-secondary {
      opacity: 0.75;
      min-width: 80px;
    }

    .tut-btn-done {
      /* primary button keeps full opacity (base .tut-btn already 9-slices the frame) */
      filter: brightness(1.05);
    }

    .tut-skip {
      position: relative;
      z-index: 1;
      background: none;
      border: none;
      color: rgba(200,180,140,0.55);
      font-size: 12px;
      cursor: pointer;
      padding: 8px;
      font-family: sans-serif;
      -webkit-tap-highlight-color: transparent;
    }
    .tut-skip:hover { color: rgba(200,180,140,0.85); }
  `;
  document.head.appendChild(style);
}
