using System.Linq;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim.Systems;
using Arkanoid.Core.Spells;
namespace Arkanoid.Core.Sim;

public sealed partial class GameInstance
{
    public SimConfig Config { get; }
    public LevelData Level { get; }
    public Rng Rng { get; internal set; }

    public GamePhase Phase { get; internal set; } = GamePhase.Serving;
    /// <summary>Player HP: reduced by enemy damage. SpareBalls tracks the ball-count axis.</summary>
    public int Hp { get; internal set; }
    public int SpareBalls { get; internal set; }

    public Paddle Paddle { get; }
    public List<Ball> Balls { get; } = new();
    /// <summary>
    /// Mutable game-state copy of the level's blocks. Deep-copied from LevelData at construction
    /// so that LevelData remains an immutable template (safe for caching/reuse across sessions).
    /// </summary>
    private readonly BlockList _blocks;
    /// <summary>The mutable block collection. Structural changes auto-invalidate the
    /// spatial index + snapshot version (see <see cref="BlockList"/>).</summary>
    public BlockList Blocks => _blocks;
    /// <summary>Extra floors (multi-floor Caverns collapse). Deep-copied from LevelData, consumed in order.</summary>
    private readonly List<List<Block>> _extraFloors;
    internal List<List<Block>> ExtraFloors => _extraFloors;
    /// <summary>Y coordinate below which balls/hazards/bonuses are considered drained.
    /// One authoritative expression so the three callers never drift.</summary>
    internal double DrainY => Level.Grid.Height + Config.CellSize * 2;

    // Spatial block index — rebuilt once per tick on first BlockAt() call.
    private readonly Dictionary<(int, int), Block> _blockGrid = new();
    private bool _blockGridDirty = true;
    internal void InvalidateBlockGrid() { _blockGridDirty = true; BlockVersion++; }
    /// <summary>
    /// Increments whenever the block list or any block's HP/visual state changes.
    /// GameSession uses this to skip rebuilding BlockDto objects on unchanged ticks.
    /// </summary>
    public int BlockVersion { get; private set; } = 1;
    internal void MarkBlocksDirty() => BlockVersion++;

    internal Block? BlockAt(int col, int row)
    {
        if (_blockGridDirty)
        {
            _blockGrid.Clear();
            foreach (var b in Blocks)
                if (!b.Dead) _blockGrid[(b.Col, b.Row)] = b;
            _blockGridDirty = false;
        }
        return _blockGrid.TryGetValue((col, row), out var found) && !found.Dead ? found : null;
    }

    internal int _nextBallId = 1;
    internal int _nextProjId = 1;

    internal sealed class ComboState
    {
        internal int BricksDestroyed;
        internal int Count;
        internal int Multiplier = 1;
    }
    internal readonly ComboState Combo = new();
    public List<Projectile> Projectiles { get; } = new();

    internal int _nextWallId = 1;
    public List<FireWall> FireWalls { get; } = new();

    internal int _nextPhoenixId = 1;
    /// <summary>Fire-Mage Phoenix entities orbiting balls (visible, serialized for the renderer).</summary>
    public List<Phoenix> Phoenixes { get; } = new();

    internal int _nextBarrierId = 1;
    /// <summary>Paladin Shield barriers — reflect downward balls upward.</summary>
    public List<Arkanoid.Core.Entities.Barrier> Barriers { get; } = new();

    internal int _nextZoneId = 1;
    /// <summary>Engineer Radiation AoE damage zones.</summary>
    public List<Arkanoid.Core.Entities.Zone> Zones { get; } = new();

    internal int _nextHazardId = 1;
    /// <summary>Falling hazards spawned by boss blocks; hitting the paddle damages player HP.</summary>
    public List<Projectile> Hazards { get; } = new();

    internal int _nextBonusId = 1;
    /// <summary>Falling bonus pickups dropped by destroyed blocks.</summary>
    public List<Bonus> Bonuses { get; } = new();

    private int _nextBlockId;
    /// <summary>Id for runtime-spawned blocks (creeping lava) — continues after the level's ids.</summary>
    public int NextBlockId()
    {
        if (_nextBlockId == 0)
            _nextBlockId = Blocks.Count == 0 ? 1 : Blocks.Max(b => b.Id) + 1;
        return _nextBlockId++;
    }

    /// <summary>Blocks queued for Reviver revival: (block, seconds remaining).</summary>
    internal readonly List<(Entities.Block Block, double Timer)> _reviveQueue = new();

