# Pickable Spells — implementation roadmap (2026-06-13)

**Goal:** replace the per-class **fixed 5-spell kit** with the design's intended model
(`docs/04` §3, §4.1, §5): a character has **one signature spell** (exclusive, locked in
hotbar slot 0) plus a **loadout drafted from a shared pool**. This closes the spec drift
recorded in `CLAUDE.md` ("Reconcile the system before you extend it").

## Decisions locked (2026-06-13)

| Decision | Choice |
|---|---|
| Acquisition | **Both, staged** — persistent **equip screen** (own → equip) *and* **in-run pick-1-of-3** (unlock/draft). |
| Loadout size | **Grows with progression** — start small, unlock slots via milestones. Variable-slot HUD. |
| First cut | **Campaign + dungeon together** — foundation + equip + picks across both modes in one pass. |

### Loadout-growth schedule (proposed, tunable)
- Slot 0 = **signature**, always present, **cannot be unequipped**.
- Drafted slots **N** grow: **start sig + 2**, **+1 per biome boss cleared**, **cap sig + 4**
  (= 5 total, matches the current `Q/E/W/R/T` keybinds). Going past 5 needs new keybinds —
  out of scope for this cut.

## What already exists (reuse, don't rebuild)
- **Shared pool of definitions** — `SpellCatalog` (`backend/Arkanoid.Core/Spells/SpellCatalog.cs`)
  already defines all **20 spells** with full behavior params. No new spell definitions needed.
- **Own/equip pattern** — the **items** system is the exact template: `Profile.OwnedItems` +
  `EquippedItems` (cap + subset-of-owned), `ItemEndpoints.cs`, `InventoryScene.svelte`
  (incl. the "Slots Full" cap UI). Mirror it for spells.
- **Persistent spell leveling** — `Profile.SpellLevels` already persists.

## The one true seam
`GameInstance.CastSlot(slot)` (`GameInstance.cs:143`) resolves `charDef.Spells[slot]` straight
from `characters.json` (the fixed kit). It must instead resolve the player's **equipped loadout**
(signature + drafted), fed in at construction like `SpellLevels`/relics are.

---

## Progress log (2026-06-13, overnight session)

- **Phase 1 (backend foundation) — DONE & green.** characters.json + CharacterCatalog signature/starting/pool;
  Profile.EquippedSpells + UnlockedSpellSlots; Loadouts resolver; SpellEndpoints; GameInstance loadout
  seam (CastSlot reads the loadout, falls back to full kit for sim tests); snapshot exposes the loadout.
  Backend **328/328** (15 new). Signatures: fire_mage=ignite, paladin=shield, engineer=overload,
  necromancer=skeleton. (Engineer/necromancer signatures reconcile to docs/04 §3; S14 deepens behavior.)
- **Phase 2 (equip UI) — DONE & green.** LoadoutScene (signature locked slot 0, variable slots, Slots-Full
  cap); HUD hotbar reads snapshot loadout (fallback to /characters); CharacterScene "Loadout" button.
  Added a symmetric `CastPhoenix` test command (phoenix previously needed castSlot(4)). Playwright
  **spell-loadout 8/8**, affected specs (class-kits/firemage-playtest/spell-turret) green. Screenshot-reviewed.
- **Phase 3 (in-run picks, both modes) — DONE & green.** DungeonRun.DraftedSpells; DungeonService
  spell pool (`spell:` prefix) + PickChoice routing; GameInitializer appends drafts; Rewards boss-clears
  unlock pool spells (hell→phoenix+paladin kit, caverns→engineer kit, village→necro kit). Frontend:
  spellCache + buildPickOverlay renders spell picks (atlas icon + "Spell · N mana"); preloadSpells.
  Verified: pick overlay screenshot shows spell cards (Duplicate/Radiation) beside a relic.
- **Phase 4 (loadout growth) — DONE & green.** Rewards grows UnlockedSpellSlots +1 per biome-boss
  clear (cap 5); equip screen + HUD already render variable slots. E2E confirms hell-boss → 4 slots.
- **Validation:** backend **333/333**, full Playwright **200/200** (workers=1, 14.1m, 0 regressions),
  Phase 3 specs (spell-picks + dungeon) **5/5**. Pick-overlay + loadout screens screenshot-reviewed.
- **Phase 5 — S15 DONE.** Behavior-accurate one-line spell descriptions authored for all 20 spells
  (`config/characters.json` `desc`, exposed via /characters + /spells), shown on the Skills screen,
  the Loadout cards, and the floor-clear pick overlay (getSpellBlurb prefers the description).
  Verified: Skills screen screenshot reads cleanly; loadout + upgrade specs green.
- **Phase 5 — S14 deferred (design call):** Necromancer "Raise" as a true summoned entity vs the
  current timer-turret (round-2-recommendations §1). Needs a design decision before building.
