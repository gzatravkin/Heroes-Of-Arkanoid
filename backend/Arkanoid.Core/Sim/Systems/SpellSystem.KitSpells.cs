using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Kit-completion spell per-tick updates, on-event handlers, and the mod_cannons turret.
/// </summary>
internal static partial class SpellSystem
{
    internal static void UpdateKitSpells(GameInstance g, double dt)
    {
        // mod_cannons paddle mod: permanent slow side-cannon volleys (docs/04 §4.4).
        if (g.PaddleMods.Contains("mod_cannons"))
        {
            g._cannonAccumulator += dt;
            const double cannonInterval = 2.5; // PaddleModCannonInterval
            if (g._cannonAccumulator >= cannonInterval)
            {
                g._cannonAccumulator -= cannonInterval;
                var turretDef = g.GetSpellDef("turret");
                double speed  = turretDef?.Speed  ?? 460;
                int    dmg    = turretDef?.Damage  ?? 1;
                var py = g.Paddle.Center.Y - g.Paddle.Height / 2;
                foreach (var px in new[] { g.Paddle.Center.X - g.Paddle.Width / 2,
                                           g.Paddle.Center.X + g.Paddle.Width / 2 })
                {
                    g.Projectiles.Add(new Projectile
                    {
                        Id     = g._nextProjId++,
                        Pos    = new Arkanoid.Core.Math.Vec2(px, py),
                        Vel    = new Arkanoid.Core.Math.Vec2(0, -speed),
                        Damage = dmg,
                        Radius = g.Config.BallRadius * 0.6,
                        Kind   = "turret",
                    });
                }
                g.RaiseEvent(SimEventKind.TurretShot, g.Paddle.Center.X, py);
            }
        }

        // Last Day inter-bounce cooldown (separate from the effect duration in _effects).
        if (g._lastDayCooldown > 0) g._lastDayCooldown -= dt;

        // Overload Charge: detonate the planted charge when the 0.5 s timer expires.
        if (g._overloadChargeTimer > 0)
        {
            g._overloadChargeTimer -= dt;
            if (g._overloadChargeTimer <= 0)
            {
                g._overloadChargeTimer = 0;
                BlockDamage.ExplodeOverload(g, g._overloadChargeCol, g._overloadChargeRow, g._overloadRadius);
                g._overloadChargeCol = -1;
                g._overloadChargeRow = -1;
            }
        }
    }

    /// <summary>Last Day top-wall smite — called from BallSystem when a ball bounces off the ceiling.</summary>
    internal static void OnTopWallBounce(GameInstance g, Ball b)
    {
        if (!EffectSystem.HasEffect(g, "lastday") || g._lastDayCooldown > 0) return;
        var def = g.GetSpellDef("lastday");
        g._lastDayCooldown = def?.Cooldown ?? 0.5;
        int col = (int)System.Math.Clamp(
            (b.Pos.X - g.Config.BoardOriginX) / g.Config.CellSize, 0, g.Level.Grid.Cols - 1);
        int dmg = def?.Damage ?? 1;
        foreach (var blk in g.Blocks.Where(x => !x.Dead && !x.Boss && x.Col == col).ToList())
            BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, killMult: 0.5);
        var colX = g.Level.Grid.CellCenter(col, 0).X;
        g.RaiseEvent(SimEventKind.Judgement, colX, g.Level.Grid.Height);
    }

    /// <summary>Paladin Penetration: applied on the deflect after arming.</summary>
    internal static void ApplyPenetrationOnDeflect(GameInstance g, Ball b)
    {
        if (!g._penetrationArmed) return;
        g._penetrationArmed = false;
        var penDef = g.GetSpellDef("penetration");
        b.PhasesLeft += (penDef?.Hits ?? 3) + (g.SpellLevel("penetration") - 1) * (penDef?.HitsPerLevel ?? 0);
        g.RaiseEvent(SimEventKind.Penetration, b.Pos.X, b.Pos.Y);
    }

    /// <summary>Shared mana gate.</summary>
    internal static bool Spend(GameInstance g, double cost, string spell)
    {
        cost = g.AffinityCost(spell, cost); // economy rework §3: matching-element hero pays less
        if (g.ManaValue < cost)
        { g._log.Log(g.TickCount, "spell", $"{spell} denied", $"mana={g.ManaValue:F0} need={cost}"); return false; }
        g.ManaValue -= cost;
        return true;
    }
}