    internal bool _autoSaveActive;
    internal readonly List<ActiveEffect> _effects = new();

    // Read-only snapshot surface — Snapshot.From uses these instead of private state.
    public bool   WidePaddleActive => EffectSystem.HasEffect(this, "wide_paddle");
    public double WidePaddleTimer  => EffectSystem.RemainingOf(this, "wide_paddle");
    public bool   SlowBallActive   => EffectSystem.HasEffect(this, "slow_ball");
    public double SlowBallTimer    => EffectSystem.RemainingOf(this, "slow_ball");
    public bool   FireshotActive   => EffectSystem.HasEffect(this, "fireshot");
    public double FireshotTimer    => EffectSystem.RemainingOf(this, "fireshot");
    /// <summary>True while the powerup_shield auto-save is armed (distinct from the Paladin barrier spell).</summary>
    public bool   AutoSaveActive   => _autoSaveActive;
    public int    ComboMultiplier           => Combo.Multiplier;
    public int    BricksDestroyedThisLevel  => Combo.BricksDestroyed;

    public int Crystals { get; internal set; } = 0;

    /// <summary>In-run spending currency (docs/04 §5 "Gold / Treasure"). Drops from blocks/chests via
    /// the coins pickup; spent at dungeon shop floors and campaign rest nodes. Unlike <see cref="Crystals"/>
    /// it is NOT awarded to the Profile at level complete — it is carried across dungeon floors via
    /// <c>DungeonRun.Gold</c> and re-applied with <see cref="SetGold"/> when the next floor is built.</summary>
    public int Gold { get; internal set; } = 0;

    internal sealed class BossState
    {
        internal double AttackAccumulator;
        internal int    Phase;
        internal bool   TelegraphPending;
        internal double TelegraphTimer;
        internal int    PendingPattern;
        internal int    FistCol            = -1;
        internal int    GoblinAnchorIdx;
        internal bool   SeraphSummonVase;
    }
    internal readonly BossState Boss = new();

    // --- Objective timers + pacing modes (docs/12) ---
    /// <summary>Seconds of Playing-phase time elapsed (drives timeLimit/surviveTime).</summary>
    public double ElapsedPlayTime { get; internal set; }
    /// <summary>Multi-floor collapse: current floor (0 = the level's first layout).</summary>
    public int FloorIndex { get; internal set; }
    internal double _descendAccumulator;
    internal double _escalateAccumulator;

    /// <summary>Continuous Rift (2026-06-16): the whole rift plays as one GameInstance whose floors are
    /// ExtraFloors. In rift mode, clearing a floor slides the next in AND relocates surviving indestructible
    /// "leftovers" to the side columns (they accumulate as a growing hazard) instead of dropping them.</summary>
    public bool RiftMode { get; internal set; }
    public void SetRiftMode(bool v) => RiftMode = v;
    /// <summary>Rift end-reward multiplier (Prospector/Cursed Bounty §8). Drives the live banked-reward HUD.</summary>
    public double RiftRewardMult { get; set; } = 1.0;
    // §8 mid-rift modifier draft (2026-06-16): on a floor clear the sim FREEZES and offers a 1-of-3 draft;
    // the pick is applied live and then the next floor slides in.
    internal bool _awaitingRiftDraft;
    public bool AwaitingRiftDraft => _awaitingRiftDraft;
    internal readonly System.Collections.Generic.List<string> _riftDraftChoices = new();
    public System.Collections.Generic.IReadOnlyList<string> RiftDraftChoices => _riftDraftChoices;
    internal readonly System.Collections.Generic.List<string> _riftModifiersTaken = new();
    internal int _riftExtraEmitters;
    private int _riftDraftCount;

    public bool TurretActive     => EffectSystem.HasEffect(this, "turret");
    /// <summary>§3: "Skeleton" is now Bonewalker — true while a rooftop-walking minion is alive.</summary>
    public bool SkeletonActive   => Minions.Any(m => m.Kind == "bonewalker" && m.Alive);
    public bool SpellDrainActive => EffectSystem.HasEffect(this, "drain");

    internal double _lavaDrainAccumulator;
    /// <summary>Drain spell cap: bonus mana budget remaining for the current cast (resets to 40 on each Drain cast).</summary>
    internal double _drainBonusLeft;

