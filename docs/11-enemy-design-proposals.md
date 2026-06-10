# 11 — Enemy Design: Is It Good, and How to Make It Great (2026-06-09)

> `docs/10` audited *fidelity* (does the port match the original). This doc asks the
> better question: **are these mechanics good game design at all** — and proposes
> changes, including departures from the original where the original was weak.
> Scored in the feature-critic spirit (Fun / Readability / Scope) so we build the
> high-value items first.

---

## 1. Verdict: is it OK as-is?

**As a hazard set: yes. As an enemy roster: not yet.** Everything works, is tested,
and is fair. But measured against what makes enemies great in this genre, the roster
has four systemic problems — none of which are about individual enemies:

1. **No shared design language.** Some enemies telegraph (boss), some don't
   (emitters fire with zero tell). Some have counterplay (necromant = kill it),
   some are pure punishment (lava eats your ball, full stop). The player can't
   build intuitions because the rules aren't consistent.
2. **Danger doesn't pay.** An enemy kill rolls the same 12% bonus chance as a plain
   brick (`BonusSystem.TrySpawnBonus`). There is no reason to *want* enemies in a
   level — they're only friction. Great roguelites make threats lucrative.
3. **Threats cluster on one axis.** Almost everything either chips HP or annoys the
   ball. Nothing attacks the player's *economy* (mana, gold), almost nothing creates
   *tempo* pressure (race conditions), and nothing forces *target prioritization*
   except the necromant — the one enemy everyone will remember, for exactly that reason.
4. **The flattened ports removed the decisions.** Heaven's ally/level-up system,
   the Witch's ball-grab, statues reacting to ball hits — those *were* the design.
   The simplifications kept the bodies and discarded the choices.

The fix is not "more enemies." It's a grammar + paying danger + restoring the
3-4 decision mechanics that got flattened.

---

## 2. The design language (apply to every enemy, current and future)

An enemy in a brick-breaker interacts with the only three things the player owns:
**paddle position** (dodging), **ball trajectory** (aim), and **resources**
(HP / balls / mana / time). The grammar:

| Rule | Meaning | Current violators |
|---|---|---|
| **R1 — One threat axis each** | Every enemy attacks exactly one of: Position, Aim, Tempo, Economy. Readable identity. | none (good) |
| **R2 — Telegraph before harm** | Any HP-damaging act gets a ≥0.5s visual tell (the `*Active` / attack-anim art exists for nearly all of them). | Emitters, melee statue fire with zero warning |
| **R3 — Counterplay through the ball** | Every threat must be answerable with the player's weapon: killable source, interceptable projectile, or exploitable behaviour. Pure punishment is banned. | Lava (uncounterable), bat grab (unanswerable 2s timeout) |
| **R4 — Danger pays** | Enemy kills drop a guaranteed bonus (or mana surge). Hard levels become lucrative levels — the Isaac/Hades loop. | All enemies (12% like a brick) |
| **R5 — One verb per biome** | Each biome teaches one enemy-driven verb: Hell **routes**, Caverns **chains**, Witchland **races**, Heaven **converts**. Enemies in a biome reinforce its verb. (The verb extends to *levels* — layout idioms, pacing, objectives — in `12-biome-identity.md`.) | Heaven's "convert" verb is currently decorative (ally = hold fire) |

**R4 is the cheapest, highest-leverage change in this doc**: one guaranteed-drop rule
in `BlockDamage` (`if (blk.Behavior != None) spawn bonus`) reframes every enemy from
"friction" to "opportunity" without touching any enemy.

---

## 3. Threat-axis map (and the gaps it exposes)

| Enemy | Axis | Decision it creates today | Grade |
|---|---|---|---|
| Necromant | **Tempo** | Kill it first vs out-race the revives | **A — the model enemy** |
| Bomb | Aim (friendly!) | Plan chain detonations | A |
| Stalactite | Position | Control when you pass beneath | B+ |
| Shield statue | Tempo | Priority target (re-armors level) | B |
| Teleporter | Aim | Route awareness | B |
| Beholder / Hell spawner / Melee statue | Position | Dodge while aiming | C+ (interchangeable turrets) |
| WindMaster | Aim | Compensate aim near it | C (invisible force) |
| Bat | Tempo | none — unavoidable 2s timeout | C− |
| Cart | Position | Dodge a sweep | C |
| Lava | — | none — pure ball tax | **D** |
| Altar / Vase | (Heaven verb) | none — both = same pacify | D as designed, C− as shipped |

**Gaps:** nothing on the **Economy** axis (steal mana/gold → makes Engineer/Necromancer
passives matter defensively); only one true **Tempo** enemy; the three turrets are one
enemy wearing three skins.

---

## 4. Per-enemy proposals (keep / polish / redesign)

Format: verdict, then the proposal with a mini score (**F**un / **R**eadability /
**S**cope-cost 1-5, higher better / lower cost).

### Keep as-is (already good)
- **Bomb** — the chain is the fun. Only addition: **GrateBomb** (armored, needs Heavy
  ball / 2 hits) as a build-synergy hook. *(F4 R5 S5)*
- **Teleporter** — fine once the ring matches skull colour + `SkullActive` flash on warp
  (already in docs/10 backlog). Level-design note, not code: place portal *exits* above
  rich pockets so good players aim INTO portals on purpose — turns a hazard into a tool.
