# Arkanoid RPG — Working Agreement

Standalone git repo rooted here (`Arkanoid game/`). Backend: C#/.NET 8 (`Arkanoid.Core` pure sim + `Arkanoid.Server` + `Arkanoid.Tests`). Frontend: Vite + TypeScript + **Svelte 5** + PixiJS renderer. Tests: xUnit (sim) + Playwright (end-to-end).

## The cardinal rule: content is judged by design fidelity, not by code metrics

Spells, relics, ball cores, paddle mods, pickups, bosses, and enemies are **game content**. Their worth is how they *play*, measured against the design docs — **never** line count, dedup, or "adding one is JSON-only."

- **Design docs are the source of truth for behavior.** Before touching any spell/relic/boss/enemy, read its spec in `docs/01-current-game-design.md` and `docs/04-new-game-design.md`. The task must cite the design-doc line it implements.
- **Never collapse bespoke content behavior into a shared abstraction** unless the abstraction reproduces the *exact* designed behavior — trigger, timing, and on-screen identity. Code that "looks similar at the cast site" is NOT license to merge: a spell's identity lives in its per-tick behavior and trigger condition (e.g. Turret fires *on ball-catch*, Phoenix is a *visible orbiting entity*), not in the spend-mana boilerplate. When in doubt, keep it bespoke.
- **LOC reduction is never a goal for content systems.** It is the metric that flattened the Fire Mage kit into lifeless generic archetypes while every test stayed green. Do not repeat it.
- **Design fidelity includes the *system that delivers* the content, not just each item's behavior.** How a spell/relic/core is *acquired* — signature vs. drafted, the pick pool it rolls from, loadout size and how it grows, the economy that gates it — is design, and lives in the doc (`docs/04` §3–§5). A spell whose trigger/timing/identity are perfect is **still wrong** if it reaches the player through the wrong acquisition model. Per-item fidelity inside the wrong structure is the systems-layer version of the LOC trap: every spell tests green while the *game* doesn't match the design.

> **ACQUISITION MODEL — source of truth as of 2026-06-14: `docs/2026-06-14-economy-rework-proposal.md` (owner-approved), superseding the older acquisition specs in `docs/04` §3–§6 and `docs/2026-06-13-content-and-stats-design.md` §5.6/§5.10.** The intended model is now: **3 currencies** — Sparks (cards+modules), Souls (spells+heroes+spell-slots+mastery respec), Insight (mastery); **fixed-price pure-random rolls** (`RollService`) where duplicates level the item / ascend the hero; spells are a **GLOBAL pool** (any non-signature spell on any hero) with each hero's **signature locked in slot 0** and **spell affinity** giving a mana discount on the matching element; heroes are **boss-unlocked into a roll pool** then rolled; mastery levels on **Insight**, respec on **Souls**; the campaign map is a **linear chain**. This is a deliberate override (it replaces "signature + drafted-in-run-from-a-shared-pool" and the per-class fixed kits), not drift — extend *this* model, and judge fidelity against the proposal doc.

## Tests must assert DESIGN, not mechanics

A test that asserts "a projectile spawns" passes for a Turret that fires on a dumb timer when the design says it fires on ball-catch. Worthless.

- Behavior tests encode the **trigger and identity** from the design doc: e.g. "Turret fires exactly once per paddle deflect, and zero bolts when the ball is never deflected"; "Phoenix exists as an entity with its own position distinct from the ball."
- For content refactors, write the design-fidelity (characterization) tests **first**, confirm they FAIL against the broken/old code, then make them pass. Red before green.
- **Cover the systems layer too, not just per-item behavior.** A content system needs at least one test of its *structural* design invariant — e.g. "a class starts with only its signature spell; the other hotbar slots are filled by drafted picks," "a spell can only enter the kit via a pick." Without it, faithfully-built individual items pass forever inside a structure that contradicts the doc (the fixed-kits drift).

## Reconcile the system before you extend it — code is not the spec

Before adding to or building on an existing content system (`config/*.json`, a catalog, a pick pool, the hotbar), read what the design doc says that system's *shape* should be and compare it to what the code actually does. If they disagree, the code has **drifted** — say so in the task and decide explicitly: close the gap, or defer it (logged in `docs/round-2-recommendations.md`). Never silently extend the drifted version as if it were the intent.

- **`MVP` / `stub` / `placeholder` / `(was never coded)` in a design doc is a known-incomplete marker, not a finished state.** When you touch a system carrying one, state whether the placeholder is still in place. A hardcoded shortcut that ships "something playable" ossifies into the de-facto design if no one revisits it — that is exactly how the per-class **fixed spell kits** (`config/characters.json`) silently replaced the doc's *signature-spell + drafted-from-a-shared-pool* model (`docs/04` §3, §4.1, §5).

## "It passed / it rendered / it compiled" is not the bar

This applies to gameplay AND visuals (see also the persistent memory on the visual quality bar).

- Any change to **spells, HUD, render, or feel** must be verified by actually running it — a Playwright scenario that exercises it plus a screenshot/trace reviewed *critically against the design doc* — before it is called done.
- A passing unit test is necessary, not sufficient. The unit test cannot tell you the spell is fun or that it matches the design.

## Frontend: `vite build`, not just `tsc`

`tsc --noEmit` does **not** catch Svelte template/compile errors (it never parses `.svelte` markup). After any `.svelte` change, run `npx vite build` (or the Playwright suite, which boots a fresh dev server) before declaring success. A broken `$derived:`/rune once shipped a white screen with `tsc` fully green.

## Validation/review must check the right thing

A review of content work verifies **design fidelity** (cite the doc, run the spell) — not code shape (file exists, switch deleted, LOC down). Re-checking the same code-shape criteria that produced a bug catches nothing.

## Commands

- Backend: `dotnet build` and `dotnet test` from `backend/`.
- Frontend typecheck: `npx vite build` from `frontend/` (preferred over `tsc` for Svelte).
- E2E: `npx playwright test` from `tests/` (boots backend + frontend automatically).

## Other standing constraints

- **Git worktrees are banned** — execute in the working tree. (Bisecting a live Vite dev server: use a worktree-free approach; do not `git stash` against the running server — HMR ships the revert straight to the browser.)
- Paths are relative to this repo root (`git add backend`, not `git add "Arkanoid game/backend"`).
