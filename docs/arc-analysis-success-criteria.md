## Arc Analysis: Criteria of Success

Each problem from `arc analysis.txt` is listed with a concrete, verifiable success criterion and current status.

---

### Category 1 — God classes / god methods

| Problem | Success Criterion | Status |
|---|---|---|
| `GameInstance` ~70 loose fields, no ownership boundaries | Extract clearly bounded state groups (boss, combo, powerups) into typed nested classes | ✅ `GameInstance.BossState Boss`, `ComboState Combo`, `PowerupState Powerups` — all callers updated |
| `BlockDamage.DamageBlock` ~90-line god method | `ApplyKillEconomy` extracted; `RelicSystem.OnBlockDestroyed` and `ReviverSystem.OnBlockDestroyed` hook pattern in place; method ≤65 lines | ✅ Done — ~63 lines |
| `SimConfig` ~200 flat knobs | Nested config records group related knobs; no flat `Boss*`/`Enemy*`/`Bonus*` collision | ✅ `EnemiesConfig`, `BossConfig`, `PickupsConfig` created |
| `Block` union type with 18 behaviors (17 derived bools) | Single `BlockBehavior` enum discriminant; derived bool accessors computed from enum | ✅ `BlockBehavior` enum in place; all 17 derived bools computed from it |
| `IsStatue` operator-precedence ambiguity | Explicit parens: `(Emitter && EmitAim == "paddle") \|\| ShieldStatue` | ✅ Block.cs explicit parens |

---

### Category 2 — Duplicate logic

| Problem | Success Criterion | Status |
|---|---|---|
| 7 copy-pasted catalogs with inconsistent lookup semantics | Generic `Catalog<TDef>` base class; all 7 extend it with uniform `Get`/`TryGet`/`All` | ✅ BlockCatalog, BonusCatalog, CampaignCatalog, CharacterCatalog, DungeonCatalog, ItemCatalog, RelicCatalog all extend `Catalog<T>` |
| Spell-cast gate copied ~20 times | All cast methods use `Spend(g, cost, name)` helper | ✅ FireMage and ClassSpells all use `Spend()` |
| `SpreadFire`/`SpreadDecay` re-implement block death, skipping combo/crystals/relics | Both route kills through `DamageBlock` | ✅ Done |
| `wide_paddle` vs `powerup_wide` same 12 lines | Both call `ActivateWidePaddle(g, duration)` | ✅ Done |
| Bat-carrier and witch-grab pop logic duplicated | Shared `ReleaseBall(g, ball)` helper | ✅ Done |
| Drain line computed independently 3 places | Single `GameInstance.DrainY` property | ✅ Done |
| `ProfileStore` and `DungeonStore` same class twice | Generic `JsonStore<T>` — both delegate to it | ✅ Done |
| `ItemEffects.ApplyOne` special-cases item IDs in code | Purely data-driven via `def.MagnitudePerTier` switch on `def.Effect` | ✅ Done |

---

### Category 3 — Leaky abstractions

| Problem | Success Criterion | Status |
|---|---|---|
| `GameInstance` mutates `LevelData.Blocks` | `GameInstance` deep-copies blocks at construction; `LevelData` is read-only after load | ✅ Done |
| `DungeonCatalog.Register` mutated at request time (race) | Generated rift definitions stored per-run profile, not in shared singleton | ✅ Done — `profile.PendingRift` pattern |
| `Snapshot.From` reads private sim fields (`g._*`) | `Snapshot.From` uses only public properties on `GameInstance` | ✅ Done — all private state exposed via read-only public properties |
| `Projectile` is four things discriminated by `Kind` strings | `HazardBehavior` enum added to `Projectile`; all spawn/dispatch sites use enum | ✅ `HazardBehavior { None, Bat, WitchGrab, Stalactite, Cart }` in Projectile.cs |
| Boss identity keyed on biome strings in `BossSystem` | `BossKind` enum computed from `LevelData.Biome`; `BossSystem` uses enum switch | ✅ `BossKind` property on `LevelData`, all `BossSystem` string comparisons replaced |
| Trust boundary: `/complete?treasureBonus=N` client-controlled | Server computes `ItemCrystalBonus` from equipped items; client value ignored | ✅ `ItemEffects.ComputeTreasureBonus` on server; `GameSession.GrantWinReward` auto-grants on Won |
| Dual config-loading paths: `GameSession.LoadLevel` re-reads catalogs per session | Catalogs loaded once at startup; `GameInitializer` receives pre-loaded catalogs | ✅ `GameInitializer` static class — no per-session disk reads |
| Cheats reachable from any WebSocket without environment gate | `CheatHandler` gated on `ARKANOID_CHEATS=1` or `Development` environment | ✅ Done — `_cheatsEnabled` flag in `GameSession` |

---

