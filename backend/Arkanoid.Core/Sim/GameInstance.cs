using System.Linq;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim.Systems;
namespace Arkanoid.Core.Sim;

public sealed class GameInstance
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
    private readonly List<Block> _blocks;
    public List<Block> Blocks => _blocks;
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

    internal sealed class PowerupState
    {
        internal bool   WidePaddleActive;
        internal double WidePaddleTimer;
        internal bool   SlowBallActive;
        internal double SlowBallTimer;
        internal bool   FireshotActive;
        internal double FireshotTimer;
        internal bool   AutoSaveActive;
    }
    internal readonly PowerupState Powerups = new();

    // Read-only snapshot surface — Snapshot.From uses these instead of private state.
    public bool   WidePaddleActive          => Powerups.WidePaddleActive;
    public double WidePaddleTimer           => Powerups.WidePaddleTimer;
    public bool   SlowBallActive            => Powerups.SlowBallActive;
    public double SlowBallTimer             => Powerups.SlowBallTimer;
    public bool   FireshotActive            => Powerups.FireshotActive;
    public double FireshotTimer             => Powerups.FireshotTimer;
    /// <summary>True while the powerup_shield auto-save is armed (distinct from the Paladin barrier spell).</summary>
    public bool   AutoSaveActive            => Powerups.AutoSaveActive;
    public int    ComboMultiplier           => Combo.Multiplier;
    public int    BricksDestroyedThisLevel  => Combo.BricksDestroyed;

    // --- Coin/crystal counter (coins bonus) ---
    public int Crystals { get; internal set; } = 0;

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

    internal double _turretRemaining;
    internal double _turretAccumulator;
    public bool TurretActive => _turretRemaining > 0;

    // --- Necromancer: Skeleton summon ---
    internal double _skeletonRemaining;
    internal double _skeletonAccumulator;
    public bool SkeletonActive => _skeletonRemaining > 0;

    // --- Necromancer: Drain ---
    internal double _drainRemaining;
    public bool SpellDrainActive => _drainRemaining > 0;

    // --- Lava: danger-zone HP drain ---
    internal double _lavaDrainAccumulator;
    internal readonly ISimLog _log;
    public RelicCatalog?  RelicCatalog  { get; }
    public BonusCatalog?  BonusCatalog  { get; }
    public long TickCount { get; private set; }

    public string Character { get; private set; } = "fire_mage";
    public void SetCharacter(string id) => Character = id;
    internal bool _wallSaveAvailable;

    public HashSet<string> ActiveRelics { get; } = new();
    public bool HasRelic(string id) => ActiveRelics.Contains(id);

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
    public void AddPaddleMod(string id)
    {
        if (!PaddleMods.Add(id)) return;
        _log.Log(TickCount, "paddlemod", "added", id);
        switch (id)
        {
            case "mod_wide": Paddle.Width *= Config.PaddleModWideMult; break;
            case "mod_grip": Paddle.DeflectAngleBonusDeg += Config.PaddleModGripBonusDeg; break;
            // mod_cannons is purely tick-driven (see SpellSystem.UpdateKitSpells)
        }
    }
    public double ManaMaxValue { get; internal set; }

    public Dictionary<string, int> SpellLevels { get; } = new()
    {
        ["ignite"]    = 1,
        ["fireball"]  = 1,
        ["firewall"]  = 1,
        ["turret"]    = 1,
        ["shield"]    = 1,
        ["spear"]     = 1,
        ["duplicate"] = 1,
        ["lightning"] = 1,
        ["rocket"]    = 1,
        ["radiation"] = 1,
        ["decay"]     = 1,
        ["skeleton"]  = 1,
        ["drain"]     = 1,
    };

    /// <summary>Overwrites spell levels from the given dictionary (unknown keys ignored; missing keys keep their current value).</summary>
    public void SetSpellLevels(IDictionary<string, int> levels)
    {
        foreach (var (k, v) in levels)
            if (SpellLevels.ContainsKey(k))
                SpellLevels[k] = v;
    }

    /// <summary>
    /// Per-class spell kits (slot index → spell id), keyed by character id.
    /// Must stay in sync with characters.json.
    /// </summary>
    private static readonly Dictionary<string, string[]> SpellKits = new()
    {
        ["fire_mage"]   = new[] { "ignite",    "fireball", "firewall",  "turret",    "phoenix"  },
        ["paladin"]     = new[] { "shield",    "spear",    "duplicate", "penetration", "lastday" },
        ["engineer"]    = new[] { "lightning", "rocket",   "radiation", "magnet",    "overload" },
        ["necromancer"] = new[] { "decay",     "skeleton", "drain",     "golem",     "mage"     },
    };

    /// <summary>
    /// Dispatch the spell in slot <paramref name="slot"/> (0-based) for the active character.
    /// No-ops if the character has no spell at that slot.
    /// </summary>
    public void CastSlot(int slot)
    {
        if (!SpellKits.TryGetValue(Character, out var kit)) return;
        if (slot < 0 || slot >= kit.Length) return;
        var spellId = kit[slot];
        SpellDispatch(spellId);
    }

    private void SpellDispatch(string spellId)
    {
        switch (spellId)
        {
            case "ignite":    CastIgnite();    break;
            case "fireball":  CastFireball();  break;
            case "firewall":  CastFireWall();  break;
            case "turret":    CastTurret();    break;
            case "shield":    Arkanoid.Core.Sim.Systems.SpellSystem.CastShield(this);    break;
            case "spear":     Arkanoid.Core.Sim.Systems.SpellSystem.CastSpear(this);     break;
            case "duplicate": Arkanoid.Core.Sim.Systems.SpellSystem.CastDuplicate(this); break;
            case "lightning": Arkanoid.Core.Sim.Systems.SpellSystem.CastLightning(this); break;
            case "rocket":    Arkanoid.Core.Sim.Systems.SpellSystem.CastRocket(this);    break;
            case "radiation": Arkanoid.Core.Sim.Systems.SpellSystem.CastRadiation(this); break;
            case "decay":     Arkanoid.Core.Sim.Systems.SpellSystem.CastDecay(this);     break;
            case "skeleton":  Arkanoid.Core.Sim.Systems.SpellSystem.CastSkeleton(this);  break;
            case "drain":     Arkanoid.Core.Sim.Systems.SpellSystem.CastDrain(this);     break;
            // G2c kit-completion spells
            case "phoenix":     Arkanoid.Core.Sim.Systems.SpellSystem.CastPhoenix(this);     break;
            case "penetration": Arkanoid.Core.Sim.Systems.SpellSystem.CastPenetration(this); break;
            case "lastday":     Arkanoid.Core.Sim.Systems.SpellSystem.CastLastDay(this);     break;
            case "magnet":      Arkanoid.Core.Sim.Systems.SpellSystem.CastMagnet(this);      break;
            case "overload":    Arkanoid.Core.Sim.Systems.SpellSystem.CastOverload(this);    break;
            case "golem":       Arkanoid.Core.Sim.Systems.SpellSystem.CastGolem(this);       break;
            case "mage":        Arkanoid.Core.Sim.Systems.SpellSystem.CastMage(this);        break;
        }
    }

    public GameInstance(LevelData level, SimConfig config, int seed, ISimLog? log = null, RelicCatalog? relics = null, BonusCatalog? bonuses = null)
    {
        Level = level; Config = config; Rng = new Rng(seed); _log = log ?? NullSimLog.Instance;
        RelicCatalog = relics;
        BonusCatalog = bonuses;
        // Deep-copy block lists so LevelData stays as an immutable template.
        _blocks      = level.Blocks.Select(b => b.Clone()).ToList();
        _extraFloors = level.ExtraFloors.Select(f => f.Select(b => b.Clone()).ToList()).ToList();
        Hp = config.StartHp;
        SpareBalls = config.StartBalls;
        ManaMaxValue = config.ManaMax;
        _wallSaveAvailable = true;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
        _log.Log(0, "init", "instance created", $"level={level.Id} seed={seed} blocks={Blocks.Count} hp={Hp} balls={SpareBalls}");
    }

    // --- G2 relic counters/flags (instance-scoped, reset with the level) ---
    internal int  _killsSinceSplit;
    internal int  _killsSinceSouljar;
    internal bool _secondWindUsed;
    internal bool _hellwalkerUsedThisServe;

    // --- G2c kit-completion spell state ---
    /// <summary>freezeMana cheat: suspends regen so HUD tests can assert stable values.</summary>
    internal bool _manaRegenFrozen;
    internal double _phoenixRemaining;
    internal double _phoenixAccum;
    internal bool   _penetrationArmed;
    internal double _lastDayRemaining;
    internal double _lastDayCooldown;
    internal double _magnetRemaining;

    public void AddRelic(string id)
    {
        ActiveRelics.Add(id);
        _log.Log(TickCount, "relic", "added", id);
        switch (id)
        {
            case "glass_cannon":
                Hp = System.Math.Max(1, Hp - 1);
                break;
            case "mana_battery":
                ManaMaxValue += Config.ManaBatteryBonus;
                break;
            case "lead_paddle":
                Paddle.Width *= Config.LeadPaddleWidthMult;
                break;
        }
    }

    internal void SpawnBallOnPaddle()
    {
        Balls.Clear();
        Balls.Add(new Ball {
            Id = _nextBallId++,
            Radius = Config.BallRadius,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height / 2 - Config.BallRadius - 1),
            Vel = new Vec2(0, 0),
            Alive = true
        });
        _igniteArmed = false;   // discard any unused arm so it doesn't leak to the next life
        _decayArmed  = false;
        _hellwalkerUsedThisServe = false; // the Hellwalker boon refreshes on every serve
        Phase = GamePhase.Serving;
    }

    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        // launch upward with a small deterministic horizontal lean
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
        _log.Log(TickCount, "serve", "ball launched", $"lean={lean:F3} vx={Balls[0].Vel.X:F1} vy={Balls[0].Vel.Y:F1}");

        // ghost ball-core: the served ball can punch through blocks (Phantom fusion: more charges)
        if (BallCores.Contains("ghost"))
        {
            Balls[0].PhasesLeft = HasFusion("ghost", "split")
                ? Config.PhantomPhaseCharges : Config.GhostCoreCharges;
            _log.Log(TickCount, "ballcore", "ghost charges", $"phases={Balls[0].PhasesLeft}");
        }

        // ember ball-core: permanently ignite every served ball
        if (BallCores.Contains("ember"))
        {
            foreach (var b in Balls)
                b.IgniteHitsLeft = System.Math.Max(b.IgniteHitsLeft, Config.EmberBallIgniteHits);
            _log.Log(TickCount, "ballcore", "ember ignite", $"hitsLeft={Config.EmberBallIgniteHits}");
        }

        // split ball-core: spawn extra balls next to the main one with slightly different velocity
        if (BallCores.Contains("split"))
        {
            var main = Balls[0];
            for (int i = 0; i < Config.SplitBallExtraBalls; i++)
            {
                // Deterministic lean offset: ±(i+1)*0.15 so each extra ball diverges
                var extraLean = lean + (i + 1) * 0.15 * (i % 2 == 0 ? 1 : -1);
                var extraBall = new Entities.Ball
                {
                    Id     = _nextBallId++,
                    Radius = Config.BallRadius,
                    Pos    = new Vec2(main.Pos.X + (i + 1) * (Config.BallRadius * 2 + 2), main.Pos.Y),
                    Vel    = new Vec2(extraLean, -1).Normalized() * Config.BallSpeed,
                    Alive  = true,
                };
                if (BallCores.Contains("ember"))
                    extraBall.IgniteHitsLeft = System.Math.Max(extraBall.IgniteHitsLeft, Config.EmberBallIgniteHits);
                Balls.Add(extraBall);
                _log.Log(TickCount, "ballcore", "split extra ball", $"id={extraBall.Id} lean={extraLean:F3}");
            }
        }
    }

    public void SetPaddleX(double x)
    {
        var half = Paddle.Width / 2;
        var clamped = System.Math.Clamp(x, half, Level.Grid.Width - half);
        Paddle.Center = new Vec2(clamped, Paddle.Center.Y);
        // Keep the ball on the paddle during the serve phase.
        if (Phase == GamePhase.Serving && Balls.Count > 0 && Balls[0].Alive)
            Balls[0].Pos = new Vec2(clamped, Paddle.Center.Y - Paddle.Height / 2 - Balls[0].Radius - 1);
    }

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        _blockGridDirty = true; // rebuild spatial index once on first BlockAt() call this tick
        TickCount++;
        ElapsedPlayTime += dt;
        if (_log.Verbose)
            _log.Log(TickCount, "tick", "", $"balls={Balls.Count(b=>b.Alive)} mana={ManaValue:F0} blocks={Blocks.Count(b=>!b.Dead)}");
        SpellSystem.RegenMana(this, dt);
        // Index loop: the Split Shot relic can append a ball mid-tick.
        for (int i = 0; i < Balls.Count; i++)
            BallSystem.UpdateBall(this, Balls[i], dt);
        SpellSystem.UpdateProjectiles(this, dt);
        SpellSystem.UpdateFireWalls(this, dt);
        SpellSystem.UpdateTurret(this, dt);
        SpellSystem.UpdateBarriers(this, dt);
        SpellSystem.UpdateZones(this, dt);
        SpellSystem.UpdateSkeleton(this, dt);
        SpellSystem.UpdateDrain(this, dt);
        SpellSystem.UpdateKitSpells(this, dt);
        BossSystem.Update(this, dt);
        BossSystem.UpdateVaseFuses(this, dt);
        EmitterSystem.Update(this, dt);
        StalactiteSystem.Update(this, dt);
        ReviverSystem.Update(this, dt);
        WindSystem.Update(this, dt);
        ShieldSystem.Update(this, dt);
        CauldronSystem.Update(this, dt);
        LavaSystem.Update(this, dt);
        PacingSystem.Update(this, dt);
        CombatSystem.UpdateHazards(this, dt);
        BonusSystem.UpdateBonuses(this, dt);
        WinLoseSystem.ResolveDrainAndWin(this);
        PruneDeadBlocks();
    }

    // Prune dead blocks once no Reviver can resurrect them, keeping linear scans short.
    // The _reviveQueue drains naturally via ReviverSystem (raising reviveCancelled).
    private void PruneDeadBlocks()
    {
        if (Blocks.Any(b => !b.Dead && b.Reviver)) return;
        if (_reviveQueue.Count > 0) return; // wait for the queue to drain; reviveCancelled events still fire
        int removed = _blocks.RemoveAll(b => b.Dead);
        if (removed > 0) InvalidateBlockGrid();
    }

    // --- resources/events surface ---
    public double ManaValue { get; set; } = 0;
    private readonly List<SimEvent> _events = new();
    public void RaiseEvent(SimEventKind kind, double x, double y, int payload = 0)
        => _events.Add(new SimEvent(kind, x, y, payload));
    public List<SimEvent> DrainEvents()
    { var copy = new List<SimEvent>(_events); _events.Clear(); return copy; }

    public void CastFireball()  => SpellSystem.CastFireball(this);
    public void CastIgnite()    => SpellSystem.CastIgnite(this);
    public void CastFireWall()  => SpellSystem.CastFireWall(this);
    public void CastTurret()    => SpellSystem.CastTurret(this);

    public void DamagePlayer(int dmg) => CombatSystem.DamagePlayer(this, dmg);

    internal bool _igniteArmed = false;
    internal bool _decayArmed  = false;

    public void ApplyCheat(string op, double value) => CheatHandler.Apply(this, op, value);
}
