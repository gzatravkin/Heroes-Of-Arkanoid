using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Spell casting and per-tick spell updates. Split across partial files by class:
///   SpellSystem.cs           — mana regen, paddle-deflect imbue, shared projectile update.
///   SpellSystem.FireMage.cs  — Fireball / Ignite / FireWall / Turret.
///   SpellSystem.ClassSpells.cs — Paladin / Engineer / Necromancer spells.
/// </summary>
internal static partial class SpellSystem
{
    // -----------------------------------------------------------------------
    // Mana regen
    // -----------------------------------------------------------------------

    internal static void RegenMana(GameInstance g, double dt)
    {
        if (g._manaRegenFrozen) return; // freezeMana cheat (deterministic HUD tests)
        // §1 Channeling: regen PAUSES while the ball is in flight, DOUBLES while it's cradled near the paddle.
        double channel = CardSystem.ChannelingRegenMult(g);
        g.ManaValue = System.Math.Min(g.ManaMaxValue,
            g.ManaValue + g.Config.ManaRegenPerSec * Modifiers.ManaRegenMult(g) * channel * dt);
    }

    // -----------------------------------------------------------------------
    // Paddle hit extras (perfect-deflect bonus + ignite imbue)
    // -----------------------------------------------------------------------

    internal static void OnPaddleHit(GameInstance g, Entities.Ball b, double t)
    {
        g.RaiseEvent(SimEventKind.Deflect, b.Pos.X, b.Pos.Y); // audio cue (G1)
        if (System.Math.Abs(t) < g.Config.PerfectDeflectBand)
        {
            var bonus = g.Config.ManaPerfectDeflectBonus
                + (Modifiers.HasOvercharge(g) ? Modifiers.OverchargeBonus(g) : 0);
            g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + bonus);
            g.RaiseEvent(SimEventKind.PerfectDeflect, b.Pos.X, b.Pos.Y); // skill-reward juice cue
            g._log.Log(g.TickCount, "mana", "perfect deflect bonus", $"mana={g.ManaValue:F0} bonus={bonus:F0}");
        }
        // §1 Cards (Dead Center / Metronome): a deflect — perfect or not — drives the rhythm cards.
        CardSystem.OnPaddleHit(g, b, System.Math.Abs(t) < g.Config.PerfectDeflectBand);
        // §2 Modules (Tidal toggle / Gyro angle / Drumhead shockwave): a deflect drives the paddle/core modules.
        ModuleSystem.OnPaddleHit(g, b, t, System.Math.Abs(t) < g.Config.PerfectDeflectBand);
        // Echo core: arm the bonus-damage strike for the next block hit.
        if (g.BallCores.Contains("echo")) b.EchoArmed = true;
        // Fire Mage Turret: paddle-mounted — fires a bolt on each catch while active (NOT on a timer).
        if (EffectSystem.HasEffect(g, "turret")) FireTurretBolt(g);
        // Paladin Penetration: armed cast lands on this deflect.
        ApplyPenetrationOnDeflect(g, b);
        ApplyIgniteOnDeflect(g, b);
        ApplyDecayOnDeflect(g, b);
    }

    /// <summary>Fire the turret on a paddle deflect: a small upward TWO-bolt spread (balance 2026-06-16 —
    /// was a single straight bolt; the fan lets one catch chip two blocks so "fires on every catch" matters).</summary>
    internal static void FireTurretBolt(GameInstance g)
    {
        var def     = g.GetSpellDef("turret");
        double speed = def?.Speed ?? 460;
        double px    = g.Paddle.Center.X;
        double py    = g.Paddle.Center.Y - g.Paddle.Height / 2;
        // Two bolts fanned ±9° from vertical.
        foreach (double angDeg in new[] { -9.0, 9.0 })
        {
            double rad = angDeg * System.Math.PI / 180.0;
            g.Projectiles.Add(new Entities.Projectile {
                Id     = g._nextProjId++,
                Pos    = new Vec2(px, py),
                Vel    = new Vec2(System.Math.Sin(rad) * speed, -System.Math.Cos(rad) * speed),
                Damage = def?.Damage ?? 1,
                Radius = g.Config.BallRadius * (def?.RadiusMult ?? 0.6),
                Kind   = "turret"
            });
        }
        g.RaiseEvent(SimEventKind.TurretShot, px, py);
    }

    internal static void ApplyIgniteOnDeflect(GameInstance g, Entities.Ball b)
    {
        if (!g._igniteArmed) return;
        b.IgniteHitsLeft = Modifiers.IgniteHits(g);
        g._igniteArmed = false;
        g.RaiseEvent(SimEventKind.Ignite, b.Pos.X, b.Pos.Y);
    }

    internal static void ApplyDecayOnDeflect(GameInstance g, Entities.Ball b)
    {
        if (!g._decayArmed) return;
        var decayDef = g.GetSpellDef("decay");
        b.DecayHitsLeft = (decayDef?.Hits ?? 4) + (g.SpellLevel("decay") - 1) * (decayDef?.HitsPerLevel ?? 0);
        g._decayArmed = false;
        g.RaiseEvent(SimEventKind.Decay, b.Pos.X, b.Pos.Y);
    }

    // -----------------------------------------------------------------------
    // Shared projectile update (piercing + homing + AoE)
    // -----------------------------------------------------------------------

    internal static void UpdateProjectiles(GameInstance g, double dt)
    {
        AshfallSystem.Update(g, dt);    // §3 Ashfall: tick the ignite-kill ember buff
        TeslaGridSystem.Update(g, dt);  // §3 Tesla Grid: tick the curtain cooldown
        LichGazeSystem.Update(g, dt);   // §3 Lich's Gaze: sweep the beam + curse blocks
        LanceSystem.Update(g, dt);      // §3 Lance of Dawn: tick pillar lifetimes
        BonewalkerSystem.Update(g, dt); // §3 Bonewalker: stride the rooftops + melee underfoot
        BoneGolemSystem.Update(g, dt);  // §3 Bone Golem: climb the column, bulldoze + soak hazards
        var cell = g.Config.CellSize;
        foreach (var pr in g.Projectiles)
        {
            if (!pr.Alive) continue;

            // Homing update (rocket steers toward nearest block)
            if (pr.Homing) UpdateRocketHoming(pr, g, dt);

            pr.Pos += pr.Vel * dt;
            if (pr.Pos.Y < -cell || pr.Pos.Y > g.Level.Grid.Height + cell * 3) { pr.Alive = false; continue; }

            foreach (var blk in g.Blocks)
            {
                if (blk.Dead) continue;
                // Ally bolts (allied statues) never hit fellow statues or walls — they
                // exist to clear the field for the player, not to eat themselves.
                if (pr.Kind == "allybolt" && (blk.IsStatue || blk.Indestructible)) continue;
                var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
                if (!box.IntersectsCircle(pr.Pos, pr.Radius)) continue;

                BlockDamage.DamageBlock(g, blk, pr.Damage, igniteSource: false, killMult: 0.5);

                // AoE explosion (rocket)
                if (pr.AoeRadius > 0)
                {
                    int aoeDmg = pr.AoeDamage > 0 ? pr.AoeDamage : pr.Damage;
                    foreach (var nb in g.Blocks)
                    {
                        if (nb.Dead || nb == blk) continue;
                        var nc = g.Level.Grid.CellCenter(nb.Col, nb.Row);
                        if ((nc - pr.Pos).Length <= pr.AoeRadius)
                        {
                            BlockDamage.DamageBlock(g, nb, aoeDmg, igniteSource: false, killMult: 0.25);
                            g.RaiseEvent(SimEventKind.Explosion, nc.X, nc.Y);
                        }
                    }
                    g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
                    pr.Alive = false;
                    break;
                }

                // Piercing (spear)
                if (pr.PiercingHitsLeft > 0)
                {
                    pr.PiercingHitsLeft--;
                    if (pr.PiercingHitsLeft <= 0) pr.Alive = false;
                    break; // one block per tick even for piercing (deterministic)
                }

                pr.Alive = false;
                break;
            }
        }
        g.Projectiles.RemoveAll(p => !p.Alive);
    }

    internal static void UpdateRocketHoming(Projectile pr, GameInstance g, double dt)
    {
        if (!pr.Homing) return;
        var target = PickRocketTarget(g, pr);
        if (target is null) return;
        var dir = (g.Level.Grid.CellCenter(target.Col, target.Row) - pr.Pos);
        if (dir.Length > 0)
        {
            double strength = pr.HomingStrength > 0 ? pr.HomingStrength : 400;
            var steer = dir.Normalized() * strength * dt;
            pr.Vel += steer;
            // Clamp to max speed so the rocket doesn't overshoot wildly
            if (pr.MaxSpeed > 0 && pr.Vel.Length > pr.MaxSpeed)
                pr.Vel = pr.Vel.Normalized() * pr.MaxSpeed;
        }
    }

    /// <summary>
    /// Rocket priority targeting (tasks list.md): boss weak point > active emitter > elite/armored
    /// > highest HP > nearest block. Gives Rocket a distinct anti-priority role vs. Lightning.
    /// </summary>
    private static Block? PickRocketTarget(GameInstance g, Projectile pr)
    {
        var alive = g.Blocks.Where(b => !b.Dead).ToList();
        if (alive.Count == 0) return null;
        // 1. Boss
        var boss = alive.FirstOrDefault(b => b.Boss);
        if (boss != null) return boss;
        // 2. Active enemy emitter
        var emitter = alive.FirstOrDefault(b => b.Emitter);
        if (emitter != null) return emitter;
        // 3. Elite or shield-immune (armored)
        var armored = alive.FirstOrDefault(b => b.Elite || b.ImmunityTimer > 0);
        if (armored != null) return armored;
        // 4. Highest HP
        int maxHp = alive.Max(b => b.Hp);
        var highHp = alive.Where(b => b.Hp == maxHp).ToList();
        if (highHp.Count == 1) return highHp[0];
        // 5. Nearest (tiebreak or pure fallback)
        return highHp.MinBy(b => (g.Level.Grid.CellCenter(b.Col, b.Row) - pr.Pos).LengthSquared);
    }
}