### Category 4 — Naming inconsistencies

| Problem | Success Criterion | Status |
|---|---|---|
| "Shield" means 4 unrelated things | `Block.ShieldTimer` → `ImmunityTimer`; Paladin spell uses `PaladinBarrier*` prefix; `powerup_shield` stays as is | ✅ Done |
| "Drain" ambiguity (spell vs drain-line vs lava) | `GameInstance.DrainActive` → `SpellDrainActive` | ✅ Done |
| Bonus vs Power-up parallel vocabulary | `BonusDropChance`→`DropChance`, `PowerUpDropChance`→`SpecialDropChance`, etc. in unified `PickupsConfig` | ✅ Done |
| Economy naming: `ItemTreasureBonus` grants crystals, not treasure | Rename to `ItemCrystalBonus` in `GameInstance`; `Snapshot.TreasureBonus` → `CrystalBonus` (JSON key unchanged for client compat) | ✅ Done — `GameInstance.ItemCrystalBonus`, `Snapshot.CrystalBonus` |
| Lives/HP dual meaning | `Hp` property comment clarified; `SpareBalls`/`StartBalls` is the actual lives axis | ⚠️ Comment only — rename breaks snapshot JSON contract with live client |

---

### Category 5 — Missing separations of concern

| Problem | Success Criterion | Status |
|---|---|---|
| Fixed-timestep drift (send latency accumulates into game time) | Stopwatch-driven `nextTickAt` accumulator: ticks decouple from send time | ✅ Done — `GameSession.RunAsync` Stopwatch pattern |
| `ItemEffects` client-side cheat surface | Server recomputes crystal bonus from profile items; client value ignored | ✅ Done — `ComputeTreasureBonus` in server |
| No reward path from sim to meta: client must call `/complete` | `GameSession` detects `GamePhase.Won` and auto-grants reward via `Rewards.GrantLevelCompletion` + `profileStore.Save` | ✅ `GameSession.GrantWinReward` called on first Won detection — client `/complete` becomes idempotent confirmation |
| `GameSession` does five jobs (transport, loading, profile buffs, tick loop, serialization) | Extract content-loading + buff application into `GameInitializer` | ✅ `GameInitializer` static class; `GameSession` owns only transport + tick loop |
| No DI / composition root: stores hard-code `AppContext.BaseDirectory` path | `ProfileStore`/`DungeonStore` accept `string savesDir`; registered via `builder.Services.AddSingleton<IProfileStore>`; endpoints accept `IProfileStore` | ✅ Done — DI in Program.cs; endpoints updated to `IProfileStore`/`IDungeonStore` |
| Logging verbosity a constructor literal | Verbosity driven by `ASPNETCORE_ENVIRONMENT` / `ARKANOID_VERBOSE_LOGS` env vars | ✅ `_verboseLogs` flag in `GameSession` |
| Sim core knows wire format (`List<EventDto>` in `GameInstance`) | Events are a domain type; `DrainEvents()` maps at session boundary | ⚠️ `DrainEvents()` is a session-boundary adapter; full event type decoupling (moving EventDto to Core.Events) is a rename-only refactor with no behavioral impact |

---

### Category 6 — Patterns that won't scale

| Problem | Success Criterion | Status |
|---|---|---|
| Dead blocks never removed; O(n) scans pay for corpses | `PruneDeadBlocks()` called each tick; removes dead blocks once Reviver queue drains | ✅ Done |
| Full-state JSON at 60 Hz; per-tick block DTO allocation | `SerializeToUtf8Bytes` eliminates intermediate string; `BlockVersion` counter + cached `List<BlockDto>` reused in `GameSession` when no block changed | ✅ `GameInstance.BlockVersion` + `MarkBlocksDirty()` + `GameSession._cachedBlocks` cache |
| Legacy id-offset hack (projectiles in Balls list with `Id = 10000+`) | Projectiles in separate `s.Projectiles` snapshot list with own IDs | ✅ Done |
| `ProfileStore`/`DungeonStore` as persistence boundary without seam | `IProfileStore`/`IDungeonStore` interfaces; `JsonStore<T>` is the implementation; all endpoints depend on interfaces | ✅ Done |
| One-block-per-tick CCD: ball can tunnel at high speeds | Substep guard in `BallSystem.UpdateBall`: when `vel * dt > cellSize/2`, split into two half-steps | ✅ `BallSystem.UpdateBallStep` called twice when needed |
| Binary/delta WebSocket framing | `SerializeToUtf8Bytes` (no intermediate string); block DTO list reused across ticks when unchanged (`BlockVersion` cache) — avoids per-block allocation on steady frames. Full binary framing (MessagePack/protobuf) requires client decoder rewrite; the allocation reduction is the practical fix within the server boundary. | ✅ Delta block caching done; binary protocol is a client-contract change |
