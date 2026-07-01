using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Per-ball update: position integration, wall resolve, teleporter warp,
/// ghost-phase skip, paddle deflect, block collision + reflection, and
/// teleport-cooldown decrement.
/// </summary>
internal static class BallSystem
{
    internal static void UpdateBall(GameInstance g, Ball b, double dt)
    {
        if (!b.Alive) return;

        // CCD guard: if the ball would travel more than half a cell per tick, split into
        // 2 sub-steps so it cannot tunnel through a block between samples.
        var halfCell = g.Config.CellSize * 0.5;
        if (b.Vel.Length * dt > halfCell)
        {
            UpdateBallStep(g, b, dt / 2);
            if (b.Alive) UpdateBallStep(g, b, dt / 2);
            return;
        }
        UpdateBallStep(g, b, dt);
    }

    private static void UpdateBallStep(GameInstance g, Ball b, double dt)
    {
        // Decrement teleport cooldown once per tick (min 0)
        if (b.TeleportCooldown > 0) b.TeleportCooldown--;

        // Holy Echo lifetime: tick the timer and expire the ball when it runs out.
        if (b.IsHolyEcho)
        {
            b.HolyEchoTimer -= dt;
            if (b.HolyEchoTimer <= 0) { b.Alive = false; return; }
        }

        // §1 Cards (Phase Window / Hot Hand / Redline): per-tick ball-state updates before collisions.
        CardSystem.OnBallTick(g, b, dt);
        // §2 Modules (Tidal swift speed, …): per-tick ball-state updates.
        ModuleSystem.OnBallTick(g, b, dt);

        // Carried by a Witchland bat toward the drain (docs/11 redesign): the ball rides
        // the carrier hazard; CombatSystem owns the rescue/escape outcomes.
        if (b.GrabberId > 0)
        {
            var carrier = g.Hazards.FirstOrDefault(h => h.Alive && h.Id == b.GrabberId
                && (h.Behavior == HazardBehavior.Bat || h.Behavior == HazardBehavior.WitchGrab));
            if (carrier != null)
            {
                b.Pos = carrier.Pos;
                b.Vel = new Vec2(0, 0);
                return; // no own movement/collision while carried
            }
            b.GrabberId = 0; // carrier gone — CombatSystem already set our release velocity
        }

        b.Pos += b.Vel * dt;
        if (g._log.Verbose)
            g._log.Log(g.TickCount, "ball", "move", $"id={b.Id} x={b.Pos.X:F1} y={b.Pos.Y:F1}");

        // Wall contacts this tick (read before ResolveWalls flips the velocity).
        var width   = g.Level.Grid.Width;
        var hitTop  = b.Pos.Y - b.Radius < 0 && b.Vel.Y < 0;
        var hitLeft  = b.Pos.X - b.Radius < 0     && b.Vel.X < 0;
        var hitRight = b.Pos.X + b.Radius > width && b.Vel.X > 0;
        Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, width, g.Config);
        if (hitTop)
            SpellSystem.OnTopWallBounce(g, b); // Paladin Last Day column smite
        if (hitLeft)  TeslaGridSystem.OnWallBounce(g, b, left: true);   // §3 Tesla Grid charges a wall
        if (hitRight) TeslaGridSystem.OnWallBounce(g, b, left: false);
        if (hitLeft || hitRight) CardSystem.OnWallBounce(g, b);         // §1 Bank Shot banks a carom