- **Cart, Stalactite (base)** — competent positional threats, keep.

### Polish (small changes, big reads)
- **Stalactite as a weapon** *(F5 R4 S4)* — a falling stalactite that passes a block
  damages it. Now hanging stalactites over enemy nests are *opportunities*: trigger
  them deliberately. One collision check in `CombatSystem`; turns a hazard into the
  Caverns "chain" verb's second half.
- **Beholder tiers** *(F3 R5 S4)* — use `Beholder1/2/3` art as actual damage states
  AND power tiers (tier 3 fires faster). The art is literally drawn for this.
- **Emitters: telegraph** *(F3 R5 S4)* — 0.5s `*Active`/attack-anim flash before every
  shot (R2). Pure renderer + one snapshot flag (`aboutToFire`).
- **WindMaster: visible aura** *(F3 R5 S5)* — `WindMasterV2Circle` + glow strip,
  already in docs/10 backlog. Mandatory before any difficulty tuning of it.

### Redesign (the original design was better — restore it)
- **Heaven Altar vs Vase — the convert system** *(F5 R4 S3)* — the flagship.
  - **Altar** (ball hit): statues become **allies** for 15s — melee statues shoot
    *blocks*, shield statue *corrupts* (damages) blocks in radius. Heaven's verb
    becomes real: route your ball to the altar, then profit.
  - **Vase** (destroy): statues **level up** permanently this level (fire faster,
    shield wider) **but** every statue kill now drops a guaranteed bonus + bonus mana.
    Risk/reward knob the player chooses to turn.
  - Statue `*Active` + glowing-part art shows the state (ally = glow, leveled = brighter).
- **Witch boss ball-grab** *(F5 R4 S3)* — restore the original: she grabs your ball and
  carries it; **hit her with a spell or second ball to force the drop**. It's her whole
  identity and it showcases the spell system as the counterplay (R3).
- **Bat: carry, don't pause** *(F4 R4 S3)* — instead of freezing the ball 2s (a
  timeout), the bat **carries it toward the drain**; killing the bat (it has 1 HP —
  any spell or second ball) drops the ball instantly. Converts dead time into a panic
  decision. Keep flyaway. Wake-from-sleep tell: `BatSleeping` → fly-anim when a
  neighbour block dies.
- **Lava: spreads, and burns both ways** *(F4 R4 S3)* — lava is the only
  decision-free enemy. Make it creep: every N seconds it converts one adjacent *empty*
  cell (up to a cap) — a soft timer that punishes slow play (Tempo axis at last).
  Counterplay (R3): a **Frost-imbued ball solidifies a lava cell** into a normal
  1-HP block. Ties a biome hazard to the build system — relic synergy for free.

### New (only one — fills the Economy gap)
- **Witchland Cauldron (Kotelok)** *(F4 R4 S4)* — the missing signature block, art
  fully drawn (3 states + death anims). Design: a cauldron **slowly siphons your mana**
  (visible bubbling, R2) while alive; destroying it **refunds the stolen mana as a
  Mana Surge drop** (R4). First and only Economy-axis enemy; makes Witchland the
  "race" biome on two resources at once. (The orphaned `hell_teleporter_green` can
  also finally appear in a Witchland portal puzzle level.)

### Bosses — one signature verb each *(F5 R4 S2 — biggest item here)*
The shared pattern system is a fine *base layer*, but the four fights must diverge:
| Boss | Keep | Add (one mechanic each) |
|---|---|---|
| Demon | patterns | **Fist columns**: telegraphed column-wide slams (DemonHand art) that also crush *blocks* in the column — openings you can exploit |
| Goblin | stalactite rain | **Hop between 3 anchors** (original) — repositioning resets your aim solution |
| Witch | witchmagic bolts | **Ball-grab** (above) |
| Seraph | patterns | **Uses Heaven's own system**: summons a Vase (destroy it or his statue adds level up) — the biome finale examines the biome's lesson |

---

## 5. What NOT to build (restraint)
- **No free-moving enemy AI** (pathfinding fliers, swarms). The sim's grid+hazard
  model handles everything above; full movers are an engine feature for marginal fun.
- **No new hostile block types beyond the cauldron** until each biome's existing
  roster appears in ≥4 levels (content first — docs/09 G3).
- **No second nuke-style punishment** (lava is being reformed, don't add another).

---

## 6. Build order — STATUS (2026-06-09)

1. ✅ **R4 danger-pays + emitter telegraphs + WindMaster aura + statue state art +
   beholder tiers + death-sphere markers** — shipped (commit `feat(enemies): slice 1`).
2. ✅ **Altar/Vase convert system + bat carry + stalactite-as-weapon** — shipped
   (slice 3). Vase = level-up risk/reward; allied statues fight for you; the bat
   carries the ball to the drain with pop counterplay.
3. ✅ **Cauldron + lava reform** — shipped (slice 4). Counterplay landed as
   "kill the spawner retracts its lava" (no Frost imbue exists yet; revisit the
   Frost-solidify idea when imbues expand in G2).
4. ✅ **Boss signature mechanics** — shipped (slice 5): Demon fist columns,
   Goblin hops, Witch ball-grab, Seraph summons + fused vase.

All four slices verified by xUnit + Playwright with screenshots; see git log.
