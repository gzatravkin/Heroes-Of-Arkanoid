# Frontend Refactor Roadmap — June 2026

Companion to `refactor-tasks-2026-06.md` (backend). Task descriptions only — nothing implemented.
Source dump: `docs/frontend-dump-2026-06.md` (generated 2026-06-12).

**Current shape:** 11,665 lines in `frontend/src` — 11 scenes (~3,800 lines), render layers (~3,100), HUD (~1,000), the rest audio/net/ui/input. No framework, no component layer: every scene is a hand-rolled `mount*(host)` function building DOM with `createElement`, injecting its own `<style>` blob, and wiring its own navigation chrome.

**The harsh verdict.** The frontend has the same disease as the backend had, expressed in DOM instead of C#: **no shared abstractions, so every scene re-invents the same five things** (style injection, topbar/back button, background layers, card/panel markup, fetch-then-render), and **backend data is hand-mirrored into TS constants that are already drifting** — spell costs, relic names/icons, character art keys, the Snapshot type itself. There is also at least one real bug (F2). Honest ceiling without a framework: ~12–15% LOC reduction (−1,400 to −1,800). **With Svelte migration (F-SV): −1,800 to −2,400 lines (~20%), which also subsumes F3, F4, and F7** — making Svelte the recommended primary path for the UI layer.

---

## F1 — Stop hand-mirroring backend data *(correctness, do first)*

**Problem.** Four separate client-side copies of server-owned data, all guaranteed to drift:

1. `Hud.ts` `SPELL_COSTS` — 20 hardcoded mana costs whose comment admits *"Mirrored from backend SimConfig — update here whenever SimConfig spell costs change"*, complete with hand-tracked balance history (`// bumped 20→25 (P7a balance pass)`). Affordability dimming silently lies after every balance pass.
2. `overlays.ts` `RELIC_NAMES` + `RELIC_ICONS` — ~40 lines duplicating `relics.json`, **even though the snapshot's `activeRelics` already carries `name` and `icon` from the server**. Dead duplication of data already on the wire.
3. `Hud.ts` `loadFireMageFallback` — a hardcoded copy of the Fire Mage kit from `characters.json`.
4. `Renderer.ts` `CLASS_PADDLE_KEYS` / ball-sprite tables — per-class art keys in code; `characters.json` already holds per-class art references.

