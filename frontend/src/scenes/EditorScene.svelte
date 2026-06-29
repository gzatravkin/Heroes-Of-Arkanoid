<script lang="ts">
  import type { BlockTypeDef } from "../net/metaApi";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import { navigateTo } from "../ui/transition";

  const ART_BASE = "/art/";
  const DEFAULT_COLS = 8;
  const DEFAULT_ROWS = 14;
  const LEGEND_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  const BIOMES = ["hell", "cavern", "village", "heaven"];

  // ── State ─────────────────────────────────────────────────────────────────
  let levelId      = $state("");
  let biome        = $state("hell");
  let cols         = $state(DEFAULT_COLS);
  let rows         = $state(DEFAULT_ROWS);
  let selectedType = $state(".");
  let blockTypes   = $state<BlockTypeDef[]>([]);
  let grid         = $state<string[][]>(
    Array.from({ length: DEFAULT_ROWS }, () => Array(DEFAULT_COLS).fill("."))
  );
  let status       = $state("");
  let statusErr    = $state(false);

  let painting = false; // not reactive — just a pointer-drag flag

  // ── Derived ───────────────────────────────────────────────────────────────
  let biomeGroups = $derived.by(() => {
    const map = new Map<string, BlockTypeDef[]>();
    for (const bt of blockTypes) {
      if (!map.has(bt.biome)) map.set(bt.biome, []);
      map.get(bt.biome)!.push(bt);
    }
    return Array.from(map.entries()).map(([b, types]) => ({ biome: b, types }));
  });

  let rowIdxs = $derived(Array.from({ length: rows }, (_, i) => i));
  let colIdxs = $derived(Array.from({ length: cols }, (_, i) => i));

  // ── Helpers ───────────────────────────────────────────────────────────────
  function showStatus(msg: string, error = false) {
    status = msg; statusErr = error;
    setTimeout(() => { if (status === msg) status = ""; }, 3000);
  }

  function paintCell(c: number, r: number) {
    grid[r][c] = selectedType;
  }

  function onDimsChange(nc: number, nr: number) {
    const old = grid;
    cols = nc; rows = nr;
    grid = Array.from({ length: nr }, (_, r) =>
      Array.from({ length: nc }, (_, c) => old[r]?.[c] ?? ".")
    );
  }

  function buildLevelData() {
    const usedIds = Array.from(new Set(grid.flat().filter(id => id !== ".")));
    const legend: Record<string, string> = {};
    const idToChar: Record<string, string> = {};
    usedIds.forEach((id, i) => {
      const ch = LEGEND_CHARS[i % LEGEND_CHARS.length];
      legend[ch] = id; idToChar[id] = ch;
    });
    const rowsData = grid.map(row =>
      row.map(cell => cell === "." ? "." : (idToChar[cell] ?? ".")).join("")
    );
    return { rowsData, legend };
  }

  // ── Actions ───────────────────────────────────────────────────────────────
  async function loadLevel() {
    const id = levelId.trim();
    if (!id) { showStatus("Enter a level ID to load.", true); return; }
    try {
      const level = await metaApi.loadLevel(id);
      const charToId: Record<string, string> = {};
      for (const [ch, bid] of Object.entries(level.legend)) charToId[ch] = bid;
      cols  = level.cols;
      rows  = level.rows;
      biome = level.biome;
      grid  = level.rows_data.map(row =>
        row.split("").map(ch => ch === "." ? "." : (charToId[ch] ?? "."))
      );
      showStatus(`Loaded "${id}".`);
    } catch {
      showStatus("Level not found or load failed.", true);
    }
  }

  async function saveLevel() {
    const id = levelId.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) { showStatus("Level ID must match ^[a-z0-9-]+$", true); return; }
    const { rowsData, legend } = buildLevelData();
    try {
      const res = await metaApi.saveLevel({ id, biome, cols, rows, rows_data: rowsData, legend });
      if (res.ok) showStatus(`Saved "${res.id}" successfully.`);
      else showStatus("Save failed.", true);
    } catch { showStatus("Save request failed.", true); }
  }

  async function testPlay() {
    const id = levelId.trim();
    if (!id || !/^[a-z0-9-]+$/.test(id)) { showStatus("Level ID must match ^[a-z0-9-]+$", true); return; }
    const { rowsData, legend } = buildLevelData();
    try {
      await metaApi.saveLevel({ id, biome, cols, rows, rows_data: rowsData, legend });
      location.search = `?scene=battle&level=${encodeURIComponent(id)}`;
    } catch { showStatus("Save failed before play.", true); }
  }

  // ── Pointer handlers ──────────────────────────────────────────────────────
  function onCellDown(e: MouseEvent, c: number, r: number) {
    e.preventDefault(); painting = true; paintCell(c, r);
  }
  function onCellEnter(c: number, r: number) {
    if (painting) paintCell(c, r);
  }

  // ── Init ──────────────────────────────────────────────────────────────────
  (async () => {
    try { blockTypes = await metaApi.getBlockTypes(); }
    catch { blockTypes = []; }
  })();
