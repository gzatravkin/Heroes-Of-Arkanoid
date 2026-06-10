using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Hazard movement + paddle collision (UpdateHazards) and direct player HP damage (DamagePlayer).
/// Boss hazard spawning has moved to BossSystem.
/// </summary>
internal static class CombatSystem
{
    // -----------------------------------------------------------------------
    // Hazards
    // -----------------------------------------------------------------------

    internal static void UpdateHazards(GameInstance g, double dt)
    {
        var drainLine = g.Level.Grid.Height + g.Config.CellSize * 2;
        var paddleBox = Aabb.FromCenter(g.Paddle.Center, g.Paddle.Width / 2, g.Paddle.Height / 2);

        // Index loop: UpdateBatCarrier may append the flyaway hazard mid-iteration.
        for (int i = 0; i < g.Hazards.Count; i++)
        {
            var hz = g.Hazards[i];
            if (!hz.Alive) continue;
            hz.Pos += hz.Vel * dt;

            // Bat carrier dragging a ball to the drain: poppable by a second ball or
            // any spell projectile; reaching the drain costs the ball (docs/11).
            if (hz.Kind == "bat" && hz.CarriedBallId > 0)
            {
                UpdateBatCarrier(g, hz, drainLine);
                continue;
            }

            // Witch grab-hand: homes on a ball, carries it to the boss, holds, then
            // hurls it at the paddle. Poppable like the bat carrier (docs/11).
            if (hz.Kind == "witchgrab")
            {
                UpdateWitchGrab(g, hz, drainLine, dt);
                continue;
            }

            // Harmless visual hazards (bat flyaway) just drift; despawn above the board.
            if (hz.Damage <= 0)
            {
                if (hz.Pos.Y + hz.Radius < g.Config.BoardOriginY - g.Config.CellSize)
                    hz.Alive = false;
                continue;
            }

            // Falling stalactites smash through blocks (one hit per block) — hanging
            // them over enemy nests makes them a weapon you trigger (docs/11).
            if (hz.Kind == "stalactite")
                StalactitePierceBlocks(g, hz);

            if (paddleBox.IntersectsCircle(hz.Pos, hz.Radius))
            {
                hz.Alive = false;
                DamagePlayer(g, hz.Damage);
                g._log.Log(g.TickCount, "hazard", "hit paddle", $"damage={hz.Damage}");
                // playerHit event already raised inside DamagePlayer
                continue;
            }

            // Carts roll horizontally and despawn once they leave the board (not by falling).
            if (hz.Kind == "cart")
            {
                if (hz.Pos.X < -hz.Radius || hz.Pos.X > g.Level.Grid.Width + hz.Radius)
                    hz.Alive = false;
                continue;
            }

            if (hz.Pos.Y - hz.Radius > drainLine)
            {
                hz.Alive = false; // dodged / missed
                g._log.Log(g.TickCount, "hazard", "missed paddle", "");
            }
        }
        g.Hazards.RemoveAll(h => !h.Alive);
    }

    /// <summary>One tick of a bat carrier: rescue checks (second ball / projectile), then drain escape.</summary>
    private static void UpdateBatCarrier(GameInstance g, Projectile hz, double drainLine)
    {
        var ball = g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId);

        // Rescue: another living ball or any spell projectile pops the carrier.
        var rescued =
            g.Balls.Any(b => b.Alive && b.Id != hz.CarriedBallId
                && (b.Pos - hz.Pos).Length <= hz.Radius + b.Radius)
            || g.Projectiles.Any(p => p.Alive && (p.Pos - hz.Pos).Length <= hz.Radius + p.Radius);
        if (rescued)
        {
            hz.Alive = false;
            if (ball != null)
            {
                var lean = g.Rng.Range(-0.3, 0.3);
                ball.Vel = new Vec2(lean, -1).Normalized() * g.Config.BallSpeed;
                ball.GrabberId = 0;
            }
            // The bat flees upward as the usual harmless flyaway.
            g.Hazards.Add(new Projectile
            {
                Id     = g._nextHazardId++,
                Pos    = hz.Pos,
                Vel    = new Vec2(0, -g.Config.BatFlyAwaySpeed),
                Damage = 0,
                Radius = g.Config.EnemyHazardRadius,
                Alive  = true,
                Kind   = "bat",
            });
            g.RaiseEvent("batRelease", hz.Pos.X, hz.Pos.Y);
            g._log.Log(g.TickCount, "bat", "carrier popped — ball rescued", $"ball={hz.CarriedBallId}");
            return;
        }

