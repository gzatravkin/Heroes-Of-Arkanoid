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
        var drainLine = g.DrainY;
        var paddleBox = Aabb.FromCenter(g.Paddle.Center, g.Paddle.Width / 2, g.Paddle.Height / 2);

        // Index loop: UpdateBatCarrier may append the flyaway hazard mid-iteration.
        for (int i = 0; i < g.Hazards.Count; i++)
        {
            var hz = g.Hazards[i];
            if (!hz.Alive) continue;
            // Telegraph/warm-up: inert (no move, no damage, no despawn) while warning — e.g. the cart at the
            // edge before it rolls.
            if (hz.Warmup > 0) { hz.Warmup -= dt; continue; }
            hz.Pos += hz.Vel * dt;

            // Cart (Option A 2026-06-15): a rolling obstacle ABOVE the paddle that DEFLECTS the ball — never a
            // paddle-line HP hazard (a left-right paddle can't dodge a full-line sweep). Despawns off the side.
            if (hz.Behavior == HazardBehavior.Cart)
            {
                DeflectBallsOffCart(g, hz);
                if (hz.Pos.X < -hz.Radius || hz.Pos.X > g.Level.Grid.Width + hz.Radius)
                    hz.Alive = false;
                continue;
            }

            // Bat carrier (LEGACY, reverted 2026-06-16): holds the ball briefly, then releases + rewards.
            if (hz.Behavior == HazardBehavior.Bat && hz.CarriedBallId > 0)
            {
                UpdateBatCarrier(g, hz, dt);
                continue;
            }

            // Witch grab-hand: homes on a ball, carries it to the boss, holds, then
            // hurls it at the paddle. Poppable like the bat carrier (docs/11).
            if (hz.Behavior == HazardBehavior.WitchGrab)
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
            if (hz.Behavior == HazardBehavior.Stalactite)
                StalactitePierceBlocks(g, hz);

            if (paddleBox.IntersectsCircle(hz.Pos, hz.Radius))
            {
                hz.Alive = false;
                // §2 Riposte Paddle: a moving paddle PARRIES the hit back up as damage instead of taking it.
                if (!ModuleSystem.OnHazardHitPaddle(g, hz))
                    DamagePlayer(g, hz.Damage);
                continue;
            }

            if (hz.Pos.Y - hz.Radius > drainLine)
                hz.Alive = false; // dodged / missed
        }
        g.Hazards.RemoveAll(h => !h.Alive);
    }

    /// <summary>Cart obstacle (Option A 2026-06-15): reflect any ball it overlaps off its surface and push the
    /// ball clear, so the rolling cart bounces the ball (a mine cart on rails) instead of harming the paddle.</summary>
    private static void DeflectBallsOffCart(GameInstance g, Projectile cart)
    {
        foreach (var ball in g.Balls)
        {
            if (!ball.Alive) continue;
            var d = ball.Pos - cart.Pos;
            double r = cart.Radius + ball.Radius;
            if (d.LengthSquared >= r * r) continue;             // not overlapping
            var n = d.Length > 1e-4 ? d.Normalized() : new Vec2(0, -1);
            double into = ball.Vel.X * n.X + ball.Vel.Y * n.Y;  // < 0 ⇒ ball moving INTO the cart
            if (into < 0)
                ball.Vel = new Vec2(ball.Vel.X - 2 * into * n.X, ball.Vel.Y - 2 * into * n.Y); // reflect (speed-preserving)
            ball.Pos = cart.Pos + n * (r + 0.5);                // push the ball outside so it doesn't stick
        }
    }

    /// <summary>One tick of a bat carrier (LEGACY behaviour, reverted 2026-06-16): it HOLDS the ball
    /// (which rides along) for BatHoldTime, then releases it AND grants the player a reward (a wide-paddle
    /// buff — the legacy paddle-speed boost has no analogue in this X-input control), then flies away. An
    /// early pop (a second ball or any spell projectile) frees the ball immediately but skips the reward.
    /// The bat is NO LONGER a drain threat.</summary>
    private static void UpdateBatCarrier(GameInstance g, Projectile hz, double dt)
    {
        var ball = g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId);
        if (ball != null) ball.Pos = hz.Pos; // the held ball rides the bat

        // Early pop: a second living ball or any spell projectile frees the ball (no reward).
        var popped =
            g.Balls.Any(b => b.Alive && b.Id != hz.CarriedBallId
                && (b.Pos - hz.Pos).Length <= hz.Radius + b.Radius)
            || g.Projectiles.Any(p => p.Alive && (p.Pos - hz.Pos).Length <= hz.Radius + p.Radius);

        hz.StateTimer -= dt;
        if (!popped && hz.StateTimer > 0) return; // still holding

        hz.Alive = false;
        ReleaseBall(g, ball);
        if (!popped)
        {
            // Risk→reward: hold the full time and the bat gifts a wide paddle on release.
            BonusSystem.ActivateWidePaddle(g, g.Config.Pickups.EffectDuration);
            g._log.Log(g.TickCount, "bat", "released ball + granted wide paddle", $"ball={hz.CarriedBallId}");
        }
        // The bat flees upward as the usual harmless flyaway.
        g.Hazards.Add(new Projectile
        {
            Id     = g._nextHazardId++,
            Pos    = hz.Pos,
            Vel    = new Vec2(0, -g.Config.Enemies.BatFlyAwaySpeed),
            Damage = 0,
            Radius = g.Config.Enemies.HazardRadius,
            Alive  = true,
            Kind   = "bat",
        });
        g.RaiseEvent(SimEventKind.BatRelease, hz.Pos.X, hz.Pos.Y);
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
            ReleaseBall(g, g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId));
            g.RaiseEvent(SimEventKind.WitchGrabPopped, hz.Pos.X, hz.Pos.Y);
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
            hz.Vel = (target.Pos - hz.Pos).Normalized() * g.Config.Boss.WitchGrabSpeed;
            if ((target.Pos - hz.Pos).Length <= hz.Radius + target.Radius)
            {
                hz.CarriedBallId = target.Id;
                target.GrabberId = hz.Id;
                target.Vel       = new Vec2(0, 0);
                hz.StateTimer    = 0;
                g.RaiseEvent(SimEventKind.WitchGrab, hz.Pos.X, hz.Pos.Y);
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
            hz.Vel = toBoss.Normalized() * g.Config.Boss.WitchGrabSpeed;
            return;
        }
        hz.Vel = new Vec2(0, 0);
        hz.StateTimer += dt;
        if (hz.StateTimer < g.Config.Boss.WitchThrowDelay) return;

        // The throw: hurl the ball down at the paddle, faster than normal — catch it!
        var ball = g.Balls.FirstOrDefault(b => b.Id == hz.CarriedBallId);
        hz.Alive = false;
        if (ball != null)
        {
            ball.GrabberId = 0;
            var dir = (g.Paddle.Center - ball.Pos).Normalized();
            ball.Vel = dir * g.Config.BallSpeed * g.Config.Boss.WitchThrowSpeedMult;
        }
        g.RaiseEvent(SimEventKind.WitchThrow, hz.Pos.X, hz.Pos.Y);
    }

    /// <summary>Damage each block a falling stalactite passes through, once per block.</summary>
    private static void StalactitePierceBlocks(GameInstance g, Projectile hz)
    {
        var cell = g.Config.CellSize;
        var grid = g.Level.Grid;
        // Restrict scan to the stalactite's column — O(rows) instead of O(all blocks).
        int col = (int)System.Math.Floor((hz.Pos.X - grid.OriginX) / cell);
        for (int row = 0; row < grid.Rows; row++)
        {
            // Boss blocks are exempt: the Goblin rains stalactites from his own row.
            var blk = g.BlockAt(col, row);
            if (blk == null || blk.Indestructible || blk.Boss) continue;
            var c   = grid.CellCenter(blk.Col, blk.Row);
            var box = Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(hz.Pos, hz.Radius)) continue;
            hz.HitBlockIds ??= new HashSet<int>();
            if (!hz.HitBlockIds.Add(blk.Id)) continue; // one hit per block while passing
            BlockDamage.DamageBlock(g, blk, g.Config.Enemies.StalactiteBlockDamage, igniteSource: false, killMult: 0.5);
            break; // one block per tick keeps it deterministic
        }
    }

    // -----------------------------------------------------------------------
    // Player HP
    // -----------------------------------------------------------------------

    /// <summary>Release a grabbed ball with a random lean — shared by bat-carrier and witch-grab pop paths.</summary>
    private static void ReleaseBall(GameInstance g, Ball? ball)
    {
        if (ball == null) return;
        ball.Vel       = new Vec2(g.Rng.Range(-0.3, 0.3), -1).Normalized() * g.Config.BallSpeed;
        ball.GrabberId = 0;
    }

    internal static void DamagePlayer(GameInstance g, int dmg)
    {
        // Post-hit i-frames (2026-06-15): immune for a few seconds after any hit — no rapid multi-hits / DoT stacking.
        if (g._damageImmunity > 0) return;
        // Second Wind relic: the first HP loss each level is negated.
        if (Modifiers.HasSecondWind(g) && !g._secondWindUsed)
        {
            g._secondWindUsed = true;
            g.RaiseEvent(SimEventKind.SecondWind, g.Paddle.Center.X, g.Paddle.Center.Y);
            return;
        }
        g.Hp -= dmg;
        g._damageImmunity = g.Config.DamageImmunity; // start i-frames
        g.RaiseEvent(SimEventKind.PlayerHit, g.Paddle.Center.X, g.Paddle.Center.Y);
        ReckoningSystem.OnHpLost(g, dmg); // §3 Reckoning: HP lost charges the smite meter
        CardSystem.OnHpLost(g, dmg);      // §1 Martyr's Brand: getting hit grants a vengeance buff
        if (g.Hp <= 0)
        {
            g.Phase = GamePhase.Lost;
            g.RaiseEvent(SimEventKind.LevelLost, 0, 0);
        }
    }
}
