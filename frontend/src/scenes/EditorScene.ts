import { metaApi } from "../net/metaApi";
import type { BlockTypeDef } from "../net/metaApi";
import { ART_BASE, DEFAULT_COLS, DEFAULT_ROWS, LEGEND_CHARS, btn } from "./editor/editorUtils";

// ── Main export ───────────────────────────────────────────────────────────────

export function mountEditor(host: HTMLElement) {
  // ── State ──────────────────────────────────────────────────────────────────
  let cols = DEFAULT_COLS;
  let rows = DEFAULT_ROWS;
  let selectedType = "."; // "." = eraser
  let blockTypes: BlockTypeDef[] = [];
  // grid: rows×cols, each cell is a blocktype id or "." for empty
  let grid: string[][] = Array.from({ length: rows }, () => Array(cols).fill("."));

  // ── Root container ─────────────────────────────────────────────────────────
  const root = document.createElement("div");
  root.id = "editor";
  root.style.cssText = [
    "display:flex",
    "flex-direction:column",
    "align-items:center",
    "padding:20px",
    "color:#e8e8ff",
    "font-family:var(--font-body)",
    // The editor is taller than a phone screen — it must own its scrolling,
    // because the scene host clips overflow (the original mobile Save bug).
    "height:100cqh",
    "overflow-y:auto",
    "background:#0d0d1a",
    "box-sizing:border-box",
  ].join(";");

  // title
  const title = document.createElement("h2");
  title.textContent = "Level Editor";
  title.style.cssText = "margin:0 0 12px 0;font-size:1.6rem;letter-spacing:0.08em";
  root.appendChild(title);

  // ── Top controls row ────────────────────────────────────────────────────────
  const controls = document.createElement("div");
  controls.style.cssText = [
    "display:flex",
    "gap:14px",
    "align-items:center",
    "flex-wrap:wrap",
    "margin-bottom:14px",
  ].join(";");

  // Level id input
  const idLabel = document.createElement("label");
  idLabel.textContent = "Level ID: ";
  idLabel.style.cssText = "font-size:var(--fs-body);color:#aab";
  const idInput = document.createElement("input");
  idInput.id = "editor-id";
  idInput.type = "text";
  idInput.placeholder = "e.g. my-level-1";
  idInput.style.cssText = [
    "background:#1a1a2e",
    "color:#e8e8ff",
    "border:1px solid #445",
    "border-radius:4px",
    "padding:4px 8px",
    "font-size:var(--fs-body)",
    "width:160px",
  ].join(";");
  idLabel.appendChild(idInput);
  controls.appendChild(idLabel);

  // Biome selector
  const biomeLabel = document.createElement("label");
  biomeLabel.textContent = "Biome: ";
  biomeLabel.style.cssText = "font-size:var(--fs-body);color:#aab";
  const biomeSelect = document.createElement("select");
  biomeSelect.id = "editor-biome";
  biomeSelect.style.cssText = [
    "background:#1a1a2e",
    "color:#e8e8ff",
    "border:1px solid #445",
    "border-radius:4px",
    "padding:4px 8px",
    "font-size:var(--fs-body)",
  ].join(";");
  ["hell", "cavern", "village", "heaven"].forEach(b => {
    const opt = document.createElement("option");
    opt.value = b;
    opt.textContent = b;
    biomeSelect.appendChild(opt);
  });
  biomeLabel.appendChild(biomeSelect);
  controls.appendChild(biomeLabel);

  // Cols / rows
  function makeNumInput(labelText: string, defaultVal: number, id: string): [HTMLLabelElement, HTMLInputElement] {
    const lbl = document.createElement("label");
    lbl.textContent = labelText + " ";
    lbl.style.cssText = "font-size:var(--fs-body);color:#aab";
    const inp = document.createElement("input");
    inp.id = id;
    inp.type = "number";
    inp.value = String(defaultVal);
    inp.min = "1";
    inp.max = "24";
    inp.style.cssText = [
      "background:#1a1a2e",
      "color:#e8e8ff",
      "border:1px solid #445",
      "border-radius:4px",
      "padding:4px 6px",
      "font-size:var(--fs-body)",
      "width:54px",
    ].join(";");
    lbl.appendChild(inp);
    return [lbl, inp];
  }

  const [colsLabel, colsInput] = makeNumInput("Cols:", DEFAULT_COLS, "editor-cols");
  const [rowsLabel, rowsInput] = makeNumInput("Rows:", DEFAULT_ROWS, "editor-rows");
  controls.appendChild(colsLabel);
  controls.appendChild(rowsLabel);

  // Load existing level by id
  const loadBtn = btn("Load", "btn-editor-load", [
    "font-size:var(--fs-body)",
    "padding:5px 14px",
    "background:#1a2a1a",
    "color:#88ff88",
    "border:1px solid #336633",
    "border-radius:4px",
    "cursor:pointer",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id) { showStatus("Enter a level ID to load.", true); return; }
    try {
      const level = await metaApi.loadLevel(id);
      cols = level.cols;
      rows = level.rows;
      colsInput.value = String(cols);
      rowsInput.value = String(rows);
      biomeSelect.value = level.biome;
      // Rebuild grid from rows_data + legend (invert legend: char → id)
      const charToId: Record<string, string> = {};
      for (const [ch, id] of Object.entries(level.legend)) charToId[ch] = id;
      grid = level.rows_data.map(row =>
        row.split("").map(ch => (ch === "." ? "." : (charToId[ch] ?? ".")))
      );
      rebuildGrid();
      showStatus(`Loaded "${id}".`);
    } catch {
      showStatus("Level not found or load failed.", true);
    }
  });
  controls.appendChild(loadBtn);

  root.appendChild(controls);

  // ── Main layout: palette + grid ────────────────────────────────────────────
  // flex-wrap lets the palette stack ABOVE the grid on phone widths (390px)
  // instead of overflowing horizontally and pushing the actions off-viewport.
  const layout = document.createElement("div");
  layout.style.cssText = [
    "display:flex",
    "flex-wrap:wrap",
    "gap:14px",
    "align-items:flex-start",
    "justify-content:center",
    "width:100%",
    "max-width:900px",
  ].join(";");

  // Palette — a wrapping chip strip on narrow screens, a column on wide ones.
  const palette = document.createElement("div");
  palette.id = "editor-palette";
  palette.style.cssText = [
    "display:flex",
    "flex-direction:row",
    "flex-wrap:wrap",
    "gap:4px",
    "min-width:0",
    "max-width:100%",
    "background:#12122a",
    "border:1px solid #334",
    "border-radius:6px",
    "padding:8px",
  ].join(";");

  const paletteTitle = document.createElement("div");
  paletteTitle.textContent = "Palette";
  paletteTitle.style.cssText = "font-size:var(--fs-caption);color:#889;margin-bottom:4px;letter-spacing:0.05em;text-transform:uppercase;width:100%";
  palette.appendChild(paletteTitle);

  layout.appendChild(palette);

  // Grid area
  const gridWrap = document.createElement("div");
  gridWrap.style.cssText = "flex:1;display:flex;flex-direction:column;gap:8px";

  const gridEl = document.createElement("div");
  gridEl.id = "editor-grid";
  gridEl.style.cssText = "display:inline-block;border:1px solid #334;border-radius:4px;line-height:0";
  gridWrap.appendChild(gridEl);
  layout.appendChild(gridWrap);
  root.appendChild(layout);

  // ── Bottom action buttons ──────────────────────────────────────────────────
  const actions = document.createElement("div");
  actions.style.cssText = "display:flex;flex-wrap:wrap;gap:12px;margin-top:14px;align-items:center;justify-content:center";

  const saveBtn = btn("Save Level", "btn-editor-save", [
    "font-size:var(--fs-subhead)",
    "padding:8px 22px",
    "background:#1a1a3a",
    "color:#88aaff",
    "border:1px solid #334477",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.04em",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) {
      showStatus("Level ID must match ^[a-z0-9-]+$", true);
      return;
    }
    const { rowsData, legend } = buildLevelData();
    try {
      const res = await metaApi.saveLevel({
        id,
        biome: biomeSelect.value,
        cols,
        rows,
        rows_data: rowsData,
        legend,
      });
      if (res.ok) showStatus(`Saved "${res.id}" successfully.`);
      else showStatus("Save failed.", true);
    } catch {
      showStatus("Save request failed.", true);
    }
  });
  actions.appendChild(saveBtn);

  const playBtn = btn("Test Play", "btn-editor-play", [
    "font-size:var(--fs-subhead)",
    "padding:8px 22px",
    "background:#1a3a1a",
    "color:#88ff88",
    "border:1px solid #336633",
    "border-radius:6px",
    "cursor:pointer",
    "letter-spacing:0.04em",
  ].join(";"), async () => {
    const id = idInput.value.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) {
      showStatus("Level ID must match ^[a-z0-9-]+$", true);
      return;
    }
    const { rowsData, legend } = buildLevelData();
    try {
      await metaApi.saveLevel({
        id,
        biome: biomeSelect.value,
        cols,
        rows,
        rows_data: rowsData,
        legend,
      });
      location.search = `?scene=battle&level=${encodeURIComponent(id)}`;
    } catch {
      showStatus("Save failed before play.", true);
    }
  });
  actions.appendChild(playBtn);

  const backBtn = btn("Back to Menu", "btn-editor-back", [
    "font-size:var(--fs-subhead)",
    "padding:8px 18px",
    "background:#1a1a1a",
    "color:#aaa",
    "border:1px solid #333",
    "border-radius:6px",
    "cursor:pointer",
  ].join(";"), () => { location.href = "/?scene=menu"; });
  actions.appendChild(backBtn);

  const statusEl = document.createElement("span");
  statusEl.id = "editor-status";
  statusEl.style.cssText = "font-size:var(--fs-body);margin-left:8px;color:#88ff88";
  actions.appendChild(statusEl);

  root.appendChild(actions);
  host.appendChild(root);

  // ── Status helper ──────────────────────────────────────────────────────────
  function showStatus(msg: string, error = false) {
    statusEl.textContent = msg;
    statusEl.style.color = error ? "#ff6666" : "#88ff88";
    setTimeout(() => { if (statusEl.textContent === msg) statusEl.textContent = ""; }, 3000);
  }

  // ── Grid interaction ───────────────────────────────────────────────────────
  let painting = false;

  function paintCell(cellEl: HTMLElement) {
    const c = parseInt(cellEl.getAttribute("data-col")!);
    const r = parseInt(cellEl.getAttribute("data-row")!);
    if (isNaN(c) || isNaN(r)) return;
    grid[r][c] = selectedType;
    updateCellDisplay(cellEl, selectedType);
  }

  function updateCellDisplay(cellEl: HTMLElement, typeId: string) {
    const bt = blockTypes.find(b => b.id === typeId);
    cellEl.style.backgroundImage = bt ? `url(${ART_BASE}${bt.sprite}.png)` : "none";
    cellEl.style.backgroundColor = bt ? "transparent" : "#0d0d1a";
    cellEl.style.backgroundSize = "cover";
  }

  function rebuildGrid() {
    gridEl.innerHTML = "";
    gridEl.style.gridTemplateColumns = `repeat(${cols}, 36px)`;
    gridEl.style.display = "grid";

    for (let r = 0; r < rows; r++) {
      if (!grid[r]) grid[r] = Array(cols).fill(".");
      for (let c = 0; c < cols; c++) {
        if (grid[r][c] === undefined) grid[r][c] = ".";
        const cell = document.createElement("div");
        cell.setAttribute("data-col", String(c));
        cell.setAttribute("data-row", String(r));
        cell.style.cssText = [
          "width:36px",
          "height:24px",
          "box-sizing:border-box",
          "border:1px solid #223",
          "cursor:crosshair",
          "background-size:cover",
          "background-position:center",
        ].join(";");

        updateCellDisplay(cell, grid[r][c]);

        cell.addEventListener("mousedown", (e) => {
          e.preventDefault();
          painting = true;
          paintCell(cell);
        });
        cell.addEventListener("mouseenter", () => {
          if (painting) paintCell(cell);
        });
        gridEl.appendChild(cell);
      }
    }

    window.addEventListener("mouseup", () => { painting = false; }, { once: false });
  }

  // ── Dimension change handlers ──────────────────────────────────────────────
  function onDimsChange() {
    const nc = Math.max(1, Math.min(24, parseInt(colsInput.value) || DEFAULT_COLS));
    const nr = Math.max(1, Math.min(24, parseInt(rowsInput.value) || DEFAULT_ROWS));
    // Preserve existing cells
    const oldGrid = grid;
    const newGrid: string[][] = Array.from({ length: nr }, (_, r) =>
      Array.from({ length: nc }, (_, c) => oldGrid[r]?.[c] ?? ".")
    );
    cols = nc;
    rows = nr;
    grid = newGrid;
    rebuildGrid();
  }

  colsInput.addEventListener("change", onDimsChange);
  rowsInput.addEventListener("change", onDimsChange);

  // ── Build level data from grid ─────────────────────────────────────────────
  function buildLevelData(): { rowsData: string[]; legend: Record<string, string> } {
    // Collect distinct used block ids (not ".")
    const usedIds = Array.from(new Set(grid.flat().filter(id => id !== ".")));
    const legend: Record<string, string> = {};
    const idToChar: Record<string, string> = {};
    usedIds.forEach((id, i) => {
      const ch = LEGEND_CHARS[i % LEGEND_CHARS.length];
      legend[ch] = id;
      idToChar[id] = ch;
    });

    const rowsData = grid.map(row =>
      row.map(cell => (cell === "." ? "." : (idToChar[cell] ?? "."))).join("")
    );

    return { rowsData, legend };
  }

  // ── Palette rendering ──────────────────────────────────────────────────────
  function renderPalette() {
    // Remove old swatches (keep title)
    while (palette.children.length > 1) palette.removeChild(palette.lastChild!);

    // Eraser swatch
    const eraserSwatch = makeSwatch(".", "Eraser", null);
    palette.appendChild(eraserSwatch);

    // Group by biome
    const biomes = Array.from(new Set(blockTypes.map(b => b.biome)));
    biomes.forEach(biome => {
      const bLabel = document.createElement("div");
      bLabel.textContent = biome.toUpperCase();
      bLabel.style.cssText = "font-size:var(--fs-tiny);color:#667;margin-top:6px;margin-bottom:2px;letter-spacing:0.06em;width:100%";
      palette.appendChild(bLabel);

      blockTypes.filter(b => b.biome === biome).forEach(bt => {
        const swatch = makeSwatch(bt.id, bt.id, bt.sprite);
        palette.appendChild(swatch);
      });
    });
  }

  function makeSwatch(typeId: string, label: string, sprite: string | null): HTMLElement {
    const sw = document.createElement("div");
    sw.setAttribute("data-blocktype", typeId);
    sw.title = label;
    sw.style.cssText = [
      "display:flex",
      "align-items:center",
      "gap:6px",
      "padding:3px 6px",
      "border-radius:4px",
      "cursor:pointer",
      "border:1px solid transparent",
      "font-size:var(--fs-small)",
      "color:#ccc",
      "white-space:nowrap",
      "overflow:hidden",
      "text-overflow:ellipsis",
    ].join(";");

    if (sprite) {
      const img = document.createElement("img");
      img.src = `${ART_BASE}${sprite}.png`;
      img.style.cssText = "width:24px;height:16px;object-fit:cover;border-radius:2px;flex-shrink:0";
      img.onerror = () => { img.style.display = "none"; };
      sw.appendChild(img);
    } else {
      // Eraser icon
      const ico = document.createElement("div");
      ico.textContent = "✕";
      ico.style.cssText = "width:24px;height:16px;display:flex;align-items:center;justify-content:center;font-size:var(--fs-caption);flex-shrink:0;color:#ff7777";
      sw.appendChild(ico);
    }

    const lbl = document.createElement("span");
    lbl.textContent = typeId === "." ? "Eraser" : typeId;
    sw.appendChild(lbl);

    sw.addEventListener("click", () => {
      selectedType = typeId;
      updatePaletteSelection();
    });

    return sw;
  }

  function updatePaletteSelection() {
    palette.querySelectorAll<HTMLElement>("[data-blocktype]").forEach(sw => {
      const active = sw.getAttribute("data-blocktype") === selectedType;
      sw.style.borderColor = active ? "#7799ff" : "transparent";
      sw.style.background   = active ? "#1a2a4a" : "transparent";
    });
  }

  // ── Init ───────────────────────────────────────────────────────────────────
  (async () => {
    try {
      blockTypes = await metaApi.getBlockTypes();
    } catch {
      blockTypes = [];
    }
    renderPalette();
    rebuildGrid();
    updatePaletteSelection();
  })();

  // Prevent page-level mouseup from being lost
  document.addEventListener("mouseup", () => { painting = false; });
}