        // Escape: carrier reaches the drain — the stolen ball is lost (WinLose handles it).
        if (hz.Pos.Y - hz.Radius > drainLine)
        {
            hz.Alive = false;
            if (ball != null) { ball.GrabberId = 0; ball.Vel = new Vec2(0, g.Config.BatCarrySpeed); }
            g._log.Log(g.TickCount, "bat", "carried ball to the drain", $"ball={hz.CarriedBallId}");
        }
    }

    /// <summary>One tick of the Witch's grab-hand: home → grab → carry to boss → hold → throw.</summary>
    private static void UpdateWitchGrab(GameInstance g, Projectile hz, double drainLine, double dt)
    {
        // Poppable while CARRYING by a second ball or any spell projectile (same
        // counterplay language as the bat carrier — R3). While homing, contact with
        // the target ball is the grab, not a pop.
        var popped = hz.CarriedBallId > 0 &&
            (g.Balls.Any(b => b.Alive && b.Id != hz.CarriedBallId
                && (b.Pos - hz.Pos).Length <= hz.Radius + b.Radius)
             || g.Projectiles.Any(p => p.Alive && p.Kind != "allybolt"
                && (p.Pos - hz.Pos).Length <= hz.Radius + p.Radius));
        if (popped)
        {
            hz.Alive = false;
            var held = g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId);
            if (held != null)
            {
                var lean = g.Rng.Range(-0.3, 0.3);
                held.Vel = new Vec2(lean, -1).Normalized() * g.Config.BallSpeed;
                held.GrabberId = 0;
            }
            g.RaiseEvent("witchGrabPopped", hz.Pos.X, hz.Pos.Y);
            g._log.Log(g.TickCount, "boss", "witch grab popped", "");
            return;
        }

        var bossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        if (bossBlocks.Count == 0) { hz.Alive = false; return; } // boss died mid-grab

        if (hz.CarriedBallId == 0)
        {
            // Homing phase: steer toward the nearest free ball; grab on contact.
            var target = g.Balls.Where(b => b.Alive && b.GrabberId == 0)
                                .OrderBy(b => (b.Pos - hz.Pos).Length).FirstOrDefault();
            if (target == null) { hz.Alive = false; return; }
            hz.Vel = (target.Pos - hz.Pos).Normalized() * g.Config.WitchGrabSpeed;
            if ((target.Pos - hz.Pos).Length <= hz.Radius + target.Radius)
            {
                hz.CarriedBallId = target.Id;
                target.GrabberId = hz.Id;
                target.Vel       = new Vec2(0, 0);
                hz.StateTimer    = 0;
                g.RaiseEvent("witchGrab", hz.Pos.X, hz.Pos.Y);
                g._log.Log(g.TickCount, "boss", "witch grabbed ball", $"ball={target.Id}");
            }
            // Missed and flew past the board → give up.
            if (hz.Pos.Y - hz.Radius > drainLine || hz.Pos.Y < g.Config.BoardOriginY - g.Config.CellSize)
                hz.Alive = false;
            return;
        }

        // Carry phase: bring the ball home to the boss, then hold and throw.
        var bossC = g.Level.Grid.CellCenter(bossBlocks[0].Col, bossBlocks[0].Row);
        var toBoss = bossC - hz.Pos;
        if (toBoss.Length > g.Config.CellSize)
        {
            hz.Vel = toBoss.Normalized() * g.Config.WitchGrabSpeed;
            return;
        }
        hz.Vel = new Vec2(0, 0);
        hz.StateTimer += dt;
        if (hz.StateTimer < g.Config.WitchThrowDelay) return;

        // The throw: hurl the ball down at the paddle, faster than normal — catch it!
        var ball = g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId);
        hz.Alive = false;
        if (ball != null)
        {
            ball.GrabberId = 0;
            var dir = (g.Paddle.Center - ball.Pos).Normalized();
            ball.Vel = dir * g.Config.BallSpeed * g.Config.WitchThrowSpeedMult;
        }
        g.RaiseEvent("witchThrow", hz.Pos.X, hz.Pos.Y);
        g._log.Log(g.TickCount, "boss", "witch threw the ball", "");
    }

    /// <summary>Damage each block a falling stalactite passes through, once per block.</summary>
    private static void StalactitePierceBlocks(GameInstance g, Projectile hz)
    {
        var cell = g.Config.CellSize;
        foreach (var blk in g.Blocks)
        {
            // Boss blocks are exempt: the Goblin rains stalactites from his own row —
            // no friendly fire on the rig.
            if (blk.Dead || blk.Indestructible || blk.Boss) continue;
            var c   = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            var box = Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(hz.Pos, hz.Radius)) continue;
            hz.HitBlockIds ??= new HashSet<int>();
            if (!hz.HitBlockIds.Add(blk.Id)) continue; // one hit per block while passing
            BlockDamage.DamageBlock(g, blk, g.Config.StalactiteBlockDamage, igniteSource: false);
            break; // one block per tick keeps it deterministic
        }
    }

    // -----------------------------------------------------------------------
    // Player HP
    // -----------------------------------------------------------------------

    internal static void DamagePlayer(GameInstance g, int dmg)
    {
        // Second Wind relic: the first HP loss each level is negated.
        if (g.HasRelic("second_wind") && !g._secondWindUsed)
        {
            g._secondWindUsed = true;
            g.RaiseEvent("secondWind", g.Paddle.Center.X, g.Paddle.Center.Y);
            g._log.Log(g.TickCount, "relic", "second wind — HP loss negated", "");
            return;
        }
        g.Lives -= dmg;
        g.RaiseEvent("playerHit", g.Paddle.Center.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "hp", "player hit", $"lives={g.Lives}");
        if (g.Lives <= 0)
        {
            g.Phase = GamePhase.Lost;
            g.RaiseEvent("levelLost", 0, 0);
            g._log.Log(g.TickCount, "lose", "hp depleted");
        }
    }
}