    // ── Overload charge state (Overload rework: arm→ball-hit→timed detonation) ─────────────
    /// <summary>True after Overload is cast: the next ball-block hit plants a charge.</summary>
    internal bool _overloadArmed;
    /// <summary>Chain-explosion radius (cells) determined at cast time from SpellDef + level.</summary>
    internal int _overloadRadius;
    /// <summary>Seconds until the planted charge detonates (0 = no active charge).</summary>
    internal double _overloadChargeTimer;
    /// <summary>Grid column of the planted charge (-1 = none).</summary>
    internal int _overloadChargeCol = -1;
    /// <summary>Grid row of the planted charge (-1 = none).</summary>
    internal int _overloadChargeRow = -1;
    /// <summary>Remaining post-hit damage-immunity (i-frames), seconds. >0 ⇒ the player can't be damaged.</summary>
    internal double _damageImmunity;
    /// <summary>True while the player is in post-hit i-frames (for the snapshot / paddle flash).</summary>
    public bool DamageImmune => _damageImmunity > 0;

    /// <summary>New Game+ ball-speed bonus added to both BallSpeed and BallSpeedMax (set by PrestigeService).</summary>
    internal double _ngSpeedBonus;
    /// <summary>Difficulty rework (2026-06-16): the ball accelerates over the level's first
    /// BallAccelSeconds from BallSpeed up to BallSpeedMax (+ NG+ bonus). Used as a rising speed FLOOR
    /// (never lowers the ball — card boosts above it still apply) and as the slow-time reference.</summary>
    public double RampedBallSpeed
    {
        get
        {
            double baseS = Config.BallSpeed + _ngSpeedBonus;
            double maxS  = Config.BallSpeedMax + _ngSpeedBonus;
            double t = Config.BallAccelSeconds > 0
                ? System.Math.Min(1.0, ElapsedPlayTime / Config.BallAccelSeconds) : 1.0;
            return baseS + (maxS - baseS) * t;
        }
    }
    internal readonly ISimLog _log;
    private readonly RelicCatalog _relicCatalog;
    public  RelicCatalog RelicCatalog => _relicCatalog;

    /// <summary>Primary numeric magnitude from a relic's JSON definition (0 if relic unknown).</summary>
    internal double RelicMagnitude(string id)  { _relicCatalog.TryGet(id, out var d); return d?.Magnitude ?? 0; }
    /// <summary>Secondary magnitude (e.g. regen multiplier); falls back to <paramref name="fallback"/> if unset.</summary>
    internal double RelicMagnitude2(string id, double fallback = 1.0) { _relicCatalog.TryGet(id, out var d); return d != null && d.Magnitude2 != 0 ? d.Magnitude2 : fallback; }
    /// <summary>Threshold value from a relic's JSON definition (0 if relic unknown).</summary>
    internal double RelicThreshold(string id)  { _relicCatalog.TryGet(id, out var d); return d?.Threshold ?? 0; }

    public BonusCatalog? BonusCatalog { get; }
    public long TickCount { get; private set; }

    public string Character { get; private set; } = "fire_mage";
    public void SetCharacter(string id) => Character = id;
    internal bool _wallSaveAvailable;

    /// <summary>True when this battle is a dungeon miniboss floor (docs/04 §6.2) — drives the HUD banner.</summary>
    public bool MinibossFloor { get; private set; }
    public void SetMinibossFloor(bool v) => MinibossFloor = v;

    /// <summary>Set starting HP (dungeon floors restore the run's carried HP — docs/04 §6.2).</summary>
    public void SetHp(int hp) => Hp = System.Math.Max(1, hp);

    /// <summary>Restore the run's carried Gold when a dungeon floor's instance is built (docs/04 §5).
    /// Clamped to ≥ 0 — Gold is never negative.</summary>
    public void SetGold(int gold) => Gold = System.Math.Max(0, gold);

    /// <summary>The active equipped spell loadout (ordered, slot 0 = signature). Drives both the
    /// hotbar (via the snapshot) and CastSlot. Empty ⇒ CastSlot falls back to the character's full
    /// kit (back-compat for sim tests that don't equip a loadout). docs/04 §3/§4.1.</summary>
    public List<string> Loadout { get; } = new();

    /// <summary>Replace the active loadout (e.g. from the player's profile at level start, or an
    /// in-run draft appending to it). Slot 0 should be the signature.</summary>
    public void SetLoadout(IEnumerable<string> spellIds)
    {
        Loadout.Clear();
        Loadout.AddRange(spellIds);
    }

    /// <summary>Append a drafted spell to the loadout (in-run pick), respecting a max slot count.
    /// No-op if already present or at capacity. Returns true if added.</summary>
    public bool DraftSpell(string spellId, int maxSlots)
    {
        if (Loadout.Contains(spellId) || Loadout.Count >= maxSlots) return false;
        Loadout.Add(spellId);
        return true;
    }

