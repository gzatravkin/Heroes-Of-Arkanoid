using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using Arkanoid.Core.Spells;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Data-driven spell dispatch: five archetype executors replace per-spell cast methods.
/// Adding a new spell requires only a new entry in spells.json — no C# changes.
/// </summary>
internal static partial class SpellSystem
{
    internal static void Cast(GameInstance g, SpellDef? def)
    {
        if (def == null || g.Phase != GamePhase.Playing) return;
        // Bespoke spells whose behaviour no generic archetype captures.
        if (def.Id == "phoenix") { PhoenixSystem.Cast(g, def); return; }
        // §3 rework: Fireball is now Conflagration — detonates the board's ignite stacks, not a projectile.
        if (def.Id == "fireball") { ConflagrationSystem.Cast(g, def); return; }
        // (Rocket reverted 2026-06-16 to the LEGACY piloted homing damage missile → falls through to the
        //  Projectile archetype with homing + AoE; no longer Concussion Charge.)
        // §3 NEW: Ashfall — a timed buff; ignite-kills rain vertical embers while active.
        if (def.Id == "ashfall") { AshfallSystem.Cast(g, def); return; }
        // §3 NEW: Reckoning — arm a meter that charges from HP lost and auto-smites the board.
        if (def.Id == "reckoning") { ReckoningSystem.Cast(g, def); return; }
        // §3 NEW: Tesla Grid — arm; side-wall bounces charge both walls → horizontal lightning curtain.
        if (def.Id == "tesla") { TeslaGridSystem.Cast(g, def); return; }
        // §3 rework: Skeletal Mage is now Lich's Gaze — a sweeping beam that curses blocks (no projectile).
        if (def.Id == "mage") { LichGazeSystem.Cast(g, def); return; }
        // Holy Echo: Paladin — spawns 1 temporary echo ball (50% damage, no imbues, 8s lifetime).
        if (def.Id == "holy_echo") { CastHolyEcho(g, def); return; }
        // Drain: Necromancer — timed bonus-mana-per-kill with a 40-mana total cap per cast.
        if (def.Id == "drain") { CastDrain(g, def); return; }
        // (Spear reverted 2026-06-16 to the LEGACY piercing damage projectile → falls through to the
        //  Projectile archetype with pierce; no longer the no-damage Lance of Dawn pillar.)
        // §3 rework: Skeleton is now Bonewalker — a minion that walks the rooftops (not a paddle turret).
        if (def.Id == "skeleton") { BonewalkerSystem.Cast(g, def); return; }
        // §3 fix: Bone Golem is a climbing bodyguard minion (not a fat piercing projectile).
        if (def.Id == "golem") { BoneGolemSystem.Cast(g, def); return; }
        switch (def.Archetype)
        {
            case SpellArchetype.Projectile: ExecuteProjectile(g, def); break;
            case SpellArchetype.Imbue:      ExecuteImbue(g, def);      break;
            case SpellArchetype.TimedAura:  ExecuteTimedAura(g, def);  break;
            case SpellArchetype.Placement:  ExecutePlacement(g, def);  break;
            case SpellArchetype.Instant:    ExecuteInstant(g, def);    break;
        }
    }

    // ── Projectile ─────────────────────────────────────────────────────────────

    private static void ExecuteProjectile(GameInstance g, SpellDef def)
    {
        if (!Spend(g, def.ManaCost, def.Id)) return;
        int count    = System.Math.Max(1, def.Count);
        int dmg      = def.Damage + (g.SpellLevel(def.Id) - 1) * def.DamagePerLevel;
        double half  = def.FanHalfAngleDeg * System.Math.PI / 180.0;
        bool fan     = def.FanHalfAngleDeg > 0 && count > 1;
        var origin   = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2);
        string kind  = def.Kind.Length > 0 ? def.Kind : def.Id;