**Task.**
- Serve spell defs (id, cost, icon) from the backend (`/characters` already exists — add costs there, or a `/spells` endpoint once backend T1's `spells.json` lands; sequencing note below).
- Delete `RELIC_NAMES`/`RELIC_ICONS`; read name/icon from the snapshot relic DTOs that already contain them.
- Move class art keys into `characters.json`; `Renderer.setClass` reads the catalog.
- Keep the Fire Mage fallback only as a last-resort constant *generated* from the fetched data shape, or drop it and show a retry.

**Acceptance:** no TS constant restates a value that exists in `config/*.json` or arrives in the snapshot. Net **−120 to −150 lines** and a class of silent-drift bugs gone. *(Coordinates with backend T1 — if T1 ships first, costs come free in the spell defs.)*

## F2 — Fix the keyboard-listener leak; give scenes a lifecycle *(bug)*

**Problem.** `Hud.wireConnHandlers` does `document.addEventListener("keydown", …)` and never removes it. Every battle mount creates a new `Hud` → a new permanent global listener closing over the old `Connection`. Play three battles, press Q: three cast attempts on three connections (two dead). More broadly, **mount functions return nothing** — there is no teardown contract at all; `main.ts` compensates with the `(window as any).__renderer/__conn` hack and `host.innerHTML = ""`.

**Task.**
- Every `mount*` returns a `dispose(): void` (remove listeners, stop tickers, close sockets). `doMount` keeps the previous scene's dispose and calls it before mounting the next.
- `Hud` keeps a reference to its keydown handler and removes it on dispose; same audit for `Music`/`Sfx`/`BallTrail` tickers and any other `document`/`window` listeners (grep shows several).
- The `__renderer`/`__conn` globals die; `__game` survives only as an explicitly typed test bridge (see F6).

**Acceptance:** mounting battle N times leaves exactly one keydown listener; `teardownBattle` is deleted. Net ~**−30 lines** plus the bug fix.

## F3 — A 5-piece UI kit instead of 11 hand-rolled scenes *(biggest win)*

**Problem.** Eleven scenes, each 250–490 lines, each independently re-implementing:
- **Style injection** — 11 copies of the `if (document.getElementById(id)) return;` inject pattern.
- **Topbar + back navigation** — at least three divergent implementations: `<a class="ach-back">` (arrow via `::before`), `<a class="set-back">` (`← Menu` text), `<button class="ui-back">` (click → `navigateTo`). Inconsistent semantics (link vs button), inconsistent a11y, restyled every time.
- **Background layers** — the warm-gradient `.x-bg` div with near-identical radial/linear gradients per scene.
- **Card/panel markup** — BarGoods 9-slice panels, grids of tiles with locked/unlocked filters, rebuilt per scene with new class prefixes (`ach-`, `set-`, `sk-`, `inv-`…).
- **Fetch-then-render** —每 scene wires its own `metaApi.get*().then(render)` with its own error handling (or none).

**Task.**
- `ui/kit.ts` with ~5 primitives: `sceneShell(title, opts)` (root + bg + topbar + back + content slot), `card()`, `tileGrid()`, `artButton()` (the 9-slice pill from MenuScene), `el(tag, cls, props)` (replacing the ad-hoc `createElement` + `cssText` chains).
- One `.scene-*` class vocabulary in shared CSS replaces the per-scene prefixed near-duplicates.
- Scenes become: data fetch + scene-specific content built from kit pieces. Target ≤150 lines for the simple scenes (Settings, Achievements, Inventory, Skills, Dungeons).

**Acceptance:** zero per-scene topbar/back/bg implementations; a new scene starts at ~60 lines. Net **−800 to −1,100 lines**.

## F4 — One CSS delivery mechanism

**Problem.** Three coexisting strategies: (a) ~2,500 lines of CSS template literals inside `injectXStyles()` functions, (b) separate style-string modules (`campaignStyles.ts` 349, `inventoryStyles.ts` 262, `hudStyles.ts` 151), (c) 53 inline `style.cssText` sites. The CSS token lint has to parse TS strings to do its job; HMR re-injects whole blobs; nothing is cacheable.

**Task.**
- Static `.css` files imported through Vite (it bundles, dedupes, HMRs, and the token lint reads plain CSS). One file per scene/feature plus the shared kit stylesheet from F3.
- `cssText` stays only for genuinely dynamic values (positions, timers, computed widths); decorative inline styles move to classes.
- Delete every `inject*Styles` function (F3's shell injects nothing — styles are just imported).

**Acceptance:** no `<style>` element created at runtime; `cssText` only sets values computed at runtime. Net **−150 to −250 lines** of TS scaffolding, and the stylesheet becomes lintable/cacheable.

## F5 — Honest Snapshot type + generic effect chips *(pairs with backend T9)*

**Problem.** The hand-written `Snapshot` interface in `Connection.ts` is already behind the wire format — `Hud.ts` reads `(s as any).fireshotActive`, `(s as any).shieldActive` because the fields were never added to the type (13 `as any` casts in src). `contract/protocol.md` still documents protocol "v0" with a fraction of today's fields. The HUD then has three separate per-effect renderers (`updateEffects`, `updatePowerups`, plus per-field timer chips) mirroring the backend's per-effect snapshot fields.

**Task.**
- One complete, authoritative `Snapshot` type — ideally *generated* from the C# DTOs (a ~50-line script reading `Snapshot.cs` attributes, run in CI to fail on drift; or sourced from a shared JSON schema). Delete every snapshot-related `as any`.
- Update `contract/protocol.md` to the actual format, or generate it from the same source.
- When backend T9 ships `activeEffects: [{id, timeLeft}]`: collapse `updateEffects` + `updatePowerups` into one chip renderer driven by a small `effectId → {label, color, icon}` map.

**Acceptance:** `grep "as any" src/` returns only the test bridge; snapshot field drift fails CI instead of failing silently at runtime. Net **−80 lines** now, more with T9.

## F6 — Typed test bridge instead of `window` grab-bag

**Problem.** 6 `(window as any)` sites: `__renderer`, `__conn`, `__game` are simultaneously (a) the teardown mechanism, (b) the Playwright introspection API, and (c) untyped. MenuScene even carries a comment-warning about how installing a partial `__game` stub breaks the standard poll — tribal knowledge papering over an undeclared contract.

**Task.** `testBridge.ts` exporting `installBridge(parts: {getState?, renderer?, conn?})` with one declared `Window` augmentation; battle installs it, dispose removes it. Teardown stops flowing through window globals entirely (F2).

**Acceptance:** one file owns every window global; Playwright helpers import its type. LOC-neutral; deletes a documented foot-gun.

## F7 — Split the HUD god class *(after F1/F5)*

**Problem.** `Hud.ts` is 595 lines doing five jobs: DOM construction for ~8 panels, 20+ element refs as fields, per-feature update methods, input binding (pointer + keyboard), and spell-kit management with a legacy-ID compatibility table for old tests.

**Task.**
- Widgets with a uniform `{el, update(s)}` shape: `StatBars`, `Hotbar` (owns spell slots + input wiring + dispose), `BossBar`, `StatusChips` (absorbs effects/powerups/combo once F5/T9 land), `Banner`, `ObjectiveTimer`. `Hud` becomes composition + fan-out `update(s)`.
- Drop `FIRE_MAGE_SLOT_IDS` legacy mapping — update the handful of Playwright selectors to the uniform `hud-spell-<id>` ids instead of carrying a compatibility table in production code.

**Acceptance:** no file in `ui/` over 250 lines; each widget independently testable. Net **−100 to −150 lines** (mostly via StatusChips merge).

## F8 — Repo hygiene: stop committing screenshots and dumps to the working tree

**Problem.** Not code, but it taxes every checkout and review: `tests/` carries ~40 loose `shot-*.png`/`audit-*.png` files beside the specs, the repo root holds another ~15 screenshots, 6 log files, and `_backend.cs` / `_frontend.ts` concatenated source dumps (12k+ lines each, instantly stale).

**Task.** Gitignore `tests/*.png` (except `*-snapshots/` golden dirs), root `*.png`/`*.log`; delete the `_backend.cs`/`_frontend.ts` dumps (regenerable by tooling if ever needed); move wanted reference shots into `docs/` or `tests/demo-screenshots/`.

**Acceptance:** `git status` after a test run shows no untracked artifacts; no generated dump files in version control.

---

## F-SV — Migrate UI layer to Svelte 5 *(recommended; subsumes F3, F4, F7)*

### Why Svelte over React/Preact/Lit/Solid

Three constraints from the actual code decide this:

1. **60Hz HUD updates from WebSocket snapshots.** A VDOM framework (React, Preact) diffs a component tree 60×/sec. Svelte 5 runes compile to direct DOM bindings — changing `snapshot.mana` updates exactly one text node. No virtual diff on the hot path.
2. **Playwright suite selects by DOM id.** ~50 specs use `#hud-spell-fireball`, `#btn-continue`, etc. Svelte renders plain DOM with your ids. **Lit uses shadow DOM — every selector breaks, so Lit is out.**
3. **CSS chaos is the biggest maintenance pain (F4).** Svelte's `<style>` blocks are scoped and co-located — F4 stops being a task and becomes a side effect of porting each file.

| | Svelte 5 | Solid | Preact | React | Lit |
|---|---|---|---|---|---|
| 60Hz HUD (signals) | ✅ | ✅ | ⚠️ VDOM | ❌ heaviest | ⚠️ |
| Solves CSS chaos | ✅ scoped `<style>` | ❌ | ❌ | ❌ | ⚠️ |
| Playwright suite survives | ✅ | ✅ | ✅ | ✅ | ❌ shadow DOM |
| Runtime size | ~5KB | ~7KB | ~4KB | ~45KB | ~15KB |
| Pixi coexistence | ✅ canvas in one component | ✅ | ✅ | ⚠️ | ✅ |
| Lifecycle / teardown | ✅ `onDestroy` | ✅ | ✅ | ✅ | ✅ |

### What gets migrated — and what doesn't

**Migrated (the UI layer only):**
- All 11 scenes (`scenes/**/*.ts`) → `.svelte` components
- `ui/Hud.ts` and sub-widgets → `.svelte` components
- CSS-in-TS blobs, `injectXStyles()` functions → `<style>` blocks

**Never migrated (gains nothing from a framework):**
- `render/**` — Pixi layers (imperative, canvas-based; stays as-is inside one `BattleCanvas.svelte`)
- `net/**`, `audio/**`, `input/**` — plain TS modules; no DOM
- `main.ts` router — stays; `mount(Scene, {target: host})` replaces `mountX(host)`

### Migration plan (incremental — no big bang)

**Phase 0 — coexistence setup (half a day).**
```
npm i svelte@5 @sveltejs/vite-plugin-svelte
```
Add Svelte plugin to `vite.config.ts`. Vanilla and Svelte scenes coexist through the existing router — `doMount` calls `mount(SvelteScene, {target: host})` for ported scenes, `mountX(host)` for unported ones. No big bang.

**Phase 1 — five simple scenes** (Settings, Achievements, Skills, Inventory, Dungeons — ~1,800 lines today → ~600 lines).
Each is a pure fetch-then-render list. Becomes a `<script>`, a template, and a `<style>` block. Playwright ids preserved. Run specs after each port. This is the F3 + F4 payoff for these scenes.

**Phase 2 — Menu, Campaign, Character, Editor, Dungeon/Dungeons detail** (~1,750 lines).
Shared topbar, back button, 9-slice button, background layer become actual Svelte components here — the F3 kit, but as `.svelte` files. The `nineSlice.ts` helper becomes a `<NineSlice>` component.

**Phase 3 — Battle shell + HUD** (the signal-model payoff).
`Snapshot` becomes `$state`; HUD widgets bind directly (`{snap.mana}` drives the mana bar). The 595-line `Hud` class with 20+ element refs dissolves into per-widget markup. `onDestroy` handles the keydown-listener leak (kills F2). One `BattleCanvas.svelte` creates/destroys `Renderer`; `render/**` is untouched.

### Task details

- **Setup:** `npm i svelte@5 @sveltejs/vite-plugin-svelte`; patch `vite.config.ts`; verify existing specs still pass.
- **Phase 1–2 per scene:** delete the `mount*` function and its `injectStyles`; create `SceneName.svelte` preserving all DOM ids the specs depend on; import via `mount()` in router.
- **Phase 3 HUD:** `$state(null as Snapshot | null)` updated from `Connection.onmessage`; each sub-widget is a component receiving the reactive state as a prop. Drop `FIRE_MAGE_SLOT_IDS` legacy table; update the ~4 Playwright selectors that use old ids.
- **F6 (test bridge):** `testBridge.ts` with declared `Window` augmentation; installed by `BattleCanvas.svelte` `onMount`, removed by `onDestroy`.

**Acceptance:** no `injectXStyles` function remains; no `createElement`+`cssText` chain for structural markup (dynamic values like computed positions still allowed); `grep "as any" src/` returns only `navigator.webdriver` (the Pixi headless gate). Playwright suite passes unchanged.

**Estimated net:** **−1,800 to −2,400 lines** (~20% of 11,665). Subsumes F3, F4, and F7 entirely. ~1.5–2× effort compared to the vanilla-kit path, but you only rewrite scenes once.

**When to skip and do vanilla F3 instead:** if this project is in maintenance mode with no new scenes or HUD features planned. The vanilla kit gets ~70% of the LOC benefit for half the work.

---

## Explicit won't-fix

- **Client-side state store.** Scenes are short-lived and refetch on mount; that's correct for this app size. Revisit only if cross-scene state actually appears.
- **Render-layer consolidation.** The ~14 Pixi layers (`BallLayer`, `BlockLayer`, `HazardLayer`…) are honest single-purpose classes around 100–300 lines; merging them would trade clarity for nothing.

---

## Sequencing

### Path A — Svelte migration (recommended)

1. **F2** (keydown leak) — live bug; fix now before any porting so Phase 3 doesn't inherit it.
2. **F1** (stop mirroring backend data) — correctness; unblocked for relics/art; partially blocked on backend T1 for spell costs.
3. **F5** (authoritative Snapshot type + codegen) — establishes the contract; delete all `as any` before Phase 3 wires signals.
4. **F-SV Phase 0** — vite plugin, coexistence verified.
5. **F-SV Phase 1** — simple scenes (Settings, Achievements, Skills, Inventory, Dungeons).
6. **F-SV Phase 2** — remaining scenes; shared components emerge.
7. **F-SV Phase 3** — Battle shell + HUD; signals wired; F6 test bridge lands here.
8. **F8** — gitignore stale artifacts; any time, zero risk.

### Path B — Vanilla UI kit (skip Svelte)

1. **F2** — bug fix.
2. **F1** — data de-mirroring.
3. **F5** — snapshot type.
4. **F3 → F4** — UI kit, then CSS consolidation.
5. **F6, F7** — cleanups.
6. **F8** — hygiene.
