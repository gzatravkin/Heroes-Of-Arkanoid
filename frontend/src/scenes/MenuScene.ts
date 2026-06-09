import { navigateTo } from "../ui/transition";

const LEVELS: { id: string; label: string }[] = [
  { id: "hell-1",        label: "Hell I" },
  { id: "hell-teleport", label: "Hell — Teleporters" },
  { id: "caverns-1",     label: "Caverns I" },
  { id: "village-1",     label: "Witchland I" },
  { id: "village-ghost", label: "Witchland — Ghosts" },
  { id: "heaven-1",      label: "Heaven I" },
];

export function mountMenu(host: HTMLElement) {
  // Inject menu styles
  injectMenuStyles();

  const el = document.createElement("div");
  el.id = "menu";
  el.className = "menu-root";

  // Background fill
  const bg = document.createElement("div");
  bg.className = "menu-bg";
  el.appendChild(bg);

  // Character art (left side decoration)
  const charArt = document.createElement("div");
  charArt.className = "menu-char-art";
  el.appendChild(charArt);

  // Main content column
  const col = document.createElement("div");
  col.className = "menu-col";

  // Logo image (visual) + screen-reader / test h1
  const h1 = document.createElement("h1");
  h1.textContent = "ARKANOID RPG";
  h1.style.cssText = "position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;";
  col.appendChild(h1);

  const logo = document.createElement("div");
  logo.className = "menu-logo";
  col.appendChild(logo);

  // Nav buttons (big art buttons)
  const navSection = document.createElement("div");
  navSection.className = "menu-nav";

  // Play button — styled with PlayButton art, keeps id="btn-play" + data-level="hell-1"
  const playBtn = document.createElement("button");
  playBtn.id = "btn-play";
  playBtn.setAttribute("data-level", "hell-1");
  playBtn.className = "menu-art-btn menu-btn-play";
  playBtn.innerHTML = `<span class="menu-btn-label">Play</span>`;
  playBtn.addEventListener("click", () => {
    navigateTo(`/?scene=battle&level=hell-1`);
  });
  navSection.appendChild(playBtn);

  // Campaign
  const campaignBtn = document.createElement("button");
  campaignBtn.id = "btn-campaign";
  campaignBtn.className = "menu-art-btn menu-btn-campaign";
  campaignBtn.innerHTML = `<span class="menu-btn-label">Campaign</span>`;
  campaignBtn.addEventListener("click", () => { navigateTo("/?scene=campaign"); });
  navSection.appendChild(campaignBtn);

  // Characters
  const charactersBtn = document.createElement("button");
  charactersBtn.id = "btn-characters";
  charactersBtn.className = "menu-art-btn menu-btn-characters";
  charactersBtn.innerHTML = `<span class="menu-btn-label">Characters</span>`;
  charactersBtn.addEventListener("click", () => { navigateTo("/?scene=characters"); });
  navSection.appendChild(charactersBtn);

  // Dungeons
  const dungeonsBtn = document.createElement("button");
  dungeonsBtn.id = "btn-dungeons";
  dungeonsBtn.className = "menu-art-btn menu-btn-dungeons";
  dungeonsBtn.innerHTML = `<span class="menu-btn-label">Dungeons</span>`;
  dungeonsBtn.addEventListener("click", () => { navigateTo("/?scene=dungeons"); });
  navSection.appendChild(dungeonsBtn);

  // Inventory / Items
  const inventoryBtn = document.createElement("button");
  inventoryBtn.id = "btn-inventory";
  inventoryBtn.className = "menu-art-btn menu-btn-inventory";
  inventoryBtn.innerHTML = `<span class="menu-btn-label">Items</span>`;
  inventoryBtn.addEventListener("click", () => { navigateTo("/?scene=inventory"); });
  navSection.appendChild(inventoryBtn);

  // Editor
  const editorBtn = document.createElement("button");
  editorBtn.id = "btn-editor";
  editorBtn.className = "menu-art-btn menu-btn-editor";
  editorBtn.innerHTML = `<span class="menu-btn-label">Level Editor</span>`;
  editorBtn.addEventListener("click", () => { navigateTo("/?scene=editor"); });
  navSection.appendChild(editorBtn);

  col.appendChild(navSection);

  // Quick-level grid (hidden below main nav, preserves all [data-level] for tests)
  const quickGrid = document.createElement("div");
  quickGrid.className = "menu-quick-grid";

  LEVELS.forEach((lvl, i) => {
    if (i === 0) return; // hell-1 is already the Play button above
    const btn = document.createElement("button");
    btn.setAttribute("data-level", lvl.id);
    btn.className = "menu-quick-btn";
    btn.textContent = lvl.label;
    btn.addEventListener("click", () => {
      navigateTo(`/?scene=battle&level=${lvl.id}`);
    });
    quickGrid.appendChild(btn);
  });

  col.appendChild(quickGrid);
  el.appendChild(col);
  host.appendChild(el);
}