        for (int i = 0; i < count; i++)
        {
            Vec2 vel;
            if (fan)
            {
                double t   = (double)i / (count - 1);
                double ang = -half + t * 2 * half;
                vel = new Vec2(System.Math.Sin(ang), -System.Math.Cos(ang)) * def.Speed;
            }
            else
            {
                vel = new Vec2(0, -def.Speed);
            }

            g.Projectiles.Add(new Projectile
            {
                Id               = g._nextProjId++,
                Pos              = origin,
                Vel              = vel,
                Damage           = dmg,
                Radius           = g.Config.BallRadius * System.Math.Max(def.RadiusMult, 0.1),
                Kind             = kind,
                Homing           = def.Homing,
                AoeRadius        = def.AoeRadius,
                AoeDamage        = def.AoeDamage,
                HomingStrength   = def.HomingStrength,
                MaxSpeed         = def.Homing ? def.Speed * 2 : 0,
                PiercingHitsLeft = def.Pierce,
            });
        }
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // ── Imbue ──────────────────────────────────────────────────────────────────

    private static void ExecuteImbue(GameInstance g, SpellDef def)
    {
        if (!Spend(g, def.ManaCost, def.Id)) return;
        switch (def.ImbueSlot)
        {
            case "ignite":      g._igniteArmed      = true; break;
            case "decay":       g._decayArmed        = true; break;
            case "penetration": g._penetrationArmed  = true; break;
        }
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // ── TimedAura ──────────────────────────────────────────────────────────────

    private static void ExecuteTimedAura(GameInstance g, SpellDef def)
    {
        if (!Spend(g, def.ManaCost, def.Id)) return;
        var duration = def.Duration + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel;
        EffectSystem.Add(g, def.Id, duration, def.TickInterval);
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    // ── Placement ──────────────────────────────────────────────────────────────

    private static void ExecutePlacement(GameInstance g, SpellDef def)
    {
        switch (def.PlacementKind)
        {
            case "firewall":
            {
                // Fire Wall (owner redesign 2026-07-01, replacing the arm-then-wait-for-a-block-hit
                // LEGACY behavior — indirect trigger + mechanically redundant with Ignite): a temporary
                // defensive line above the paddle. For its Lifetime, any descending ball crossing it
                // bounces back up AND is imbued with Ignite (sets up the next Conflagration for free);
                // any plain enemy hazard crossing it is destroyed outright.
                if (!Spend(g, def.ManaCost, def.Id)) return;
                double wallY = System.Math.Max(
                    g.Config.BoardOriginY + g.Config.CellSize,
                    g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Config.CellSize * 3);
                g.Barriers.Add(new Arkanoid.Core.Entities.Barrier
                {
                    Id                 = g._nextBarrierId++,
                    Y                  = wallY,
                    CenterX            = g.Paddle.Center.X,
                    Width              = g.Paddle.Width * def.WidthMult,
                    LifeRemaining      = def.Lifetime + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel,
                    Kind               = "firewall",
                    ReflectsBall       = true,
                    IgnitesBallOnCross = true,
                    DestroysHazards    = true,
                });
                g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, wallY);
                break;
            }

            case "barrier":
            {
                if (!Spend(g, def.ManaCost, def.Id)) return;
                var shieldY = g.Paddle.Center.Y - g.Paddle.Height / 2 - g.Config.BallRadius;
                g.Barriers.Add(new Arkanoid.Core.Entities.Barrier
                {
                    Id            = g._nextBarrierId++,
                    Y             = shieldY,
                    CenterX       = g.Paddle.Center.X,
                    Width         = g.Paddle.Width * def.WidthMult,
                    // Leveling extends how long the barrier guards the pit (docs/01 §61).
                    LifeRemaining = def.Lifetime + (g.SpellLevel(def.Id) - 1) * def.DurationPerLevel,
                    ReflectsHazardsAsBolts = true,
                });
                g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
                break;
            }

            case "zone":
            {
                if (!Spend(g, def.ManaCost, def.Id)) return;
                // Containment Field (§3 rework): its job is to SUPPRESS emitter enemies, so it auto-deploys
                // onto the nearest live emitter. With no emitter on the board it anchors above the paddle
                // to melt descending blocks (the HP-axis fallback).
                double zx = g.Paddle.Center.X, zy = g.Paddle.Center.Y - g.Paddle.Height;
                Block? target = null; double best = double.MaxValue;
                foreach (var b in g.Blocks)
                {
                    if (b.Dead || !b.Emitter) continue;
                    var bc = g.Level.Grid.CellCenter(b.Col, b.Row);
                    double d = (bc - g.Paddle.Center).LengthSquared;
                    if (d < best) { best = d; target = b; }
                }
                if (target != null) { var tc = g.Level.Grid.CellCenter(target.Col, target.Row); zx = tc.X; zy = tc.Y; }
                g.Zones.Add(new Zone
                {
                    Id             = g._nextZoneId++,
                    X              = zx,
                    Y              = zy,
                    Radius         = def.Radius,
                    LifeRemaining  = def.Lifetime,
                    DamagePerTick  = def.Damage + (g.SpellLevel(def.Id) - 1) * def.DamagePerLevel,
                    DamageInterval = def.DamageInterval,
                    Suppresses     = true, // Containment Field silences emitters caught inside it (§3)
                });
                g.RaiseEvent(SimEventKind.SpellCast, zx, zy);
                break;
            }

            case "bomb":
            {
                // Overload rework (tasks list.md §4): arm a "next ball-hit plants charge" mode.
                // The OLD fixed-row bomb placement is replaced: cast arms the flag; the charge is
                // planted when the ball next hits a block (BallSystem.ResolveBlocks) then detonates
                // after 0.5 s (SpellSystem.UpdateKitSpells), chaining to nearby blocks.
                if (!Spend(g, def.ManaCost, def.Id)) return;
                g._overloadArmed = true;
                g._overloadRadius = System.Math.Max(1,
                    (int)(def.AoeRadius + (g.SpellLevel(def.Id) - 1) * def.AoeRadiusPerLevel));
                g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
                break;
            }
        }
    }

    // ── Instant ────────────────────────────────────────────────────────────────

    private static void ExecuteInstant(GameInstance g, SpellDef def)
    {
        switch (def.Id)
        {
            // Necromancer signature (docs/04 §3): Raise summons friendly skeleton helper-ball(s) —
            // extra balls served from the paddle that bounce and break blocks alongside yours.
            case "raise":
            {
                if (!Spend(g, def.ManaCost, def.Id)) return;
                int count = System.Math.Max(1, def.ExtraCopies + (g.SpellLevel(def.Id) - 1) * def.ExtraCopiesPerLevel);
                double radius = g.Config.BallRadius * System.Math.Max(0.3, def.RadiusMult);
                for (int i = 0; i < count; i++)
                {
                    double lean = g.Rng.Range(-0.35, 0.35) + (i - (count - 1) / 2.0) * 0.18;
                    g.Balls.Add(new Ball
                    {
                        Id     = g._nextBallId++,
                        Radius = radius,
                        Pos    = new Vec2(g.Paddle.Center.X, g.Paddle.Center.Y - g.Paddle.Height / 2 - radius - 1),
                        Vel    = new Vec2(lean, -1).Normalized() * g.Config.BallSpeed,
                        Alive  = true,
                        Summoned = true,
                    });
                }
                g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
                break;
            }

            case "lightning":
            {
                var alive = g.Blocks.Where(b => !b.Dead).ToList();
                if (alive.Count == 0) return;
                if (!Spend(g, def.ManaCost, def.Id)) return;
                int dmg = def.Damage + (g.SpellLevel(def.Id) - 1) * def.DamagePerLevel;
                // Targeting rework (tasks list.md): strike the block nearest to the live ball.
                var liveBall = g.Balls.FirstOrDefault(b => b.Alive);
                Block current;
                if (liveBall != null)
                    current = alive.MinBy(b => (g.Level.Grid.CellCenter(b.Col, b.Row) - liveBall.Pos).LengthSquared)!;
                else
                    current = alive[g.Rng.Range(alive.Count)];
                var hit      = new HashSet<int> { current.Id };
                BlockDamage.DamageBlock(g, current, dmg, igniteSource: false, killMult: 0.5);
                var cPos = g.Level.Grid.CellCenter(current.Col, current.Row);
                g.RaiseEvent(SimEventKind.Lightning, cPos.X, cPos.Y);
                int chainJumps = def.ChainJumps + (Modifiers.HasConductor(g) ? 1 : 0);
                for (int jump = 0; jump < chainJumps; jump++)
                {
                    var candidates = alive
                        .Where(b => !b.Dead && !hit.Contains(b.Id))
                        .Select(b => new { blk = b, center = g.Level.Grid.CellCenter(b.Col, b.Row) })
                        .Where(x => (x.center - cPos).Length <= def.ChainRadius)
                        .ToList();
                    if (candidates.Count == 0) break;
                    int nextIdx = g.Rng.Range(candidates.Count);
                    var next    = candidates[nextIdx];
                    current = next.blk; cPos = next.center;
                    hit.Add(current.Id);
                    BlockDamage.DamageBlock(g, current, dmg, igniteSource: false, killMult: 0.25);
                    g.RaiseEvent(SimEventKind.Lightning, cPos.X, cPos.Y);
                }
                break;
            }

            case "duplicate":
            {
                var alive = g.Balls.Where(b => b.Alive).ToList();
                if (alive.Count == 0) return;
                if (!Spend(g, def.ManaCost, def.Id)) return;
                var src = alive[0];
                // Leveling clones more balls (docs/01 §61: "N smaller balls").
                int copies = System.Math.Max(1, def.ExtraCopies + (g.SpellLevel(def.Id) - 1) * def.ExtraCopiesPerLevel);
                for (int i = 0; i < copies; i++)
                {
                    double angleDeg = 15.0 * (i + 1);
                    double rad = angleDeg * System.Math.PI / 180.0;
                    double cos = System.Math.Cos(rad), sin = System.Math.Sin(rad);
                    var newVel = new Vec2(src.Vel.X * cos - src.Vel.Y * sin,
                                         src.Vel.X * sin + src.Vel.Y * cos);
                    // docs/01 §61: Duplication clones a ball into N *smaller* balls.
                    double cloneRadius = System.Math.Max(2.0, src.Radius * 0.8);
                    g.Balls.Add(new Ball
                    {
                        Id             = g._nextBallId++,
                        Radius         = cloneRadius,
                        Pos            = new Vec2(src.Pos.X + (i + 1) * (src.Radius + cloneRadius + 2), src.Pos.Y),
                        Vel            = newVel,
                        Alive          = true,
                        IgniteHitsLeft = src.IgniteHitsLeft,
                        DecayHitsLeft  = src.DecayHitsLeft,
                    });
                }
                g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
                break;
            }
        }
    }

    /// <summary>
    /// Holy Echo: Paladin unique — spawns one temporary echo ball. The echo deals 50% damage,
    /// carries no imbues, cannot spawn further echoes, and disappears after 8 seconds.
    /// </summary>
    private static void CastHolyEcho(GameInstance g, SpellDef def)
    {
        var alive = g.Balls.Where(b => b.Alive && !b.IsHolyEcho).ToList();
        if (alive.Count == 0) return;
        if (!Spend(g, def.ManaCost, def.Id)) return;
        var src = alive[0];
        double duration = def.Duration > 0 ? def.Duration : 8.0;
        double angleDeg = 20.0;
        double rad = angleDeg * System.Math.PI / 180.0;
        double cos = System.Math.Cos(rad), sin = System.Math.Sin(rad);
        var echoVel = new Vec2(src.Vel.X * cos - src.Vel.Y * sin,
                               src.Vel.X * sin + src.Vel.Y * cos);
        double echoRadius = System.Math.Max(2.0, src.Radius * 0.85);
        g.Balls.Add(new Ball
        {
            Id            = g._nextBallId++,
            Radius        = echoRadius,
            Pos           = new Vec2(src.Pos.X + (src.Radius + echoRadius + 2), src.Pos.Y),
            Vel           = echoVel,
            Alive         = true,
            IsHolyEcho    = true,
            HolyEchoTimer = duration,
            // No imbues — echoes do not carry fire/decay/penetration
            IgniteHitsLeft = 0,
            DecayHitsLeft  = 0,
        });
        g.RaiseEvent(SimEventKind.SpellCast, g.Paddle.Center.X, g.Paddle.Center.Y);
    }

    /// <summary>
    /// Drain: Necromancer — timed aura that gives 4 bonus mana per block kill, capped at 40 total per cast.
    /// Re-casting mid-duration resets the cap to 40.
    /// </summary>
    private static void CastDrain(GameInstance g, SpellDef def)
    {
        ExecuteTimedAura(g, def);
        if (g.SpellDrainActive)
            g._drainBonusLeft = 40.0;
    }
}
