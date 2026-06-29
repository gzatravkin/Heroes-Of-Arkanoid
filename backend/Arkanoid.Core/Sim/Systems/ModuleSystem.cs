using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// §2 Modules — slot-bound PASSIVES, one strong bespoke behaviour each (design §2; NO sub-stats). Like
/// <see cref="CardSystem"/>, each module's identity is its trigger + lever, hooked into the sim at the exact
/// moment it names. A module fires only while equipped (<see cref="GameInstance.HasModule"/>); per-level
/// scaling reads <see cref="GameInstance.ModuleLevel"/>.
/// </summary>
internal static class ModuleSystem
{
    // ── ball → block damage bonus (added at the BallSystem hit site) ──
    internal static int BallDamageBonus(GameInstance g, Ball b, Block target)
    {
        if (!g.AnyModules) return 0;
        int bonus = 0;
        // Tidal Core: alternates each deflect — in HEAVY mode the ball hits harder (SWIFT mode trades that
        // for speed, handled in OnBallTick). Half your time in each mode.
        if (g.HasModule("tidal_core") && !g._tidalSwift)
            bonus += 1 + g.ModuleLevel("tidal_core");
        // Hollow Ball: big & light — wide coverage but LOW per-hit damage (the coverage/damage tradeoff).
        if (g.HasModule("hollow_ball"))
            bonus -= 1; // reduced punch (BallSystem floors total damage at ≥1)
        // Brittle Glass Ball: HUGE per-hit damage — the upside of its shatter-risk durability economy.
        if (g.HasModule("brittle_glass"))
            bonus += 3 + 2 * g.ModuleLevel("brittle_glass");
        // Twin Soul Core: each twin hits softer (the cost of running two balls + a tether).
        if (g.HasModule("twin_soul_core") && b.Twin)
            bonus -= 1;
        return bonus;
    }

    /// <summary>Toll Roads (§2): gate the per-kill gold/crystal payout. Returns the crystals to actually
    /// award for a kill worth <paramref name="baseCrystals"/>. Normally unchanged; with Toll Roads equipped,
    /// only crit kills or kills inside a perfect-deflect window pay — and they pay DOUBLE (skill-gated economy).</summary>
    internal static int KillCrystals(GameInstance g, int baseCrystals)
    {
        if (!g.HasModule("toll_roads")) return baseCrystals;
        bool paid = g.LastHitWasCrit || g._tollPerfectWindow > 0;
        g._log.Log(g.TickCount, "module", "toll_roads", paid ? $"paid x2 ({baseCrystals * 2})" : "suppressed (0)");
        return paid ? baseCrystals * 2 : 0;
    }