function injectMenuStyles() {
  const id = "menu-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .menu-root {
      position: relative;
      min-height: 100vh;
      width: 100%;
      overflow: hidden;
      display: flex;
      align-items: stretch;
      font-family: sans-serif;
    }

    /* Dark gradient background with parchment-brown tint matching the game art palette */
    .menu-bg {
      position: absolute;
      inset: 0;
      background:
        radial-gradient(ellipse at 50% 0%, rgba(80,50,20,0.55) 0%, transparent 60%),
        linear-gradient(180deg, #1a0e06 0%, #0d0808 40%, #050308 100%);
      z-index: 0;
    }

    /* Character art positioned right side */
    .menu-char-art {
      position: absolute;
      right: -20px;
      bottom: 0;
      width: min(260px, 60vw);
      height: 70vh;
      background: url('/ui/MainCharacter.png') no-repeat bottom right / contain;
      opacity: 0.18;
      z-index: 1;
      pointer-events: none;
    }

    .menu-col {
      position: relative;
      z-index: 2;
      display: flex;
      flex-direction: column;
      align-items: center;
      width: 100%;
      padding: env(safe-area-inset-top, 0px) 0 env(safe-area-inset-bottom, 16px) 0;
      padding-top: max(env(safe-area-inset-top, 0px), 28px);
      gap: 0;
    }

    /* Logo — the real Heroes of Arkanoid II title image */
    .menu-logo {
      width: min(340px, 88vw);
      height: 80px;
      background: url('/ui/LogoArkanoid.png') no-repeat center / contain;
      margin-bottom: 28px;
    }

    .menu-nav {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      width: 100%;
      padding: 0 24px;
      box-sizing: border-box;
    }

    /* Art button base — uses InterfaceButton (blue-gold pill) as the bg */
    .menu-art-btn {
      position: relative;
      width: min(320px, 88vw);
      height: 56px;
      border: none;
      background: url('/ui/InterfaceButton.png') no-repeat center / 100% 100%;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: filter 0.15s, transform 0.1s;
      -webkit-tap-highlight-color: transparent;
      touch-action: manipulation;
    }
    .menu-art-btn:hover  { filter: brightness(1.15); }
    .menu-art-btn:active { transform: scale(0.97); filter: brightness(0.9); }

    /* Play button — has the PlayButton art icon on the left */
    .menu-btn-play {
      background-image: url('/ui/InterfaceButton.png');
    }
    .menu-btn-play::before {
      content: '';
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      width: 40px;
      height: 40px;
      background: url('/ui/PlayButtonEng.png') no-repeat center / contain;
    }

    /* Campaign — uses the Campaign text art as icon label */
    .menu-btn-campaign::before {
      content: '';
      position: absolute;
      left: 50%;
      top: 50%;
      transform: translate(-50%, -50%);
      width: 80%;
      height: 80%;
      background: url('/ui/InterfaceCampaignENG.png') no-repeat center / contain;
    }
    .menu-btn-campaign .menu-btn-label { opacity: 0; }

    /* Characters — uses Inventory helmet button as icon */
    .menu-btn-characters::before {
      content: '';
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      width: 38px;
      height: 38px;
      background: url('/ui/InventoryButton.png') no-repeat center / contain;
    }

    /* Dungeons — uses skill-arrows button as icon */
    .menu-btn-dungeons::before {
      content: '';
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      width: 38px;
      height: 38px;
      background: url('/ui/InterfaceSkillsButton.png') no-repeat center / contain;
    }

    /* Inventory / Items — uses InventoryButton art as icon */
    .menu-btn-inventory::before {
      content: '';
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      width: 38px;
      height: 38px;
      background: url('/ui/InventoryButton.png') no-repeat center / contain;
    }

    /* Editor — uses new+ button as icon */
    .menu-btn-editor::before {
      content: '';
      position: absolute;
      left: 16px;
      top: 50%;
      transform: translateY(-50%);
      width: 38px;
      height: 38px;
      background: url('/ui/InterfaceNewButton.png') no-repeat center / contain;
    }

    .menu-btn-label {
      color: #f0e0b8;
      font-size: 17px;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-shadow: 0 1px 3px rgba(0,0,0,0.9), 0 0 8px rgba(0,0,0,0.6);
      pointer-events: none;
    }

    /* Quick-level grid — compact, secondary */
    .menu-quick-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      width: min(320px, 88vw);
      margin-top: 16px;
      padding: 0 0 24px 0;
    }
    .menu-quick-btn {
      padding: 10px 8px;
      background: rgba(30,20,10,0.75);
      color: #c8b888;
      border: 1px solid rgba(160,120,50,0.45);
      border-radius: 6px;
      cursor: pointer;
      font-size: 12px;
      font-family: sans-serif;
      letter-spacing: 0.04em;
      transition: background 0.15s, border-color 0.15s;
      min-height: 44px;
      touch-action: manipulation;
    }
    .menu-quick-btn:hover  { background: rgba(50,35,15,0.85); border-color: rgba(200,160,80,0.7); }
    .menu-quick-btn:active { filter: brightness(0.85); }
  `;
  document.head.appendChild(style);
}