    public HashSet<string> ActiveRelics { get; } = new();
    public bool HasRelic(string id) => ActiveRelics.Contains(id);

    // --- §1 Cards (cross-hero rule-breaking PASSIVE triggers; set at run start by CardEffects.Apply) ---
    private readonly Dictionary<string, int> _activeCards = new();
    /// <summary>Equipped card level this run (0 = not equipped). Drives the §1 card triggers.</summary>
    public int CardLevel(string id) => _activeCards.TryGetValue(id, out var l) ? l : 0;
    /// <summary>True if the given §1 card is equipped this run.</summary>
    public bool HasCard(string id) => _activeCards.ContainsKey(id);
    /// <summary>Fast-path: any cards equipped at all (skips per-hit card work when none are).</summary>
    internal bool AnyCards => _activeCards.Count > 0;
    /// <summary>Set the run's active cards (id→level) from the equipped loadout. 0-level entries are ignored.</summary>
    public void SetCards(System.Collections.Generic.IReadOnlyDictionary<string, int> cards)
    { _activeCards.Clear(); foreach (var kv in cards) if (kv.Value > 0) _activeCards[kv.Key] = kv.Value; }
    /// <summary>Opening Gambit (§1): the once-per-level first-kill AoE has already fired this level.</summary>
    internal bool _cardOpeningGambitUsed;

    // --- §2 Modules (slot-bound passives; set at run start by ModuleEffects.Apply) ---
    private readonly Dictionary<string, int> _activeModules = new();
    /// <summary>Equipped module level this run (0 = not equipped). Drives the §2 module passives.</summary>
    public int ModuleLevel(string id) => _activeModules.TryGetValue(id, out var l) ? l : 0;
    /// <summary>True if the given §2 module is equipped this run.</summary>
    public bool HasModule(string id) => _activeModules.ContainsKey(id);
    internal bool AnyModules => _activeModules.Count > 0;
    /// <summary>Set the run's active modules (id→level) from the equipped loadout.</summary>
    public void SetModules(System.Collections.Generic.IReadOnlyDictionary<string, int> mods)
    { _activeModules.Clear(); foreach (var kv in mods) if (kv.Value > 0) _activeModules[kv.Key] = kv.Value; }

    // --- Spell affinity (economy rework §3: a spell on its matching-element hero spends less mana) ---
    private readonly HashSet<string> _affinitySpells = new();
    private double _affinityCostMult = 1.0;
    /// <summary>Register the equipped spell ids that MATCH the hero's element + the cost multiplier they get
    /// (economy rework §3). Applied by the shared mana gate (<see cref="Systems.SpellSystem"/>.Spend).</summary>
    public void SetSpellAffinity(System.Collections.Generic.IEnumerable<string> matchedSpellIds, double costMult)
    { _affinitySpells.Clear(); foreach (var id in matchedSpellIds) _affinitySpells.Add(id); _affinityCostMult = costMult; }
    /// <summary>The mana cost of a spell after its affinity discount (unchanged when no match).</summary>
    internal double AffinityCost(string spellId, double cost)
        => _affinitySpells.Contains(spellId) ? cost * _affinityCostMult : cost;

    /// <summary>Tidal Core (§2): false = HEAVY mode (bonus damage), true = SWIFT mode (bonus speed). Toggles each deflect.</summary>
    internal bool _tidalSwift;
    /// <summary>Horizontal paddle velocity (px/tick) — Gyro Paddle reads it to drive the deflect angle.</summary>
    internal double _paddleVelX;
    internal double _paddlePrevX = double.NaN;
    /// <summary>Toll Roads (§2): seconds remaining in the "paid" window opened by a perfect deflect — kills
    /// during it (or crit kills) earn gold; other kills earn nothing.</summary>
    internal double _tollPerfectWindow;
    /// <summary>Pressure Cooker (§2): seconds accumulated toward the next field descent.</summary>
    internal double _pressureDescendAccum;
    /// <summary>Pressure Cooker (§2): kills accumulated toward pushing the field back up one row.</summary>
    internal int _pressureKills;
    /// <summary>Twin Soul Core (§2): seconds accumulated toward the next tether slice.</summary>
    internal double _twinTetherAccum;
    /// <summary>Twin Soul Core (§2): the live tether segment endpoints for the renderer (null when no tether).</summary>
    public (double X1, double Y1, double X2, double Y2)? TwinTether { get; internal set; }
    /// <summary>Fission Core (§2): kills accumulated toward the next ball split.</summary>
    internal int _fissionKills;
    /// <summary>Metronome (§1): consecutive PERFECT deflects; each adds ball damage, any non-perfect resets it.</summary>
    internal int _metronomeStacks;
    /// <summary>Domino (§1): block-death ticks within the rolling 1s window; ≥3 arms the next-death explosion.</summary>
    internal readonly System.Collections.Generic.List<long> _dominoDeaths = new();
    internal bool _dominoArmed;
    internal bool _dominoExploding;

