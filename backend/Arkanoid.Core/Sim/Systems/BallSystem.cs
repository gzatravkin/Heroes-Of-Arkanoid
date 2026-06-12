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

        // Top-wall contact this tick (read before ResolveWalls flips the velocity).
        var hitTop = b.Pos.Y - b.Radius < 0 && b.Vel.Y < 0;
        Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, g.Level.Grid.Width, g.Config);
        if (hitTop)
            SpellSystem.OnTopWallBounce(g, b); // Paladin Last Day column smite

        if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, g.Paddle, g.Config, out var t))
        {
            SpellSystem.OnPaddleHit(g, b, t);
            // Combo resets on every paddle contact (streak broken).
            g.Combo.Count = 0;
            g.Combo.Multiplier = 1;
        }

        ResolveBarriers(g, b);
        ResolveBlocks(g, b);
    }

    /// <summary>
    /// Paladin Shield barrier: if ball is moving downward and crosses a barrier's Y within its X-span,
    /// reflect it upward. Mirrors the same logic used for paddle deflection (simplified).
    /// </summary>
    private static void ResolveBarriers(GameInstance g, Ball b)
    {
        foreach (var barrier in g.Barriers)
        {
            if (!barrier.Alive) continue;
            // Only trigger for downward-moving ball crossing the barrier line
            if (b.Vel.Y <= 0) continue;
            double halfW = barrier.Width / 2.0;
            if (b.Pos.X < barrier.CenterX - halfW || b.Pos.X > barrier.CenterX + halfW) continue;
            // Check if ball's circle crossed the barrier this tick
            if (b.Pos.Y + b.Radius >= barrier.Y && b.Pos.Y - b.Radius <= barrier.Y + 4)
            {
                b.Vel = new Vec2(b.Vel.X, -System.Math.Abs(b.Vel.Y));
                g._log.Log(g.TickCount, "barrier", "reflected ball", $"ballId={b.Id} y={barrier.Y:F1}");
                g.RaiseEvent(SimEventKind.BarrierHit, barrier.CenterX, barrier.Y);
            }
        }
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
        g._log.Log(g.TickCount, "vase", "statues levelled up");
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
                    g._log.Log(g.TickCount, "teleport", "warped",
                        $"ball={b.Id} from=({blk.Col},{blk.Row}) to=({dest.Col},{dest.Row})");
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
                    g._log.Log(g.TickCount, "portal", "phase toggled", $"ball={b.Id} ghost={b.Ghost}");
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
                    Vel      = new Vec2(0, g.Config.Enemies.BatCarrySpeed),
                    Damage   = 0,
                    Radius   = g.Config.Enemies.HazardRadius,
                    Alive    = true,
                    Kind     = "bat",
                    Behavior = HazardBehavior.Bat,
                    CarriedBallId = b.Id,
                };
                g.Hazards.Add(carrier);
                b.GrabberId = carrier.Id;
                b.Vel       = new Vec2(0, 0);
                g.RaiseEvent(SimEventKind.BatGrab, c.X, c.Y);
                g._log.Log(g.TickCount, "bat", "carrying ball to drain", $"ball={b.Id} carrier={carrier.Id}");
                return; // ball is now held
            }

            // Lava: ball passes through — lava drains HP only when it reaches the paddle zone.
            if (blk.Lava) continue;

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
            // Echo core: the first block hit after each paddle deflect strikes harder.
            if (b.EchoArmed) { dmg += g.Config.EchoBonus; b.EchoArmed = false; }
            // Frost core: hitting an emitter/statue freezes its cadence.
            if (g.BallCores.Contains("frost") && (blk.Emitter || blk.ShieldStatue))
            {
                var freeze = g.Config.FrostFreezeSeconds
                    * (g.HasFusion("echo", "frost") ? g.Config.StasisFreezeMult : 1.0);
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

            BlockDamage.DamageBlock(g, blk, dmg, igniteSource: ignited, decaySource: decayed);
            if (ignited) b.IgniteHitsLeft--;
            if (decayed) b.DecayHitsLeft--;
            break; // one block per tick keeps it deterministic
        }
    }
}
