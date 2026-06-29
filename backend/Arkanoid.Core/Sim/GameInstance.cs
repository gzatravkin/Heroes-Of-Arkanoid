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
    // Ordered simulation pipeline — each step receives (game, dt).
    private static readonly Action<GameInstance, double>[] _tickPipeline =
    {
        (g, dt) => SpellSystem.RegenMana(g, dt),
        (g, dt) => { for (int i = 0; i < g.Balls.Count; i++) BallSystem.UpdateBall(g, g.Balls[i], dt); },
        // Time-based ball acceleration (2026-06-16): raise each free ball to the level's ramped speed
        // (rising floor; card boosts above it survive). Slow-time clamps relative to this afterward.
        (g, _) => {
            double floor = g.RampedBallSpeed;
            foreach (var b in g.Balls)
            {
                if (!b.Alive || b.GrabberId != 0) continue;
                double sp = b.Vel.Length;
                if (sp > 1e-6 && sp < floor) b.Vel = b.Vel.Normalized() * floor;
            }
        },
        (g, dt) => BurnSystem.Update(g, dt),
        (g, dt) => SpellSystem.UpdateProjectiles(g, dt),
        (g, dt) => SpellSystem.UpdateFireWalls(g, dt),
        (g, dt) => PhoenixSystem.Update(g, dt),
        (g, dt) => EffectSystem.Update(g, dt),
        (g, dt) => SpellSystem.UpdateBarriers(g, dt),
        (g, dt) => SpellSystem.UpdateZones(g, dt),
        (g, dt) => SpellSystem.UpdateKitSpells(g, dt),
        (g, dt) => BossSystem.Update(g, dt),
        (g, dt) => BossSystem.UpdateVaseFuses(g, dt),
        (g, dt) => EmitterSystem.Update(g, dt),
        (g, dt) => StalactiteSystem.Update(g, dt),
        (g, dt) => ReviverSystem.Update(g, dt),
        (g, dt) => WindSystem.Update(g, dt),
        (g, dt) => ShieldSystem.Update(g, dt),
        (g, dt) => CauldronSystem.Update(g, dt),
        (g, dt) => LavaSystem.Update(g, dt),
        (g, dt) => ModuleSystem.Update(g, dt),   // §2 Pressure Cooker field descent
        (g, dt) => PacingSystem.Update(g, dt),
        (g, dt) => CombatSystem.UpdateHazards(g, dt),
        (g, dt) => BonusSystem.UpdateBonuses(g, dt),
        (g, _)  => WinLoseSystem.ResolveDrainAndWin(g),
    };

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        if (_awaitingRiftDraft) return; // frozen between rift floors, awaiting the §8 modifier pick
        // §2 Gyro Paddle: paddle horizontal velocity this tick (input was applied before Tick).
        if (double.IsNaN(_paddlePrevX)) _paddlePrevX = Paddle.Center.X;
        _paddleVelX = Paddle.Center.X - _paddlePrevX;
        _paddlePrevX = Paddle.Center.X;
        _blockGridDirty = true;
        TickCount++; ElapsedPlayTime += dt;
        if (_damageImmunity > 0) _damageImmunity = System.Math.Max(0, _damageImmunity - dt); // post-hit i-frames

        if (_log.Verbose)
            _log.Log(TickCount, "tick", "", $"balls={Balls.Count(b=>b.Alive)} mana={ManaValue:F0} blocks={Blocks.Count(b=>!b.Dead)}");
        foreach (var step in _tickPipeline) step(this, dt);
        PruneDeadBlocks();
    }

    // ── §8 mid-rift modifier draft (2026-06-16) ─────────────────────────────────────────────────
    /// <summary>A rift floor cleared and more remain — pause and offer a seeded 1-of-3 §8 draft.</summary>
    internal void BeginRiftDraft()
    {
        _awaitingRiftDraft = true;
        int seed = Rng.Range(int.MaxValue) ^ (_riftDraftCount++ * unchecked((int)0x9E3779B1));
        _riftDraftChoices.Clear();
        foreach (var m in Meta.RiftModifierService.Offer(seed, _riftModifiersTaken)) _riftDraftChoices.Add(m.Id);
        RaiseEvent(SimEventKind.FloorDown, 0, 0);
    }

    /// <summary>Apply the player's §8 pick LIVE to the running rift, then slide the next floor in.</summary>
    public void PickRiftModifier(string id)
    {
        if (!_awaitingRiftDraft) return;
        ApplyRiftModifierLive(id);
        _riftModifiersTaken.Add(id);
        _awaitingRiftDraft = false;
        _riftDraftChoices.Clear();
        AdvanceRiftFloor();
    }

    private void ApplyRiftModifierLive(string id)
    {
        int basePower = StatPower > 0 ? StatPower : Config.BallDamage;
        switch (id)
        {
            case "field_medic":   SetHp(StatMaxHp > 0 ? StatMaxHp : Hp); break;
            case "berserker":     StatPower = System.Math.Max(1, (int)System.Math.Round(basePower * 1.5));
                                  if (StatMaxHp > 1) { StatMaxHp -= 1; if (Hp > StatMaxHp) SetHp(StatMaxHp); } break;
            case "ironclad":      StatMaxHp += 2; SetHp(Hp + 2); break;
            case "keen_edge":     SetCrit(CritChance + 0.15, CritDamage); break;
            case "cruelty":       SetCrit(CritChance, CritDamage + 0.50); break;
            case "twin_serve":    SpareBalls += 1; break;
            case "prospector":    RiftRewardMult += 0.30; break;
            case "cursed_bounty": RiftRewardMult += 0.40; _riftExtraEmitters += 1; break;
            case "wide_gait":     Paddle.Width *= 1.25; break;
            case "snowball":      StatPower = System.Math.Max(1, (int)System.Math.Round(basePower * (1.0 + 0.05 * FloorIndex))); break;
        }
    }

    /// <summary>Slide the next rift floor in: relocate immortal leftovers to the sides, drop debris, add the
    /// next layout, and (Cursed Bounty) force extra emitters onto it.</summary>
    internal void AdvanceRiftFloor()
    {
        if (FloorIndex >= _extraFloors.Count) return;
        var next = _extraFloors[FloorIndex];
        FloorIndex++;
        Systems.WinLoseSystem.SlideLeftoversToSides(this);
        _blocks.AddRange(next);
        if (_riftExtraEmitters > 0)
        {
            int set = 0;
            foreach (var blk in _blocks)
            {
                if (set >= _riftExtraEmitters) break;
                if (blk.Dead || blk.Indestructible || blk.Boss || blk.Emitter) continue;
                blk.ForcedEmitter = true; set++;
            }
        }
        MarkBlocksDirty();
    }

    /// <summary>Caverns union-of-sticks: flood-fill 4-connected union blocks into shared groups so a
    /// single break collapses the whole connected bridge (see BlockDamage.CollapseUnion).</summary>
    private void AssignUnionGroups()
    {
        var unionAt = new System.Collections.Generic.Dictionary<(int, int), Entities.Block>();
        foreach (var b in _blocks) if (b.IsUnion) unionAt[(b.Col, b.Row)] = b;
        if (unionAt.Count == 0) return;

        int nextGroup = 1;
        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var b in _blocks)
        {
            if (!b.IsUnion || b.UnionGroup != 0) continue;
            int group = nextGroup++;
            var queue = new System.Collections.Generic.Queue<Entities.Block>();
            b.UnionGroup = group; queue.Enqueue(b);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var (dc, dr) in steps)
                    if (unionAt.TryGetValue((cur.Col + dc, cur.Row + dr), out var nb) && nb.UnionGroup == 0)
                    { nb.UnionGroup = group; queue.Enqueue(nb); }
            }
        }
    }

    private void PruneDeadBlocks()
    {
        if (Blocks.Any(b => !b.Dead && b.Reviver)) return;
        if (_reviveQueue.Count > 0) return;
        _blocks.RemoveAll(b => b.Dead); // BlockList auto-invalidates the grid + version
    }

    public GameInstance(LevelData level, SimConfig config, int seed, ISimLog? log = null,
        RelicCatalog? relics = null, BonusCatalog? bonuses = null,
        SpellCatalog? spells = null, Arkanoid.Core.Meta.CharacterCatalog? chars = null)
    {
        Level = level; Config = config; Rng = new Rng(seed); _log = log ?? NullSimLog.Instance;
        _relicCatalog = relics ?? RelicCatalog.Default;
        BonusCatalog  = bonuses;
        _spellCatalog = spells ?? SpellCatalog.Default;
        _charCatalog  = chars  ?? Arkanoid.Core.Meta.CharacterCatalog.Default;
        _blocks      = new BlockList(InvalidateBlockGrid);
        _blocks.AddRange(level.Blocks.Select(b => b.Clone()));
        AssignUnionGroups(); // Caverns union-of-sticks: group adjacent bridge blocks so they collapse together.
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
        _igniteArmed = false;
        _decayArmed  = false;
        _hellwalkerUsedThisServe = false;
        Phase = GamePhase.Serving;
    }

    /// <summary>Raise a friendly Summoned helper-ball above the paddle (Necromancer ★5 perk, §5.5;
    /// same identity as the Raise spell). Only while the level is in play, so it bounces immediately.</summary>
    internal void SpawnHelperBall()
    {
        if (Phase != GamePhase.Playing) return;
        double radius = Config.BallRadius;
        double lean = Rng.Range(-0.35, 0.35);
        Balls.Add(new Ball
        {
            Id     = _nextBallId++,
            Radius = radius,
            Pos    = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height / 2 - radius - 1),
            Vel    = new Vec2(lean, -1).Normalized() * Config.BallSpeed,
            Alive  = true,
            Summoned = true,
        });
        RaiseEvent(SimEventKind.SpellCast, Paddle.Center.X, Paddle.Center.Y);
    }

    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
        _log.Log(TickCount, "serve", "ball launched", $"lean={lean:F3} vx={Balls[0].Vel.X:F1} vy={Balls[0].Vel.Y:F1}");
        // Stat engine (§5.1 Multiball): launch extra balls alongside the first, fanned out.
        var src = Balls[0];
        for (int i = 0; i < StatMultiball; i++)
        {
            double rad = (12.0 * (i + 1)) * System.Math.PI / 180.0;
            double cos = System.Math.Cos(rad), sin = System.Math.Sin(rad);
            // Alternate the fan left/right so multiple extra balls spread both ways.
            if (i % 2 == 1) sin = -sin;
            Balls.Add(new Ball
            {
                Id     = _nextBallId++,
                Radius = Config.BallRadius,
                Pos    = new Vec2(src.Pos.X, src.Pos.Y),
                Vel    = new Vec2(src.Vel.X * cos - src.Vel.Y * sin, src.Vel.X * sin + src.Vel.Y * cos),
                Alive  = true,
            });
        }
        if (StatMultiball > 0)
            _log.Log(TickCount, "serve", "multiball", $"extra={StatMultiball} total={Balls.Count}");
        BallSystem.ApplyBallCoresOnServe(this, lean);
        // §2 Modules: per-ball serve setup (e.g. Hollow Ball's wide radius; Twin/Fission tagging). ToList so
        // Twin Soul's partner-spawn (in AfterServe) doesn't mutate the list mid-iteration.
        foreach (var ball in Balls.ToList()) if (ball.Alive) Systems.ModuleSystem.OnServe(this, ball);
        Systems.ModuleSystem.AfterServe(this);
    }

    public void SetPaddleX(double x)
    {
        var half = Paddle.Width / 2;
        var clamped = System.Math.Clamp(x, half, Level.Grid.Width - half);
        Paddle.Center = new Vec2(clamped, Paddle.Center.Y);
        if (Phase == GamePhase.Serving && Balls.Count > 0 && Balls[0].Alive)
            Balls[0].Pos = new Vec2(clamped, Paddle.Center.Y - Paddle.Height / 2 - Balls[0].Radius - 1);
    }

    public void AddRelic(string id)
    {
        ActiveRelics.Add(id);
        _log.Log(TickCount, "relic", "added", id);
        _relicCatalog.TryGet(id, out var def);
        switch (def?.Effect)
        {
            case "cost_hp":    Hp = System.Math.Max(1, Hp - 1); break;
            case "mana_max":   ManaMaxValue += def!.Magnitude; break;
            case "width_mult": Paddle.Width *= def!.Magnitude; break;
        }
    }

    public void SetSpellLevels(IDictionary<string, int> levels)
    {
        foreach (var (k, v) in levels) SpellLevels[k] = v;
    }

    public SpellDef? GetSpellDef(string id)
    {
        _spellCatalog.TryGet(id, out var def);
        return def;
    }

    public void CastSlot(int slot)
    {
        if (slot < 0) return;
        // Equipped loadout drives the hotbar (docs/04 §3): slot 0 = signature, 1..N = drafted.
        if (Loadout.Count > 0)
        {
            if (slot >= Loadout.Count) return;
            SpellSystem.Cast(this, GetSpellDef(Loadout[slot]));
            return;
        }
        // Fallback: no loadout equipped → the character's full kit (sim-test back-compat).
        if (!_charCatalog.TryGet(Character, out var charDef)) return;
        if (slot >= charDef.Spells.Count) return;
        SpellSystem.Cast(this, GetSpellDef(charDef.Spells[slot].Id));
    }

    public void RaiseEvent(SimEventKind kind, double x, double y, int payload = 0)
        => _events.Add(new SimEvent(kind, x, y, payload));
    public List<SimEvent> DrainEvents()
    { var copy = new List<SimEvent>(_events); _events.Clear(); return copy; }

    public void DamagePlayer(int dmg) => CombatSystem.DamagePlayer(this, dmg);
    public void ApplyCheat(string op, double value) => CheatHandler.Apply(this, op, value);
}
