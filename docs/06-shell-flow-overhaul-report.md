# Shell & Flow Overhaul — Final Report

Execution of `docs/06-shell-flow-overhaul.md`, one move at a time, each proven by an
automatic test + a mobile screenshot reviewed by a clean-context subagent before commit.

**Final gate:** 194 xUnit + 103 Playwright (@390×844) green, run **twice**. Asset usage 86.3%.

| Move | Commit | Proving tests | Screenshots | Subagent verdict |
|------|--------|---------------|-------------|------------------|
| 1 — Collapse menu into one journey | `bd0297d` | `menu.spec` (one-journey structure; Continue resumes furthest node), `biomes.spec` (menu→map→node nav) | `move1-home-fresh.png`, `move1-home-advanced.png` | MATCHES |
| 2 — Classic-button polish | `4746e4a` | `menu.spec` (≥44px touch targets; every docked destination navigates) | `move1-home-fresh.png` (hierarchy/framing) | MATCHES |
| 3 — Dungeons → Rifts | `b09a7b0` | `RiftTests` (11 cases: force/none/roll, biome pick, deterministic), `rift.spec` (banner→Descend→active run; Skip→campaign intact; no Dungeons menu) | `move3-rift-banner.png` | MATCHES |
| 4 — HUD bars 3-slice | `c1d432d` | `hud-bars.spec` (border-image caps symmetric; HP/balls/boss track 100/50/0%), `hud-live.spec` | `move4-bars-{full,half,empty}.png`, `move4-boss-{full,half,empty}.png` | MATCHES |
| 5 — Distinct levels | `554c673` | `all-levels.spec` (14 levels load + winnable), `biomes.spec` | `move5-{hell-2,caverns-1,village-ghost,heaven-2}.png` | MATCHES |

## Per-move log evidence
- **Move 1:** client console `[ark:menu] furthest-node {level, label}` / `continue` / `open-map` / `open-scene`.
- **Move 3:** `[ark:rift] offered|none`, `banner-shown`, `descend`, `skip`; server `/complete` returns `rift` object; `RiftService` deterministic by seed.
- **Move 4:** fill width stays a plain `%` string (parseable); `setLives/setBalls/setBossHp` cheats drive exact fills; logged via `cheat` sim-log lines.
- **Move 5:** `tools/gen-levels.mjs` validates 8×14 + ≥1 needToKill at author time; `all-levels.spec` is the runtime safety net.

## Honest notes / deferred
- Net-new level mechanics named in the brief (destructible *treasure* blocks, *cursed multiplier* blocks, Heaven *light-beam* puzzles) are **deferred** — each needs a new sim system + art beyond the existing block catalog. Recorded in `06-shell-flow-overhaul.md`.
- Caverns rock-pillar legibility was the softest of the five biome mechanics; caverns-1 was re-authored into clearer varied-length stalactite columns after the subagent flagged it.
- `RiftChance` default 0.34; the rift banner is a non-blocking overlay over the campaign map, so it can never desync existing campaign assertions (and `campaign.spec` pins `ark_rift_mode=none` for full determinism).