</script>

<svelte:window onmouseup={() => { painting = false; }} />

<div id="editor" class="root">
  <h2 class="title">Level Editor</h2>

  <!-- Controls row -->
  <div class="controls">
    <label class="ctrl-label">Level ID:
      <input id="editor-id" type="text" placeholder="e.g. my-level-1"
             class="ctrl-input" style="width:160px" bind:value={levelId} />
    </label>
    <label class="ctrl-label">Biome:
      <select id="editor-biome" class="ctrl-input" bind:value={biome}>
        {#each BIOMES as b}<option value={b}>{b}</option>{/each}
      </select>
    </label>
    <label class="ctrl-label">Cols:
      <input id="editor-cols" type="number" min="1" max="24" class="ctrl-input ctrl-num"
             value={cols}
             onchange={e => onDimsChange(
               Math.max(1, Math.min(24, parseInt((e.target as HTMLInputElement).value) || DEFAULT_COLS)),
               rows
             )} />
    </label>
    <label class="ctrl-label">Rows:
      <input id="editor-rows" type="number" min="1" max="24" class="ctrl-input ctrl-num"
             value={rows}
             onchange={e => onDimsChange(
               cols,
               Math.max(1, Math.min(24, parseInt((e.target as HTMLInputElement).value) || DEFAULT_ROWS))
             )} />
    </label>
    <button id="btn-editor-load" class="btn btn-load" onclick={loadLevel}>Load</button>
  </div>

  <!-- Palette + grid -->
  <div class="layout">
    <div id="editor-palette" class="palette">
      <div class="palette-title">Palette</div>

      <!-- Eraser -->
      <div class="swatch {selectedType === '.' ? 'selected' : ''}"
           data-blocktype="." title="Eraser"
           role="button" tabindex="0"
           onclick={() => selectedType = "."}
           onkeydown={e => e.key === 'Enter' && (selectedType = ".")}>
        <div class="swatch-eraser">✕</div>
        <span>Eraser</span>
      </div>

      {#each biomeGroups as grp}
        <div class="palette-biome">{grp.biome.toUpperCase()}</div>
        {#each grp.types as bt}
          <div class="swatch {selectedType === bt.id ? 'selected' : ''}"
               data-blocktype={bt.id} title={bt.id}
               role="button" tabindex="0"
               onclick={() => selectedType = bt.id}
               onkeydown={e => e.key === 'Enter' && (selectedType = bt.id)}>
            <img src="{ART_BASE}{bt.sprite}.png" class="swatch-img" alt=""
                 onerror={e => ((e.target as HTMLImageElement).style.display = 'none')} />
            <span>{bt.id}</span>
          </div>
        {/each}
      {/each}
    </div>

    <!-- Grid -->
    <div style="flex:1;display:flex;flex-direction:column;gap:8px">
      <div id="editor-grid" class="grid" style="grid-template-columns:repeat({cols}, 36px)">
        {#each rowIdxs as r}
          {#each colIdxs as c}
            {@const typeId = grid[r]?.[c] ?? "."}
            {@const bt = blockTypes.find(b => b.id === typeId)}
            <div data-col={c} data-row={r} class="cell"
                 style="background-image:{bt ? `url(${ART_BASE}${bt.sprite}.png)` : 'none'};background-color:{bt ? 'transparent' : '#0d0d1a'}"
                 onmousedown={e => onCellDown(e, c, r)}
                 onmouseenter={() => onCellEnter(c, r)}>
            </div>
          {/each}
        {/each}
      </div>
    </div>
  </div>

  <!-- Actions -->
  <div class="actions">
    <button id="btn-editor-save" class="btn btn-save" onclick={saveLevel}>Save Level</button>
    <button id="btn-editor-play" class="btn btn-play" onclick={testPlay}>Test Play</button>
    <button id="btn-editor-back" class="btn btn-back" onclick={() => navigateTo("/?scene=menu")}>Back to Menu</button>
    <span id="editor-status" class="status" style="color:{statusErr ? '#ff6666' : '#88ff88'}">{status}</span>
  </div>
</div>

<style>
  .root {
    display: flex; flex-direction: column; align-items: center;
    padding: 20px; color: #e8e8ff; font-family: var(--font-body);
    height: 100cqh; overflow-y: auto;
    background: #0d0d1a; box-sizing: border-box;
  }
  .title { margin: 0 0 12px 0; font-size: 1.6rem; letter-spacing: 0.08em; }

  /* Controls */
  .controls {
    display: flex; gap: 14px; align-items: center;
    flex-wrap: wrap; margin-bottom: 14px;
  }
  .ctrl-label { font-size: var(--fs-body); color: #aab; }
  .ctrl-input {
    background: #1a1a2e; color: #e8e8ff;
    border: 1px solid #445; border-radius: 4px;
    padding: 4px 8px; font-size: var(--fs-body);
  }
  .ctrl-num { width: 54px; }

  /* Layout */
  .layout {
    display: flex; flex-wrap: wrap; gap: 14px;
    align-items: flex-start; justify-content: center;
    width: 100%; max-width: 900px;
  }

  /* Palette */
  .palette {
    display: flex; flex-direction: row; flex-wrap: wrap;
    gap: 4px; min-width: 0; max-width: 100%;
    background: #12122a; border: 1px solid #334;
    border-radius: 6px; padding: 8px;
  }
  .palette-title {
    font-size: var(--fs-caption); color: #889;
    margin-bottom: 4px; letter-spacing: 0.05em; text-transform: uppercase; width: 100%;
  }
  .palette-biome {
    font-size: var(--fs-tiny); color: #667; margin-top: 6px; margin-bottom: 2px;
    letter-spacing: 0.06em; width: 100%;
  }
  .swatch {
    display: flex; align-items: center; gap: 6px;
    padding: 3px 6px; border-radius: 4px; cursor: pointer;
    border: 1px solid transparent;
    font-size: var(--fs-small); color: #ccc;
    white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    transition: border-color var(--dur-fast), background var(--dur-fast);
  }
  .swatch.selected { border-color: #7799ff; background: #1a2a4a; }
  .swatch-eraser {
    width: 24px; height: 16px; display: flex;
    align-items: center; justify-content: center;
    font-size: var(--fs-caption); flex-shrink: 0; color: #ff7777;
  }
  .swatch-img { width: 24px; height: 16px; object-fit: cover; border-radius: 2px; flex-shrink: 0; }

  /* Grid */
  .grid {
    display: inline-grid;
    border: 1px solid #334; border-radius: 4px; line-height: 0;
  }
  .cell {
    width: 36px; height: 24px; box-sizing: border-box;
    border: 1px solid #223; cursor: crosshair;
    background-size: cover; background-position: center;
  }

  /* Actions */
  .actions {
    display: flex; flex-wrap: wrap; gap: 12px;
    margin-top: 14px; align-items: center; justify-content: center;
  }
  .btn {
    border-radius: 6px; cursor: pointer;
    font-size: var(--fs-subhead); letter-spacing: 0.04em;
    padding: 8px 22px;
  }
  .btn-load  { font-size: var(--fs-body); padding: 5px 14px; background: #1a2a1a; color: #88ff88; border: 1px solid #336633; border-radius: 4px; }
  .btn-save  { background: #1a1a3a; color: #88aaff; border: 1px solid #334477; }
  .btn-play  { background: #1a3a1a; color: #88ff88; border: 1px solid #336633; }
  .btn-back  { background: #1a1a1a; color: #aaa; border: 1px solid #333; padding: 8px 18px; }
  .btn:hover { filter: brightness(1.15); }
  .status { font-size: var(--fs-body); margin-left: 8px; }
</style>