    /// <summary>Brittle Glass (§2) + other post-hit module reactions. Called after a ball→block hit resolves.</summary>
    internal static void OnBlockHit(GameInstance g, Ball b, Block blk, int dmgDealt, int hpBefore)
    {
        if (!g.AnyModules) return;
        // Brittle Glass Ball: it SHATTERS the instant it strikes something unbreakable — the durability
        // cost of its huge damage. Counterplay: don't let it hit indestructible blocks (catch/steer away).
        if (g.HasModule("brittle_glass") && blk.Indestructible && b.Alive)
        {
            b.Alive = false;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.BlockDestroyed, c.X, c.Y);
            g._log.Log(g.TickCount, "module", "brittle_glass", $"ball {b.Id} shattered on indestructible block={blk.Id}");
        }
    }

    // ── a ball was served (called from GameInstance.Serve, per ball) ──
    internal static void OnServe(GameInstance g, Ball b)
    {
        if (!g.AnyModules) return;
        // Hollow Ball: serve a big, light ball — wide board coverage (the upside of the tradeoff).
        if (g.HasModule("hollow_ball"))
            b.Radius = g.Config.BallRadius * (1.5 + 0.1 * g.ModuleLevel("hollow_ball"));
        // Twin Soul Core: the served ball is one of the tethered twins.
        if (g.HasModule("twin_soul_core")) b.Twin = true;
        // Fission Core: the served ball can split on kills + re-fuse on a catch.
        if (g.HasModule("fission_core")) b.Fission = true;
    }

    /// <summary>Called once after the serve loop. Twin Soul Core needs TWO tethered twins — spawn the partner.</summary>
    internal static void AfterServe(GameInstance g)
    {
        if (!g.HasModule("twin_soul_core")) return;
        var twins = g.Balls.Where(x => x.Twin && x.Alive).ToList();
        if (twins.Count >= 2 || twins.Count == 0) return;
        var src = twins[0];
        double speed = src.Vel.Length;
        if (speed < 1e-6) speed = g.Config.BallSpeed;
        // Fan the two twins WIDE apart (±35° from straight up) so the tether strung between them spans a real
        // gap of the field and slices blocks as they rise — not a useless near-vertical pair.
        const double rad = 35.0 * System.Math.PI / 180.0;
        double sin = System.Math.Sin(rad), cos = System.Math.Cos(rad);
        src.Vel = new Vec2(-sin * speed, -cos * speed); // left-leaning
        g.Balls.Add(new Ball
        {
            Id     = g._nextBallId++,
            Radius = src.Radius,
            Pos    = new Vec2(src.Pos.X, src.Pos.Y),
            Vel    = new Vec2(sin * speed, -cos * speed), // right-leaning → they spread apart
            Alive  = true,
            Twin   = true,
        });
        g._log.Log(g.TickCount, "module", "twin_soul_core", "spawned the second twin (tether active)");
    }

    // ── a ball was deflected by the paddle (called from SpellSystem.OnPaddleHit) ──
    internal static void OnPaddleHit(GameInstance g, Ball b, double t, bool isPerfect)
    {
        if (!g.AnyModules) return;

        // Tidal Core: flip between HEAVY and SWIFT on every deflect.
        if (g.HasModule("tidal_core"))
        {
            g._tidalSwift = !g._tidalSwift;
            g._log.Log(g.TickCount, "module", "tidal_core", g._tidalSwift ? "swift" : "heavy");
        }

        // Gyro Paddle: the deflect angle is driven by how fast the paddle is MOVING — a still paddle sends
        // the ball straight, a sweeping paddle whips it sideways. Double-edged (control vs. wildness).
        if (g.HasModule("gyro_paddle"))
        {
            double speed = b.Vel.Length;
            if (speed > 1e-6)
            {
                double influence = 0.06 + 0.02 * g.ModuleLevel("gyro_paddle");
                var v = new Vec2(b.Vel.X + g._paddleVelX * influence, b.Vel.Y).Normalized() * speed;
                b.Vel = Physics.BallPhysics.EnforceMinAngle(v);
                g._log.Log(g.TickCount, "module", "gyro_paddle", $"paddleVel={g._paddleVelX:0.0} → vx={b.Vel.X:0}");
            }
        }

        // Toll Roads: a PERFECT deflect opens a short window during which kills actually pay gold.
        if (g.HasModule("toll_roads") && isPerfect)
            g._tollPerfectWindow = 2.5; // seconds

        // Fission Core: a paddle CATCH re-fuses all the fragments into ONE bigger ball (the fused payoff).
        if (g.HasModule("fission_core"))
        {
            var frags = g.Balls.Where(x => x.Fission && x.Alive).ToList();
            if (frags.Count > 1)
            {
                // Keep the one we just deflected (b); fuse the rest into it, growing the radius.
                double grown = b.Radius;
                foreach (var f in frags)
                {
                    if (f == b) continue;
                    f.Alive = false;
                    grown += g.Config.BallRadius * 0.25;
                }
                b.Radius = System.Math.Min(g.Config.BallRadius * 1.6, grown);
                g._log.Log(g.TickCount, "module", "fission_core", $"fused {frags.Count} fragments → r={b.Radius:0.0}");
            }
        }

        // Spin-Loaded: an EDGE deflect (caught off-centre) imparts SPIN — the ball then curves. The further
        // from centre, the more spin. Decays over time (OnBallTick). Erratic edge play.
        if (g.HasModule("spin_loaded") && System.Math.Abs(t) > 0.45)
        {
            b.Spin = System.Math.Sign(t) * (1.5 + 0.5 * g.ModuleLevel("spin_loaded")); // rad/sec, sign follows edge
            g._log.Log(g.TickCount, "module", "spin_loaded", $"edge hit t={t:0.00} → spin={b.Spin:0.0}");
        }

        // Drumhead Paddle: a PERFECT (dead-centre) deflect sends a shockwave straight up the paddle's
        // column, damaging every block in that column. Perfect-deflect only; one column.
        if (g.HasModule("drumhead_paddle") && isPerfect)
        {
            int col = (int)System.Math.Floor((g.Paddle.Center.X - g.Level.Grid.OriginX) / g.Config.CellSize);
            int dmg = 2 + g.ModuleLevel("drumhead_paddle");
            int hits = 0;
            foreach (var blk in g.Blocks.Where(bl => !bl.Dead && bl.Col == col && !bl.Indestructible).ToList())
            { BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, killMult: 0.5); hits++; }
            var c = g.Level.Grid.CellCenter(System.Math.Clamp(col, 0, g.Level.Grid.Cols - 1), 0);
            g.RaiseEvent(SimEventKind.Lightning, c.X, g.Paddle.Center.Y);
            g._log.Log(g.TickCount, "module", "drumhead_paddle", $"shockwave col={col} dmg={dmg} blocks={hits}");
        }
    }

    // ── global per-tick (called from the tick pipeline) ──
    internal static void Update(GameInstance g, double dt)
    {
        if (!g.AnyModules) return;
        // Pressure Cooker: the whole block field creeps DOWN on a timer (bringing blocks closer — easier to
        // hit, but an overrun risk). Each kill pushes it back up; clear fast or get crowded out.
        if (g.HasModule("pressure_cooker"))
        {
            const double interval = 6.0; // seconds per descent
            g._pressureDescendAccum += dt;
            if (g._pressureDescendAccum >= interval)
            {
                g._pressureDescendAccum -= interval;
                ShiftField(g, +1); // descend; loses the level if a needToKill block overruns the bottom
            }
        }

        // Twin Soul Core: the TETHER between the two twins slices blocks it crosses. If a twin dies, the
        // tether dies (lose one → no more tether).
        if (g.HasModule("twin_soul_core"))
        {
            var twins = g.Balls.Where(x => x.Twin && x.Alive).ToList();
            if (twins.Count < 2) { g.TwinTether = null; }
            else
            {
                var a = twins[0].Pos; var bb = twins[1].Pos;
                g.TwinTether = (a.X, a.Y, bb.X, bb.Y);
                g._twinTetherAccum += dt;
                if (g._twinTetherAccum >= 0.25) // slice cadence
                {
                    g._twinTetherAccum = 0;
                    TetherSlice(g, a, bb, 1 + g.ModuleLevel("twin_soul_core"));
                }
            }
        }
    }

    /// <summary>Damage each distinct destructible block the tether segment A→B passes through.</summary>
    private static void TetherSlice(GameInstance g, Vec2 a, Vec2 b, int dmg)
    {
        double cell = g.Config.CellSize;
        var dir = b - a; double len = dir.Length;
        if (len < 1e-6) return;
        dir = dir.Normalized();
        var hit = new System.Collections.Generic.HashSet<int>();
        for (double s = 0; s <= len; s += cell * 0.4)
        {
            var p = a + dir * s;
            int col = (int)System.Math.Floor((p.X - g.Level.Grid.OriginX) / cell);
            int row = (int)System.Math.Floor((p.Y - g.Level.Grid.OriginY) / cell);
            var blk = g.BlockAt(col, row);
            if (blk != null && !blk.Indestructible && hit.Add(blk.Id))
                BlockDamage.DamageBlock(g, blk, dmg, igniteSource: false, killMult: 0.5);
        }
        if (hit.Count > 0) g._log.Log(g.TickCount, "module", "twin_soul_core", $"tether sliced {hit.Count} block(s)");
    }

    // ── a destructible block was destroyed (called from BlockDamage's kill branch) ──
    internal static void OnBlockDestroyed(GameInstance g, Block blk)
    {
        if (!g.AnyModules) return;
        // Pressure Cooker: every few kills, shove the field back UP one row (reward for clearing).
        if (g.HasModule("pressure_cooker"))
        {
            int need = System.Math.Max(2, 5 - g.ModuleLevel("pressure_cooker")); // higher level = easier to hold
            if (++g._pressureKills >= need)
            {
                g._pressureKills = 0;
                ShiftField(g, -1); // push back up (clamped at the top row)
            }
        }

        // Fission Core: every Nth kill the ball SPLITS into two smaller balls (scattered/fragile). Capped so
        // it doesn't avalanche. Re-fuses on a paddle catch (OnPaddleHit) into one bigger ball.
        if (g.HasModule("fission_core"))
        {
            int every = System.Math.Max(2, 4 - (g.ModuleLevel("fission_core") - 1)); // higher level = splits sooner
            if (++g._fissionKills >= every)
            {
                g._fissionKills = 0;
                var src = g.Balls.FirstOrDefault(x => x.Fission && x.Alive && x.Vel.Length > 1e-6);
                int liveFission = g.Balls.Count(x => x.Fission && x.Alive);
                if (src != null && liveFission < 4)
                {
                    double newR = System.Math.Max(g.Config.BallRadius * 0.5, src.Radius * 0.72);
                    src.Radius = newR; // the source shrinks too (fragments)
                    // perpendicular split so the two fragments fan apart
                    var v = src.Vel; var perp = new Vec2(-v.Y, v.X).Normalized();
                    g.Balls.Add(new Ball
                    {
                        Id = g._nextBallId++, Radius = newR, Pos = new Vec2(src.Pos.X, src.Pos.Y),
                        Vel = (v.Normalized() + perp * 0.5).Normalized() * v.Length, Alive = true, Fission = true,
                    });
                    src.Vel = (v.Normalized() - perp * 0.5).Normalized() * v.Length;
                    g._log.Log(g.TickCount, "module", "fission_core", $"split → {liveFission + 1} fragments (r={newR:0.0})");
                }
            }
        }
    }

    /// <summary>Shift every live non-boss block by <paramref name="dRow"/> rows. +1 descends (overrun at the
    /// bottom row loses the level — same rule as the Hell descend); -1 pushes up (clamped to row 0).</summary>
    private static void ShiftField(GameInstance g, int dRow)
    {
        int lastRow = g.Level.Grid.Rows - 1;
        bool moved = false;
        foreach (var b in g.Blocks)
        {
            if (b.Dead || b.Boss) continue;
            if (dRow > 0)
            {
                b.Row++;
                moved = true;
                if (b.Row >= lastRow && b.NeedToKill)
                {
                    g.Phase = GamePhase.Lost;
                    g.RaiseEvent(SimEventKind.Overrun, g.Level.Grid.CellCenter(b.Col, b.Row).X, g.Level.Grid.Height);
                    g.RaiseEvent(SimEventKind.LevelLost, 0, 0);
                    g._log.Log(g.TickCount, "module", "pressure_cooker", "OVERRUN — field reached the paddle");
                    return;
                }
            }
            else if (b.Row > 0) { b.Row--; moved = true; }
        }
        if (moved)
        {
            g.MarkBlocksDirty();
            g.InvalidateBlockGrid();
            g.RaiseEvent(SimEventKind.Descend, 0, 0);
            g._log.Log(g.TickCount, "module", "pressure_cooker", dRow > 0 ? "field descends" : "field pushed back up");
        }
    }

    /// <summary>Riposte Paddle (§2): an enemy hazard reaching the paddle is PARRIED back up as a damaging
    /// bolt instead of hurting the player — but only while the paddle is actively moving (the parry timing).
    /// Returns true if parried (caller must NOT damage the player). Enemy attacks only.</summary>
    internal static bool OnHazardHitPaddle(GameInstance g, Projectile hz)
    {
        if (!g.HasModule("riposte_paddle")) return false;
        // The "timing window": you must be actively swinging the paddle to parry; a stationary paddle eats it.
        if (System.Math.Abs(g._paddleVelX) < 4.0) return false;
        int dmg = 2 + g.ModuleLevel("riposte_paddle");
        g.Projectiles.Add(new Projectile
        {
            Id               = g._nextProjId++,
            Pos              = new Vec2(hz.Pos.X, g.Paddle.Center.Y - g.Paddle.Height / 2),
            Vel              = new Vec2(0, -g.Config.BallSpeed),
            Damage           = dmg,
            Radius           = g.Config.BallRadius * 0.5,
            Kind             = "riposte",
            PiercingHitsLeft = 1,
        });
        g.RaiseEvent(SimEventKind.Lightning, hz.Pos.X, g.Paddle.Center.Y);
        g._log.Log(g.TickCount, "module", "riposte_paddle", $"parried a {hz.Kind} → bolt dmg={dmg}");
        return true;
    }

    // ── per-ball, per-tick (called from BallSystem.UpdateBallStep before collisions) ──
    internal static void OnBallTick(GameInstance g, Ball b, double dt)
    {
        if (!g.AnyModules) return;
        // Tidal Core: in SWIFT mode the ball flies faster (the other half of the heavy/swift cycle).
        if (g.HasModule("tidal_core") && g._tidalSwift)
        {
            double speed = b.Vel.Length;
            if (speed > 1e-6)
            {
                double mult = 1.0 + 0.10 + 0.04 * g.ModuleLevel("tidal_core");
                b.Vel = b.Vel.Normalized() * g.Config.BallSpeed * mult;
            }
        }
        // Hollow Ball: a big, LIGHT ball drifts ERRATICALLY — a tiny random heading wobble each tick (the
        // downside paired with its wide coverage). Kept off near-horizontal by EnforceMinAngle.
        if (g.HasModule("hollow_ball"))
        {
            double speed = b.Vel.Length;
            if (speed > 1e-6)
            {
                double jitter = (g.Rng.NextDouble() - 0.5) * 0.06; // ±0.03 rad/tick random walk
                double cos = System.Math.Cos(jitter), sin = System.Math.Sin(jitter);
                b.Vel = Physics.BallPhysics.EnforceMinAngle(
                    new Vec2(b.Vel.X * cos - b.Vel.Y * sin, b.Vel.X * sin + b.Vel.Y * cos));
            }
        }

        // Spin-Loaded: a spinning ball CURVES — rotate its heading by the spin each tick; the spin decays.
        if (g.HasModule("spin_loaded") && System.Math.Abs(b.Spin) > 0.01)
        {
            double speed = b.Vel.Length;
            if (speed > 1e-6)
            {
                double a = b.Spin * dt;
                double cos = System.Math.Cos(a), sin = System.Math.Sin(a);
                b.Vel = Physics.BallPhysics.EnforceMinAngle(
                    new Vec2(b.Vel.X * cos - b.Vel.Y * sin, b.Vel.X * sin + b.Vel.Y * cos));
            }
            b.Spin *= System.Math.Pow(0.5, dt / 1.5); // half-life ~1.5s
        }

        // Gravity Well: the arena PULLS the ball toward the densest part of the block field (its centroid),
        // making edge blocks awkward to reach. Gentle steering, like a magnet toward the mass centre.
        if (g.HasModule("gravity_well"))
        {
            double sx = 0, sy = 0; int n = 0;
            foreach (var blk in g.Blocks)
            {
                if (blk.Dead || !blk.NeedToKill || blk.Indestructible) continue;
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                sx += c.X; sy += c.Y; n++;
            }
            double speed = b.Vel.Length;
            if (n > 0 && speed > 1e-6)
            {
                var want = System.Math.Atan2(sy / n - b.Pos.Y, sx / n - b.Pos.X);
                var have = System.Math.Atan2(b.Vel.Y, b.Vel.X);
                var diff = System.Math.IEEERemainder(want - have, System.Math.PI * 2);
                var maxTurn = (40 + 20 * g.ModuleLevel("gravity_well")) * System.Math.PI / 180.0 * dt;
                var turn = System.Math.Clamp(diff, -maxTurn, maxTurn);
                var ang = have + turn;
                b.Vel = Physics.BallPhysics.EnforceMinAngle(new Vec2(System.Math.Cos(ang), System.Math.Sin(ang)) * speed);
            }
        }

        // Toll Roads: tick down the perfect-deflect "paid" window.
        if (g.HasModule("toll_roads") && g._tollPerfectWindow > 0)
            g._tollPerfectWindow = System.Math.Max(0, g._tollPerfectWindow - dt);
    }
}
