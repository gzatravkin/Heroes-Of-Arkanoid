const LEVELS: { id: string; label: string }[] = [
  { id: "hell-1",        label: "Hell I" },
  { id: "hell-teleport", label: "Hell — Teleporters" },
  { id: "caverns-1",     label: "Caverns I" },
  { id: "village-1",     label: "Witchland I" },
  { id: "village-ghost", label: "Witchland — Ghosts" },
  { id: "heaven-1",      label: "Heaven I" },
];

export function mountMenu(host: HTMLElement) {
  const el = document.createElement("div");
  el.id = "menu";
  el.style.cssText = [
    "color:#e8e8ff",
    "font-family:sans-serif",
    "text-align:center",
    "padding-top:12vh",
    "display:flex",
    "flex-direction:column",
    "align-items:center",
    "gap:0",
  ].join(";");

  const h1 = document.createElement("h1");
  h1.textContent = "ARKANOID RPG";
  h1.style.cssText = "margin:0 0 8px 0;font-size:2.4rem;letter-spacing:0.08em";
  el.appendChild(h1);

  const sub = document.createElement("p");
  sub.textContent = "Choose a level";
  sub.style.cssText = "margin:0 0 24px 0;color:#8899cc;font-size:0.95rem;letter-spacing:0.05em";
  el.appendChild(sub);

  const grid = document.createElement("div");
  grid.style.cssText = [
    "display:grid",
    "grid-template-columns:1fr 1fr",
    "gap:12px",
    "width:360px",
  ].join(";");

  LEVELS.forEach((lvl, i) => {
    const btn = document.createElement("button");
    btn.setAttribute("data-level", lvl.id);
    btn.textContent = lvl.label;
    // Keep id="btn-play" on the first button so existing menu test still passes.
    if (i === 0) btn.id = "btn-play";
    btn.style.cssText = [
      "font-size:15px",
      "padding:12px 10px",
      "background:#1a1a2e",
      "color:#e8e8ff",
      "border:1px solid #334",
      "border-radius:6px",
      "cursor:pointer",
      "transition:background 0.15s,border-color 0.15s",
    ].join(";");
    btn.addEventListener("mouseenter", () => {
      btn.style.background = "#2a2a4e";
      btn.style.borderColor = "#556";
    });
    btn.addEventListener("mouseleave", () => {
      btn.style.background = "#1a1a2e";
      btn.style.borderColor = "#334";
    });
    btn.addEventListener("click", () => {
      location.search = `?scene=battle&level=${lvl.id}`;
    });
    grid.appendChild(btn);
  });

  el.appendChild(grid);

  // Campaign button — navigate to campaign scene
  const campaignBtn = document.createElement("button");
  campaignBtn.id = "btn-campaign";
  campaignBtn.textContent = "Campaign";
  campaignBtn.style.cssText = [
    "margin-top:20px",
    "font-size:15px",
    "padding:12px 32px",
    "background:#1a1a3a",
    "color:#cc88ff",
    "border:1px solid #553377",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.05em",
    "transition:background 0.15s,border-color 0.15s",
  ].join(";");
  campaignBtn.addEventListener("mouseenter", () => {
    campaignBtn.style.background = "#2a1a4a";
    campaignBtn.style.borderColor = "#775599";
  });
  campaignBtn.addEventListener("mouseleave", () => {
    campaignBtn.style.background = "#1a1a3a";
    campaignBtn.style.borderColor = "#553377";
  });
  campaignBtn.addEventListener("click", () => {
    location.href = "/?scene=campaign";
  });
  el.appendChild(campaignBtn);

  // Characters button — navigate to character select scene
  const charactersBtn = document.createElement("button");
  charactersBtn.id = "btn-characters";
  charactersBtn.textContent = "Characters";
  charactersBtn.style.cssText = [
    "margin-top:10px",
    "font-size:15px",
    "padding:12px 32px",
    "background:#120a22",
    "color:#cc88ff",
    "border:1px solid #553366",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.05em",
    "transition:background 0.15s,border-color 0.15s",
  ].join(";");
  charactersBtn.addEventListener("mouseenter", () => {
    charactersBtn.style.background = "#1e1040";
    charactersBtn.style.borderColor = "#885599";
  });
  charactersBtn.addEventListener("mouseleave", () => {
    charactersBtn.style.background = "#120a22";
    charactersBtn.style.borderColor = "#553366";
  });
  charactersBtn.addEventListener("click", () => {
    location.href = "/?scene=characters";
  });
  el.appendChild(charactersBtn);

  // Dungeons button — navigate to dungeons scene
  const dungeonsBtn = document.createElement("button");
  dungeonsBtn.id = "btn-dungeons";
  dungeonsBtn.textContent = "Dungeons";
  dungeonsBtn.style.cssText = [
    "margin-top:10px",
    "font-size:15px",
    "padding:12px 32px",
    "background:#1a0a2a",
    "color:#aa66ff",
    "border:1px solid #442266",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.05em",
    "transition:background 0.15s,border-color 0.15s",
  ].join(";");
  dungeonsBtn.addEventListener("mouseenter", () => {
    dungeonsBtn.style.background = "#2a1a4a";
    dungeonsBtn.style.borderColor = "#664488";
  });
  dungeonsBtn.addEventListener("mouseleave", () => {
    dungeonsBtn.style.background = "#1a0a2a";
    dungeonsBtn.style.borderColor = "#442266";
  });
  dungeonsBtn.addEventListener("click", () => {
    location.href = "/?scene=dungeons";
  });
  el.appendChild(dungeonsBtn);

  // Editor button — navigate to level editor
  const editorBtn = document.createElement("button");
  editorBtn.id = "btn-editor";
  editorBtn.textContent = "Level Editor";
  editorBtn.style.cssText = [
    "margin-top:10px",
    "font-size:15px",
    "padding:12px 32px",
    "background:#0d1a2a",
    "color:#66ccff",
    "border:1px solid #224466",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.05em",
    "transition:background 0.15s,border-color 0.15s",
  ].join(";");
  editorBtn.addEventListener("mouseenter", () => {
    editorBtn.style.background = "#1a2a3a";
    editorBtn.style.borderColor = "#3366aa";
  });
  editorBtn.addEventListener("mouseleave", () => {
    editorBtn.style.background = "#0d1a2a";
    editorBtn.style.borderColor = "#224466";
  });
  editorBtn.addEventListener("click", () => {
    location.href = "/?scene=editor";
  });
  el.appendChild(editorBtn);

  host.appendChild(el);
}