        if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, g.Paddle, g.Config, out var t))
        {
            SpellSystem.OnPaddleHit(g, b, t);
            // Combo resets on every paddle contact (streak broken).
            g.Combo.Count = 0;
            g.Combo.Multiplier = 1;
        }

        // (Paladin Shield reverted 2026-06-16 to the LEGACY bullet-reflect barrier — it no longer pit-saves
        //  the ball; the reflect logic lives in SpellSystem.UpdateBarriers, acting on enemy hazards.)
        LanceSystem.Resolve(g, b); // §3 Lance of Dawn: bank off temporary pillars
        ResolveBlocks(g, b);
    }

    /// <summary>Altar: ally every Heaven statue — they fight FOR the player while the timer holds.</summary>
    internal static void PacifyStatues(GameInstance g)
    {
        foreach (var s in g.Blocks)
            if (!s.Dead && s.IsStatue) s.AllyTimer = g.Config.Enemies.AltarAllyDuration;
    }

    /// <summary>Vase: permanently level every statue up — faster fire, but bigger kill rewards.</summary>
    internal static void LevelUpStatues(GameInstance g)
    {
        foreach (var s in g.Blocks)
            if (!s.Dead && s.IsStatue) s.StatueLevel++;
        g.RaiseEvent(SimEventKind.VaseLevelUp, 0, 0);
    }

    // 3×3 neighborhood in row-ascending order (row -1 → 0 → +1, left to right within each row).
    // Row-ascending order matches the original g.Blocks list order, preserving tie-breaking behavior
    // when the ball overlaps two adjacent blocks simultaneously.
    private static readonly (int dc, int dr)[] _neighbourOffsets =
        { (-1,-1),(0,-1),(1,-1), (-1,0),(0,0),(1,0), (-1,1),(0,1),(1,1) };

    private static void ResolveBlocks(GameInstance g, Ball b)
    {
        var cell      = g.Config.CellSize;
        int centerCol = (int)System.Math.Floor((b.Pos.X - g.Level.Grid.OriginX) / cell);
        int centerRow = (int)System.Math.Floor((b.Pos.Y - g.Level.Grid.OriginY) / cell);
        foreach (var (dc, dr) in _neighbourOffsets)
        {
            var blk = g.BlockAt(centerCol + dc, centerRow + dr);
            if (blk == null) continue;
            var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            var box = Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(b.Pos, b.Radius)) continue;

            // Teleporter: warp ball to next teleporter in cycle (Hell signature mechanic)
            if (blk.Teleporter && b.TeleportCooldown == 0)
            {
                var teleporters = g.Blocks.Where(t => !t.Dead && t.Teleporter && t.TeleportColor == blk.TeleportColor).ToList();
                if (teleporters.Count >= 2)
                {
                    int idx  = teleporters.IndexOf(blk);
                    var dest = teleporters[(idx + 1) % teleporters.Count];
                    var destCenter = g.Level.Grid.CellCenter(dest.Col, dest.Row);
                    // nudge one ball-radius along current velocity so ball exits cleanly
                    var nudge = b.Vel.Length > 0 ? b.Vel.Normalized() * b.Radius : new Vec2(0, -b.Radius);
                    b.Pos = destCenter + nudge;
                    b.TeleportCooldown = g.Config.Enemies.TeleportCooldownTicks;
                    g.RaiseEvent(SimEventKind.Teleport, destCenter.X, destCenter.Y);
                    return; // do not also reflect
                }
                // single teleporter: fall through to indestructible bounce below
            }

            // Ghost portal (Witchland): toggle the ball's phase and pass through the portal.
            if (blk.Portal)
            {
                if (b.TeleportCooldown == 0)
                {
                    b.Ghost = !b.Ghost;
                    b.TeleportCooldown = g.Config.Enemies.TeleportCooldownTicks;
                    g.RaiseEvent(SimEventKind.GhostPortal, c.X, c.Y);
                }
                continue; // always pass through the portal block itself
            }

            // Bat: snatches the ball and CARRIES it toward the drain. Counterplay: pop
            // the carrier with a second ball or any spell projectile (docs/11 redesign).
            if (blk.Bat)
            {
                blk.Dead = true; // the block becomes the moving carrier
                var carrier = new Projectile
                {
                    Id       = g._nextHazardId++,
                    Pos      = c,
                    // LEGACY bat (reverted 2026-06-16): it HOVERS (gentle upward drift) while holding the
                    // ball for BatHoldTime, then releases + rewards — it does NOT drag the ball to the drain.
                    Vel      = new Vec2(0, -8),
                    Damage   = 0,
                    Radius   = g.Config.Enemies.HazardRadius,
                    Alive    = true,
                    Kind     = "bat",
                    Behavior = HazardBehavior.Bat,
                    CarriedBallId = b.Id,
                    StateTimer = g.Config.Enemies.BatHoldTime,
                };
                g.Hazards.Add(carrier);
                b.GrabberId = carrier.Id;
                b.Vel       = new Vec2(0, 0);
                g.RaiseEvent(SimEventKind.BatGrab, c.X, c.Y);
                return; // ball is now held
            }

            // Lava: the ball passes THROUGH and DESTROYS the lava cell it flies over (2026-06-15) — active
            // counterplay to the lava drain. (Lava is otherwise indestructible; only the ball clears it.)
            if (blk.Lava)
            {
                blk.Dead = true;
                var lc = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                g.RaiseEvent(SimEventKind.LavaRetract, lc.X, lc.Y);
                g.InvalidateBlockGrid();
                continue;
            }

            // Altar: hitting it pacifies the Heaven statues; then bounce off as a solid block.
            if (blk.Altar)
            {
                PacifyStatues(g);
                g.RaiseEvent(SimEventKind.Altar, c.X, c.Y);
                // fall through to normal reflection
            }

            // Phase interaction: a NORMAL ball passes through ghost (ballPhases) blocks; a GHOST
            // ball instead passes through normal destructible blocks and collides with ghost ones.
            bool ghostBlock = blk.BallPhases;
            if (b.Ghost)
            {
                if (!ghostBlock && !blk.Indestructible && !blk.Boss && !blk.Teleporter) continue;
            }
            else
            {
                if (ghostBlock) continue;
            }

            bool ignited = b.IgniteHitsLeft > 0;
            bool decayed = b.DecayHitsLeft  > 0;
            var dmg = Modifiers.BallDamage(g, blk, ignited, b.Ghost);
            // §1 Cards: position/state-gated ball-damage bonuses (Headhunter, Underdog, Cleanup Crew, …).
            dmg += CardSystem.BallDamageBonus(g, b, blk);
            // §2 Modules: ball-damage modifiers (Tidal heavy mode, Hollow Ball penalty, …).
            dmg += ModuleSystem.BallDamageBonus(g, b, blk);
            if (dmg < 1) dmg = 1; // a hit always does at least 1 (Hollow Ball can't drop it below)
            // Holy Echo: echo balls deal 50% of normal damage (minimum 1).
            if (b.IsHolyEcho) dmg = System.Math.Max(1, (int)System.Math.Round(dmg * 0.5));
            // Echo core: the first block hit after each paddle deflect strikes harder.
            if (b.EchoArmed) { dmg += 1; b.EchoArmed = false; } // EchoBonus = 1
            // Crit (stat engine, design §5.7): a % chance to multiply this hit's damage. Applies to any
            // real killable target — including bosses (§5.9: "bosses 500+ HP, so big crits pop"); only
            // indestructible blocks are exempt. Raises a Crit event for renderer juice + read by crit cards.
            g.LastHitWasCrit = false;
            if (g.CritChance > 0 && blk.NeedToKill && !blk.Indestructible
                && g.Rng.NextDouble() < g.CritChance)
            {
                int baseDmg = dmg;
                double critMult = g.CritDamage;
                // §5.5 Paladin ★5: below 50% HP, +25% crit damage.
                if (g.HasPerk(Meta.StatResolver.PalLowHpCritDmg) && g.StatMaxHp > 0 && g.Hp * 2 <= g.StatMaxHp)
                    critMult *= 1.25;
                // §5.5 Fire Mage ★3: an already-burning block takes +15% from crits.
                if (g.HasPerk(Meta.StatResolver.FmIgnitedCrit) && blk.BurnRemaining > 0)
                    critMult *= 1.15;
                dmg = (int)System.Math.Round(dmg * critMult);
                // §1 Executioner's Edge: a crit on an already-low block executes for double.
                dmg += CardSystem.ExecutionerExtra(g, blk, dmg);
                g.LastHitWasCrit = true;
                g.RaiseEvent(SimEventKind.Crit, c.X, c.Y, dmg);
                g._log.Log(g.TickCount, "crit", "ball crit", $"block={blk.Id} base={baseDmg} crit={dmg} mult=x{critMult:0.00}");
                // §5.5 Necromancer ★3: crits drain mana to you.
                if (g.HasPerk(Meta.StatResolver.NecroCritDrain))
                    g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + 4);
            }
            // Frost core: hitting an emitter/statue freezes its cadence.
            if (g.BallCores.Contains("frost") && (blk.Emitter || blk.ShieldStatue))
            {
                var freeze = 2.0 // FrostFreezeSeconds
                    * (g.HasFusion("echo", "frost") ? 2.0 : 1.0); // StasisFreezeMult = 2
                blk.EmitAccumulator = -freeze;
                g.RaiseEvent(SimEventKind.Frost, c.X, c.Y);
            }

            // Ghost core: spend a phase charge to punch THROUGH the block (damage, no bounce).
            var phased = b.PhasesLeft > 0 && !blk.Indestructible && !blk.Boss;
            if (phased)
            {
                b.PhasesLeft--;
                g._log.Log(g.TickCount, "ballcore", "phase-through", $"ball={b.Id} block={blk.Id}");
            }
            else
            {
                // reflect by dominant penetration axis
                var dx = b.Pos.X - c.X;
                var dy = b.Pos.Y - c.Y;
                if (System.Math.Abs(dx) / (cell / 2) > System.Math.Abs(dy) / (cell / 2))
                    b.Vel = new Vec2(System.Math.Sign(dx) * System.Math.Abs(b.Vel.X), b.Vel.Y);
                else
                    b.Vel = new Vec2(b.Vel.X, System.Math.Sign(dy) * System.Math.Abs(b.Vel.Y));
                // Enforce minimum 20° angle from horizontal (prevents unplayable flat shots).
                b.Vel = Arkanoid.Core.Physics.BallPhysics.EnforceMinAngle(b.Vel);
            }

            // Ignite redesign (2026-06-16): an ignite-imbued hit LIGHTS the block (slow burn) instead of
            // dealing direct damage — so even 1-HP blocks smoulder down over time rather than popping
            // instantly. Walls/bosses ignore ignite and take a normal hit.
            if (ignited && blk.NeedToKill && !blk.Indestructible && !blk.Boss)
            {
                BurnSystem.LightBlock(g, blk, 0); // gen-0 seed; BurnSystem creeps the chain + ticks the DoT
                b.IgniteHitsLeft--;
            }
            else
            {
                int hpBefore = blk.Hp;
                BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, decaySource: decayed);
                CardSystem.OnBlockHit(g, b, blk, dmg, hpBefore); // §1 Overkill spillover + Erosion (indestructible)
                ModuleSystem.OnBlockHit(g, b, blk, dmg, hpBefore); // §2 Brittle Glass shatter on indestructible
                if (decayed) b.DecayHitsLeft--;
            }
            // Overload Charge: cast arms the flag; the next ball-block hit on a real block plants a
            // 0.5 s charge that detonates (chain-explode neighbors) via SpellSystem.UpdateKitSpells.
            if (g._overloadArmed && !b.IsHolyEcho && blk.NeedToKill && !blk.Indestructible && !blk.Boss)
            {
                g._overloadArmed = false;
                g._overloadChargeTimer = 0.5;
                g._overloadChargeCol = blk.Col;
                g._overloadChargeRow = blk.Row;
                g.RaiseEvent(SimEventKind.SpellCast, c.X, c.Y);
            }
            break; // one block per tick keeps it deterministic
        }
    }

    // Apply ball-core on-serve effects: ghost phase-through, ember ignite, split extra ball.
    internal static void ApplyBallCoresOnServe(GameInstance g, double lean)
    {
        if (g.BallCores.Contains("ghost"))
        {
            g.Balls[0].PhasesLeft = g.HasFusion("ghost", "split") ? 2 : 1;
            g._log.Log(g.TickCount, "ballcore", "ghost charges", $"phases={g.Balls[0].PhasesLeft}");
        }
        if (g.BallCores.Contains("ember"))
        {
            foreach (var b in g.Balls)
                b.IgniteHitsLeft = System.Math.Max(b.IgniteHitsLeft, 2);
            g._log.Log(g.TickCount, "ballcore", "ember ignite", "hitsLeft=2");
        }
        if (g.BallCores.Contains("split"))
        {
            var main = g.Balls[0];
            var extraLean = lean + 0.15;
            var extraBall = new Arkanoid.Core.Entities.Ball
            {
                Id     = g._nextBallId++,
                Radius = g.Config.BallRadius,
                Pos    = new Arkanoid.Core.Math.Vec2(main.Pos.X + g.Config.BallRadius * 2 + 2, main.Pos.Y),
                Vel    = new Arkanoid.Core.Math.Vec2(extraLean, -1).Normalized() * g.Config.BallSpeed,
                Alive  = true,
            };
            if (g.BallCores.Contains("ember"))
                extraBall.IgniteHitsLeft = System.Math.Max(extraBall.IgniteHitsLeft, 2);
            g.Balls.Add(extraBall);
            g._log.Log(g.TickCount, "ballcore", "split extra ball", $"id={extraBall.Id} lean={extraLean:F3}");
        }
    }
}