    // --- Crit (stat engine, set at run start from hero stats + masteries + cards) ---
    /// <summary>Design §5.10 LOCKED caps: crit chance ≤ 75%, crit damage ≤ ×4.</summary>
    public const double CritChanceCap = 0.75;
    public const double CritDamageCap = 4.0;
    /// <summary>Probability (0..1) a ball hit on a block crits. Capped at 0.75 by the setter.</summary>
    public double CritChance { get; internal set; } = 0;
    /// <summary>Crit damage multiplier (e.g. 2.0 = double). Clamped to [1.0, 4.0] by the setter.</summary>
    public double CritDamage { get; internal set; } = 2.0;
    /// <summary>True iff the most recent ball→block hit critted (read by crit-synergy cards).</summary>
    public bool LastHitWasCrit { get; internal set; } = false;

    /// <summary>Set crit stats for the run (hero base + level + ★ + perks + masteries + cards).
    /// Both caps are LOCKED in design §5.10: chance ≤ 75%, damage ≤ ×4 (floored at ×1).</summary>
    public void SetCrit(double chance, double damage)
    {
        CritChance = System.Math.Clamp(chance, 0, CritChanceCap);
        CritDamage = System.Math.Clamp(damage, 1.0, CritDamageCap);
    }

    // --- Stat engine (design §5: Heroes-are-your-stats; set at run start by StatEngine.Apply) ---
    /// <summary>Resolved Power = base ball damage per hit (§5.1). 0 ⇒ fall back to Config.BallDamage
    /// (keeps legacy/test instances unchanged). Crit multiplies this.</summary>
    public int StatPower { get; internal set; } = 0;
    /// <summary>Resolved Tempo (§5.1): multiplies mana-regen (and paddle speed where applicable). 1.0 = neutral.</summary>
    public double Tempo { get; internal set; } = 1.0;
    /// <summary>Resolved Multiball (§5.1): extra balls launched on each serve, capped +2 (§5.10).</summary>
    public int StatMultiball { get; internal set; } = 0;
    /// <summary>Resolved max HP (Vitality) the run started at — basis for HP-threshold perks (§5.5 Pal ★5).</summary>
    public int StatMaxHp { get; internal set; } = 0;

    // --- Behavioral hero perks (§5.5, ★3/★5): active perk ids set at run start from hero + ★. ---
    private readonly System.Collections.Generic.HashSet<string> _activePerks = new();
    /// <summary>True if the given §5.5 behavioral perk id is active this run.</summary>
    public bool HasPerk(string id) => _activePerks.Contains(id);
    /// <summary>Set the active behavioral perks for the run (from <see cref="Arkanoid.Core.Meta.StatResolver"/>).</summary>
    public void SetPerks(System.Collections.Generic.IEnumerable<string> perks)
    {
        _activePerks.Clear();
        foreach (var p in perks) _activePerks.Add(p);
    }
    /// <summary>Running kill count for cadence perks (Necromancer ★1: heal 1 HP per 60 kills).</summary>
    internal int _perkKillCounter;
    /// <summary>Paladin ★3: the once-per-level "first ball-drain is saved" perk save is still available.</summary>
    internal bool _perkSaveAvailable = true;
    /// <summary>Ashfall (§3, Fire Mage): seconds remaining during which ignite-kills rain vertical embers.</summary>
    internal double _ashfallTimer;
    /// <summary>Reckoning (§3, Paladin): armed for the level once cast; its meter charges from HP lost.</summary>
    internal bool _reckoningArmed;
    /// <summary>Reckoning meter — accumulated HP lost; at threshold it auto-smites the board and drains.</summary>
    internal int _reckoningMeter;
    /// <summary>Tesla Grid (§3, Engineer): armed for the level; side-wall bounces charge each wall, and
    /// when BOTH are charged a horizontal lightning curtain fires and both reset.</summary>
    internal bool _teslaArmed;
    internal bool _teslaLeftCharged;
    internal bool _teslaRightCharged;
    /// <summary>Seconds until Tesla Grid can fire its next curtain (prevents multiball/bouncy-ball spam).</summary>
    internal double _teslaCooldown;
    /// <summary>Lich's Gaze (§3): bonus ball damage dealt to a cursed block (set on cast, scales with level).</summary>
    public int LichCurseBonus { get; internal set; } = 2;
    /// <summary>The active Lich's Gaze sweeping beam, or null. Curses blocks it crosses.</summary>
    public Arkanoid.Core.Entities.LichBeam? LichBeam { get; internal set; }
    internal int _nextBeamId;
    /// <summary>Lance of Dawn (§3): active temporary solid pillars the ball banks off.</summary>
    public List<Arkanoid.Core.Entities.Pillar> Pillars { get; } = new();
    internal int _nextPillarId;
    /// <summary>Bonewalker / Bone Golem (§3 Necromancer summons): active friendly minion entities.</summary>
    public List<Arkanoid.Core.Entities.Minion> Minions { get; } = new();
    internal int _nextMinionId;