- **Adjacent B1 (block-break animation) DONE:** replaced the 3-stack (oversized crack + fireball +
  wrong vaza/hell-ball death on every brick) with a coherent biome shatter + one biome-tinted spark;
  destruction-path specs green, no console errors.

**Net: the roadmap is functionally complete (Phases 1–5 less the S14 design call). All green:
backend 333/333, full E2E 200/200 + Phase 3 picks.**

## Build order

### Phase 1 — Backend foundation
- **S1.** Reframe `config/characters.json`: each character declares a `signature` (slot-0 id) +
  `starting` spells (initial owned/bias). The other 15 become pool-draftable by anyone
  (`docs/04` §3 line 69). Extend `SpellSlotDef`/`CharacterDef` + `CharacterCatalog`.
- **S2.** `Profile`: add `EquippedSpells: Dictionary<characterId, List<spellId>>` (ordered,
  slot 0 = signature) and `UnlockedSpellSlots: int` (the growth counter). Treat `SpellLevels`
  keys as the *owned* set (own ⇒ level ≥ 1). Seed signature + starting in `NewDefault()`;
  migrate existing saves (back-fill signature + current 4 as owned/equipped).
- **S3.** `SpellEndpoints` mirroring `ItemEndpoints`: `/spells` (pool / owned / equipped for the
  selected character), `/spell/equip`, `/spell/unequip` — enforce the slot cap (= unlocked
  slots) and "signature can't be unequipped / is always slot 0".
- **S4.** Sim: `GameInstance.SetLoadout(IReadOnlyList<string>)`; `CastSlot` resolves the loadout,
  not `charDef.Spells`. Wire from `GameInitializer` via `profile.EquippedSpells[selected]`
  (fallback = signature + starting).
- **S5.** Snapshot exposes the active loadout (ordered ids + mana + level) so the HUD renders the
  *equipped* spells, not the static `characters.json` list.
- **S6.** Design-fidelity tests, **red-first** (per the new CLAUDE.md systems-layer rule):
  "a class starts with only its signature in the hotbar"; "a spell casts only if equipped";
  "signature cannot be unequipped"; "equip is capped at the unlocked-slot count".

### Phase 2 — Loadout / equip UI
- **S7.** Loadout screen mirroring `InventoryScene.svelte`: owned spells, equip up to the
  unlocked-slot count per character, signature locked in slot 0, reuse the "Slots Full" UI.
  Entry point from `CharacterScene` / Skills.
- **S8.** HUD hotbar reads the equipped loadout from the snapshot (drop the `/characters` read);
  **variable slot count** (renders only unlocked slots).
- **S9.** Playwright: equip a pool spell → appears in hotbar + casts; unequip → gone; signature
  always present; locked slots not shown.

### Phase 3 — In-run picks (campaign + dungeon)
- **S10.** Add **spell** entries to the pick-1-of-3 pool. Campaign: picking a spell = **permanent
  unlock** into the owned pool. Dungeon: picking = **draft into this run** (resets on death)
  (`docs/04` §5). Backend pick generator + a spell-pick source alongside `bonuses.json`.
- **S11.** Pick overlay renders spell picks with descriptions (reuse the `buffDesc` / relic-desc
  cache in `overlays.ts` / `relicCache.ts`).
- **S12.** Playwright: clear a campaign level → spell offered → picking unlocks it (persists);
  dungeon floor → spell drafted into the run kit.

### Phase 4 — Loadout growth
- **S13.** Implement the growth schedule (S-decisions): `UnlockedSpellSlots` rises on biome-boss
  clears; equip screen + HUD respect it; a "new spell slot!" beat on unlock.

### Phase 5 — Fidelity & polish
- **S14.** Signature audit per class vs `docs/04` §3: Fire Mage **Ignite** ✓; Necromancer **Raise**
  is still a stubbed timer, not a summon; reconcile Paladin **Aegis** / Engineer **Overload**
  naming vs the current `shield`/`overload`.
- **S15.** Spell descriptions on the Skills screen (the deferred content task) — viable now that a
  pool/loadout exists. Author ~20 short descriptions verified against `docs/01`/`docs/04`.
- **S16.** Design-fidelity playtest + screenshots, reviewed critically ("it rendered" isn't the bar).

---

## Adjacent (not part of this goal, logged here so it isn't lost)
- **B1 — block-break animation pass.** `Effects.spawnBlockDestroy` (`Effects.ts:193`) stacks **3
  simultaneous bursts** (biome crack-strip @1.5× + additive Explosion @0.9× + biome secondary
  @1.2×) of differing scale/tint/blend → incoherent. Biome strips are sliced with a **hardcoded
  20-frame count** (`animStrip(biomeStrip, 20)`) that may not match the asset → squashed frames
  (the very "squashed-garbage" failure warned about at `BlockLayer.ts:34`). Underneath, the block
  sprite `removeChild`s with no fade (`BlockLayer.ts:233`) → a 1-frame pop. Fix: one coherent
  effect per biome, verify true frame counts, align scale, drop the triple-stack, review on screen.
