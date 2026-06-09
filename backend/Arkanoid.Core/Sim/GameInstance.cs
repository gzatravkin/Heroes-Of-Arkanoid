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
    public int Lives { get; internal set; }
    public int SpareBalls { get; internal set; }

    public Paddle Paddle { get; }
    public List<Ball> Balls { get; } = new();
    public List<Block> Blocks => Level.Blocks;

    internal int _nextBallId = 1;
    internal int _nextProjId = 1;
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

    /// <summary>Blocks queued for Necromant revival: (block, seconds remaining).</summary>
    internal readonly List<(Entities.Block Block, double Timer)> _reviveQueue = new();

    // --- Temp-effect state (wide_paddle / slow_ball) ---
    internal bool   _widePaddleActive = false;
    internal double _widePaddleTimer  = 0;
    internal bool   _slowBallActive   = false;
    internal double _slowBallTimer    = 0;

    // --- Coin/crystal counter (coins bonus) ---
    public int Crystals { get; internal set; } = 0;

    // --- Boss multi-pattern state ---
    internal double _bossAttackAccumulator;
    /// <summary>Current boss phase (1/2/3). 0 = not yet computed.</summary>
    internal int    _bossPhase = 0;
    /// <summary>True while we are in the telegraph window before the next attack fires.</summary>
    internal bool   _bossTelegraphPending;
    /// <summary>Seconds remaining in the current telegraph window.</summary>
    internal double _bossTelegraphTimer;
    /// <summary>Encoded pattern index queued to fire when the telegraph expires.</summary>
    internal int    _bossPendingPattern;

    internal double _turretRemaining;
    internal double _turretAccumulator;
    public bool TurretActive => _turretRemaining > 0;

    // --- Necromancer: Skeleton summon ---
    internal double _skeletonRemaining;
    internal double _skeletonAccumulator;
    public bool SkeletonActive => _skeletonRemaining > 0;

    // --- Necromancer: Drain ---
    internal double _drainRemaining;
    public bool DrainActive => _drainRemaining > 0;
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
    public int    ItemTreasureBonus      { get; set; } = 0;
    /// <summary>Additive bonus to the kill-mana multiplier from equipped items (kill_mana effect).</summary>
    public double ItemKillManaMultBonus  { get; set; } = 0;

    public HashSet<string> BallCores { get; } = new();
    public void AddBallCore(string id) => BallCores.Add(id);
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
        ["fire_mage"]   = new[] { "ignite",   "fireball",  "firewall",  "turret"    },
        ["paladin"]     = new[] { "shield",   "spear",     "duplicate"              },
        ["engineer"]    = new[] { "lightning","rocket",    "radiation"              },
        ["necromancer"] = new[] { "decay",    "skeleton",  "drain"                 },
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
        }
    }

    public GameInstance(LevelData level, SimConfig config, int seed, ISimLog? log = null, RelicCatalog? relics = null, BonusCatalog? bonuses = null)
    {
        Level = level; Config = config; Rng = new Rng(seed); _log = log ?? NullSimLog.Instance;
        RelicCatalog = relics;
        BonusCatalog = bonuses;
        Lives = config.StartLives;
        SpareBalls = config.StartBalls;
        ManaMaxValue = config.ManaMax;
        _wallSaveAvailable = true;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
        _log.Log(0, "init", "instance created", $"level={level.Id} seed={seed} blocks={Blocks.Count} lives={Lives} balls={SpareBalls}");
    }

    public void AddRelic(string id)
    {
        ActiveRelics.Add(id);
        _log.Log(TickCount, "relic", "added", id);
        switch (id)
        {
            case "glass_cannon":
                Lives = System.Math.Max(1, Lives - 1);
                break;
            case "mana_battery":
                ManaMaxValue += Config.ManaBatteryBonus;
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
    }

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        TickCount++;
        if (_log.Verbose)
            _log.Log(TickCount, "tick", "", $"balls={Balls.Count(b=>b.Alive)} mana={ManaValue:F0} blocks={Blocks.Count(b=>!b.Dead)}");
        SpellSystem.RegenMana(this, dt);
        foreach (var b in Balls)
            BallSystem.UpdateBall(this, b, dt);
        SpellSystem.UpdateProjectiles(this, dt);
        SpellSystem.UpdateFireWalls(this, dt);
        SpellSystem.UpdateTurret(this, dt);
        SpellSystem.UpdateBarriers(this, dt);
        SpellSystem.UpdateZones(this, dt);
        SpellSystem.UpdateSkeleton(this, dt);
        SpellSystem.UpdateDrain(this, dt);
        BossSystem.Update(this, dt);
        EmitterSystem.Update(this, dt);
        StalactiteSystem.Update(this, dt);
        NecromantSystem.Update(this, dt);
        WindSystem.Update(this, dt);
        ShieldSystem.Update(this, dt);
        CombatSystem.UpdateHazards(this, dt);
        BonusSystem.UpdateBonuses(this, dt);
        WinLoseSystem.ResolveDrainAndWin(this);
    }

    // --- resources/events surface ---
    public double ManaValue { get; set; } = 0;
    private readonly List<Arkanoid.Core.Net.EventDto> _events = new();
    public void RaiseEvent(string type, double x, double y)
        => _events.Add(new Arkanoid.Core.Net.EventDto { Type = type, X = x, Y = y });
    public List<Arkanoid.Core.Net.EventDto> DrainEvents()
    { var copy = new List<Arkanoid.Core.Net.EventDto>(_events); _events.Clear(); return copy; }

    public void CastFireball()  => SpellSystem.CastFireball(this);
    public void CastIgnite()    => SpellSystem.CastIgnite(this);
    public void CastFireWall()  => SpellSystem.CastFireWall(this);
    public void CastTurret()    => SpellSystem.CastTurret(this);

    public void DamagePlayer(int dmg) => CombatSystem.DamagePlayer(this, dmg);

    internal bool _igniteArmed = false;
    internal bool _decayArmed  = false;

    public void ApplyCheat(string op, double value) => CheatHandler.Apply(this, op, value);
}