    // --- Equipped-item passive bonuses (set by ItemEffects.Apply before first tick) ---
    /// <summary>Extra ball damage from equipped items (ball_damage effect, accumulated).</summary>
    public int    ItemBallDamageBonus    { get; set; } = 0;
    /// <summary>Extra max mana from equipped items (max_mana effect, accumulated). Applied by ItemEffects.Commit.</summary>
    public double ItemMaxManaBonus       { get; set; } = 0;
    /// <summary>Additive bonus to the mana-regen multiplier from equipped items (mana_regen effect).</summary>
    public double ItemManaRegenMultBonus { get; set; } = 0;
    /// <summary>Bonus damage vs tough blocks from equipped items (crit_tough effect).</summary>
    public int    ItemCritToughBonus     { get; set; } = 0;
    /// <summary>Extra crystals awarded at level clear from equipped items (treasure effect).</summary>
    public int    ItemCrystalBonus       { get; set; } = 0;
    /// <summary>Additive bonus to the kill-mana multiplier from equipped items (kill_mana effect).</summary>
    public double ItemKillManaMultBonus  { get; set; } = 0;

    public HashSet<string> BallCores { get; } = new();
    public void AddBallCore(string id) => BallCores.Add(id);
    /// <summary>Fusion (docs/04 §4.3): holding both cores of a pair unlocks a combined effect.</summary>
    public bool HasFusion(string a, string b) => BallCores.Contains(a) && BallCores.Contains(b);

    /// <summary>Paddle mods — the fourth build axis (docs/04 §4.4).</summary>
    public HashSet<string> PaddleMods { get; } = new();
    internal double _cannonAccumulator;
    public double ManaMaxValue { get; internal set; }

    /// <summary>Per-spell upgrade levels; absent keys default to 1 in SpellLevel().</summary>
    public Dictionary<string, int> SpellLevels { get; } = new();
    /// <summary>Returns the upgrade level for a spell (1 = base).</summary>
    internal int SpellLevel(string id) => SpellLevels.TryGetValue(id, out var l) ? l : 1;

    private readonly SpellCatalog _spellCatalog;
    private readonly Arkanoid.Core.Meta.CharacterCatalog _charCatalog;

    /// <summary>Display info (name/icon/manaCost) for a spell id from the shared catalog, or null.</summary>
    public Arkanoid.Core.Meta.SpellSlotDef? SpellDisplay(string id) => _charCatalog.DisplayOf(id);
    /// <summary>The current character's signature spell id (locked hotbar slot 0).</summary>
    public string SignatureId => _charCatalog.TryGet(Character, out var c) ? c.SignatureId : "";

    // --- G2 relic counters/flags (instance-scoped, reset with the level) ---
    internal int  _killsSinceSplit;
    internal int  _killsSinceSouljar;
    internal bool _secondWindUsed;
    internal bool _hellwalkerUsedThisServe;

    // --- G2c kit-completion spell state ---
    /// <summary>freezeMana cheat: suspends regen so HUD tests can assert stable values.</summary>
    internal bool _manaRegenFrozen;
    internal bool   _penetrationArmed;
    internal double _lastDayCooldown;

    // --- resources/events surface ---
    public double ManaValue { get; set; } = 0;
    private readonly List<SimEvent> _events = new();

    internal bool _igniteArmed = false;
    internal bool _decayArmed  = false;
}
