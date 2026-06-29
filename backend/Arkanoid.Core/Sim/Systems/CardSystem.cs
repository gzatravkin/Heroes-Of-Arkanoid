using System.Linq;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// §1 Cards — cross-hero, rule-breaking PASSIVE triggers. Each card is bespoke: its identity is its trigger
/// + balance lever (CLAUDE.md — content fidelity, not a shared archetype), hooked into the sim at the exact
/// moment its design names. A card fires only while equipped (<see cref="GameInstance.HasCard"/>); its
/// per-level scaling reads <see cref="GameInstance.CardLevel"/>.
/// </summary>
internal static class CardSystem
{
    // ── ball → block damage bonus (added at the BallSystem hit site, where the ball is in scope) ──
    internal static int BallDamageBonus(GameInstance g, Ball b, Block target)
    {
        if (!g.AnyCards) return 0;
        int bonus = 0;
        // Headhunter (common): +dmg to a block on the TOP row of the current field — rewards reaching the
        // far blocks. Position-gated to the top only.
        if (g.HasCard("headhunter") && target.Row == TopLiveRow(g))
            bonus += g.CardLevel("headhunter"); // +1/level

        // Underdog (common): +dmg to a block in the BOTTOM two rows of the current field.
        if (g.HasCard("underdog") && BottomLiveRow(g) is var br && br >= 0 && target.Row >= br - 1)
            bonus += g.CardLevel("underdog"); // +1/level

        // Cleanup Crew (rare): +dmg once the board is nearly clear (≤6 destructible blocks left) — the tail
        // only, so it never helps the main fight.
        if (g.HasCard("cleanup_crew") && LiveDestructibleCount(g) <= 6)
            bonus += 2 * g.CardLevel("cleanup_crew"); // +2/level

        // Bank Shot (rare): banks +dmg for each wall bounce since the last block hit, then resets. The lever
        // is "set up the carom" — more bounces before connecting = a bigger hit (capped so it can't run away).
        if (g.HasCard("bank_shot") && b.BankCharge > 0)
        {
            int cap = 3 + g.CardLevel("bank_shot"); // L1 = up to +4
            bonus += System.Math.Min(b.BankCharge, cap);
            b.BankCharge = 0; // "resets on any hit"
        }

        // Dead Center (common): a PERFECT deflect arms a burst on the FIRST block hit after it. Skill reward.
        if (g.HasCard("dead_center") && b.DeadCenterArmed)
        {
            bonus += 2 + g.CardLevel("dead_center"); // L1 = +3 burst
            b.DeadCenterArmed = false; // first-block only
        }

        // Metronome (epic): each consecutive perfect deflect adds +1 damage (capped); any non-perfect resets.
        if (g.HasCard("metronome") && g._metronomeStacks > 0)
            bonus += System.Math.Min(g._metronomeStacks, 4 + g.CardLevel("metronome"));

        // Martyr's Brand (rare): for a few seconds after you LOSE HP, your ball hits harder (vengeance).
        if (g.HasCard("martyrs_brand") && EffectSystem.HasEffect(g, "martyr_brand"))
            bonus += 2 + g.CardLevel("martyrs_brand");

        // Redline (epic): the longer the ball stays airborne (no paddle touch), the STRONGER it hits —
        // +1 per 0.5s aloft, capped. Paired with the speed ramp in OnBallTick ("harder to catch") tradeoff.
        if (g.HasCard("redline") && b.SincePaddle > 0)
            bonus += System.Math.Min(2 + g.CardLevel("redline"), (int)(b.SincePaddle / 0.5));

        if (bonus > 0)
            g._log.Log(g.TickCount, "card", "ball dmg", $"+{bonus} (row={target.Row} blocksLeft={LiveDestructibleCount(g)})");
        return bonus;
    }

    // ── a destructible block was just destroyed (called from BlockDamage's kill branch) ──
    internal static void OnBlockDestroyed(GameInstance g, Block blk, bool wasCrit)
    {
        if (!g.AnyCards) return;
        // Opening Gambit (common): the FIRST kill each level detonates a small AoE around it (once/level cap).
        if (g.HasCard("opening_gambit") && !g._cardOpeningGambitUsed)
        {
            g._cardOpeningGambitUsed = true;
            int dmg = 2 + (g.CardLevel("opening_gambit") - 1); // base 2, +1/level
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
            g._log.Log(g.TickCount, "card", "opening_gambit", $"aoe at col={blk.Col} row={blk.Row} dmg={dmg}");
            for (int dc = -1; dc <= 1; dc++)
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (dc == 0 && dr == 0) continue;
                    var nb = g.BlockAt(blk.Col + dc, blk.Row + dr);
                    if (nb != null) BlockDamage.DamageBlock(g, nb, dmg, igniteSource: false, killMult: 0.5);
                }
        }

        // Avalanche (rare): a kill during a HOT combo (≥8) may make the dead block FALL and crush the block
        // directly below it (gravity damage). % chance rises with level (guaranteed by L5). Combo-gated.
        if (g.HasCard("avalanche") && g.Combo.Count >= 8
            && g.Rng.NextDouble() < System.Math.Min(1.0, 0.25 + 0.15 * g.CardLevel("avalanche")))
        {
            var below = g.BlockAt(blk.Col, blk.Row + 1);
            if (below != null && !below.Indestructible)
            {
                int dmg = 3 + g.CardLevel("avalanche");
                g._log.Log(g.TickCount, "card", "avalanche", $"rubble crushes block={below.Id} for {dmg}");
                BlockDamage.DamageBlock(g, below, dmg, igniteSource: false, killMult: 0.5);
            }
        }

        // Keystone (rare): killing a load-bearing block (one with a stack directly ABOVE it) may crack the
        // support, dropping the unsupported column into the gap (gravity collapse). Position-gated.
        if (g.HasCard("keystone") && g.BlockAt(blk.Col, blk.Row - 1) != null
            && g.Rng.NextDouble() < System.Math.Min(1.0, 0.25 + 0.15 * g.CardLevel("keystone")))
        {
            g._log.Log(g.TickCount, "card", "keystone", $"load-bearing kill → column {blk.Col} collapses");
            GravitySystem.CollapseColumn(g, blk.Col);
        }

        // Domino (epic): when 3+ blocks die within 1 second, the NEXT death EXPLODES in an AoE — a chain
        // reaction reward for fast clears. The explosion itself doesn't feed the chain (re-entrancy guard).
        if (g.HasCard("domino") && !g._dominoExploding)
        {
            long now = g.TickCount;
            long window = (long)System.Math.Round(1.0 / g.Config.FixedDt); // 1 second
            g._dominoDeaths.Add(now);
            g._dominoDeaths.RemoveAll(t => now - t > window);
            if (g._dominoArmed)
            {
                g._dominoArmed = false;
                g._dominoExploding = true;
                int dmg = 3 + g.CardLevel("domino");
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
                g._log.Log(g.TickCount, "card", "domino", $"chain explosion at col={blk.Col} row={blk.Row} dmg={dmg}");
                for (int dc = -1; dc <= 1; dc++)
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        if (dc == 0 && dr == 0) continue;
                        var nb = g.BlockAt(blk.Col + dc, blk.Row + dr);
                        if (nb != null) BlockDamage.DamageBlock(g, nb, dmg, igniteSource: false, killMult: 0.5);
                    }
                g._dominoExploding = false;
                g._dominoDeaths.Clear(); // a detonation consumes the window — need 3 FRESH kills to re-arm
            }
            else if (g._dominoDeaths.Count >= 3)
                g._dominoArmed = true;
        }
    }

    // ── a ball bounced off a side wall (called from BallSystem after ResolveWalls) ──
    internal static void OnWallBounce(GameInstance g, Ball b)
    {
        if (!g.AnyCards) return;
        // Bank Shot: each wall carom before connecting banks more damage onto the next block hit.
        if (g.HasCard("bank_shot")) b.BankCharge++;

        // Ricochet (rare): a side-wall bounce has a % chance (rising per level) to fire a horizontal bolt
        // straight across the row, off the wall it just hit — a free sweep of that lane.
        if (g.HasCard("ricochet")
            && g.Rng.NextDouble() < System.Math.Min(0.6, 0.15 + 0.1 * g.CardLevel("ricochet")))
        {
            // After ResolveWalls, Vel.X points away from the wall → fire the bolt that way.
            int dir = b.Vel.X >= 0 ? 1 : -1;
            g.Projectiles.Add(new Projectile
            {
                Id               = g._nextProjId++,
                Pos              = b.Pos,
                Vel              = new Vec2(dir * 520, 0),
                Damage           = 1 + g.CardLevel("ricochet"),
                Radius           = g.Config.BallRadius * 0.5,
                Kind             = "ricochet",
                PiercingHitsLeft = g.Level.Grid.Cols, // crosses the whole row
            });
            g.RaiseEvent(SimEventKind.Lightning, b.Pos.X, b.Pos.Y);
            g._log.Log(g.TickCount, "card", "ricochet", $"bolt dir={dir} dmg={1 + g.CardLevel("ricochet")}");
        }
    }

    // ── the player just lost HP (called from CombatSystem / LavaSystem) ──
    internal static void OnHpLost(GameInstance g, int amount)
    {
        if (amount <= 0 || !g.AnyCards) return;
        // Martyr's Brand: getting hit grants a short vengeance buff (damage up). "must be hit; short buff."
        if (g.HasCard("martyrs_brand"))
        {
            double dur = 3.0 + g.CardLevel("martyrs_brand"); // seconds
            EffectSystem.Add(g, "martyr_brand", dur);
            g._log.Log(g.TickCount, "card", "martyrs_brand", $"vengeance buff {dur:0.0}s after losing {amount} HP");
        }
    }

    // ── a falling pickup was caught by the paddle (called from BonusSystem) ──
    internal static void OnBonusCaught(GameInstance g, Bonus bonus)
    {
        if (!g.AnyCards) return;
        // Sleight of Hand (rare): catching a pickup DEAD-CENTRE on the paddle duplicates it — a second,
        // identical pickup drops right above you. Precision-gated (must be a centre catch). A duplicate
        // cannot itself be re-duplicated (NoDuplicate), so it never chains into infinite copies.
        if (g.HasCard("sleight_of_hand") && !bonus.NoDuplicate)
        {
            double band = g.Paddle.Width * 0.18; // centre window (widens with the paddle)
            if (System.Math.Abs(bonus.Pos.X - g.Paddle.Center.X) <= band)
            {
                var dup = BonusSystem.SpawnWithType(g, g.Paddle.Center.X,
                    g.Paddle.Center.Y - g.Config.CellSize * 3, bonus.Type);
                dup.NoDuplicate = true;
                g._log.Log(g.TickCount, "card", "sleight_of_hand", $"centre-catch duplicated pickup {bonus.Type}");
            }
        }
    }

    // ── a ball was deflected by the paddle (called from SpellSystem.OnPaddleHit) ──
    internal static void OnPaddleHit(GameInstance g, Ball b, bool isPerfect)
    {
        if (!g.AnyCards) return;
        // Dead Center: a perfect deflect arms the next-block burst.
        if (g.HasCard("dead_center") && isPerfect) b.DeadCenterArmed = true;
        // Metronome: consecutive perfect deflects stack; a non-perfect deflect breaks the rhythm.
        if (g.HasCard("metronome"))
        {
            if (isPerfect) { g._metronomeStacks++; g._log.Log(g.TickCount, "card", "metronome", $"stacks={g._metronomeStacks}"); }
            else g._metronomeStacks = 0;
        }
        // Redline: touching the paddle resets the airborne timer (back to base speed/damage).
        if (g.HasCard("redline")) b.SincePaddle = 0;
    }

    // ── per-ball, per-tick (called from BallSystem.UpdateBallStep before collisions) ──
    internal static void OnBallTick(GameInstance g, Ball b, double dt)
    {
        if (!g.AnyCards) return;
        // Phase Window: a long combo (≥ threshold) opens a full-pierce window — the ball punches THROUGH
        // blocks (dealing damage, no bounce). The window closes when the combo breaks (any paddle touch),
        // since PhasesLeft is only refreshed while the combo is high. Higher levels open it sooner.
        if (g.HasCard("phase_window"))
        {
            int threshold = System.Math.Max(7, 15 - (g.CardLevel("phase_window") - 1) * 2);
            if (g.Combo.Count >= threshold) b.PhasesLeft = System.Math.Max(b.PhasesLeft, 2);
        }

        // Hot Hand: the ball GROWS as your combo passes each milestone (every 5 kills). It keeps its size
        // across paddle touches and only resets when a fresh ball is served. Bigger = wider coverage.
        if (g.HasCard("hot_hand"))
        {
            int milestone = g.Combo.Count / 5;
            if (milestone > b.HotHandMilestone)
            {
                b.HotHandMilestone = milestone;
                double cap  = g.Config.BallRadius * (1.5 + 0.1 * g.CardLevel("hot_hand"));
                b.Radius = System.Math.Min(cap, b.Radius + g.Config.BallRadius * 0.15);
                g._log.Log(g.TickCount, "card", "hot_hand", $"ball grows → r={b.Radius:0.0} (milestone {milestone})");
            }
        }

        // Redline: the longer aloft, the FASTER the ball flies (harder to catch — the tradeoff). Ramps up to
        // +40% speed; resets to base on the next paddle touch (OnPaddleHit).
        if (g.HasCard("redline"))
        {
            b.SincePaddle += dt;
            double speed = b.Vel.Length;
            if (speed > 1e-6)
            {
                double mult = 1.0 + System.Math.Min(0.40, b.SincePaddle * 0.08 * g.CardLevel("redline"));
                b.Vel = b.Vel.Normalized() * g.Config.BallSpeed * mult;
            }
        }
    }

    // ── mana-regen multiplier (called from SpellSystem.RegenMana) ──
    /// <summary>Channeling (rare): regen PAUSES (×0) while a ball is in flight up the board, and DOUBLES
    /// (×2) while the ball is cradled low near the paddle ("caught"). 1.0 when not equipped — a control-
    /// playstyle tradeoff: keep the ball low to bank mana, or send it flying and go dry.</summary>
    internal static double ChannelingRegenMult(GameInstance g)
    {
        if (!g.HasCard("channeling")) return 1.0;
        // "Caught" band = within ~3 cells above the paddle top (cradled/controlled low).
        double band = g.Config.CellSize * 3.0;
        double paddleTop = g.Paddle.Center.Y - g.Paddle.Height / 2;
        bool anyLow = false, anyAloft = false;
        foreach (var b in g.Balls)
        {
            if (!b.Alive) continue;
            if (b.Pos.Y >= paddleTop - band) anyLow = true; else anyAloft = true;
        }
        if (anyAloft) return 0.0;          // a ball is in flight → regen paused
        if (anyLow)   return 2.0;          // ball(s) cradled low → regen doubled
        return 1.0;                         // no live balls (e.g. serving) → neutral
    }

    // ── crit execute (called from BallSystem's crit branch, with the rolled crit damage) ──
    /// <summary>Executioner's Edge (rare): a CRIT against an already-low block (≤ threshold HP) executes for
    /// DOUBLE. Double-gated: it only fires on a crit AND a low-HP block. Higher levels raise the HP window so
    /// it triggers on more blocks. Returns the EXTRA damage to add to <paramref name="critDmg"/> (0 if N/A).</summary>
    internal static int ExecutionerExtra(GameInstance g, Block target, int critDmg)
    {
        if (!g.HasCard("executioners_edge")) return 0;
        double frac = 0.20 + 0.05 * g.CardLevel("executioners_edge"); // L1 = ≤25% HP
        if (target.Hp > target.MaxHp * frac) return 0;
        g._log.Log(g.TickCount, "card", "executioners_edge", $"execute block={target.Id} hp={target.Hp}/{target.MaxHp} +{critDmg}");
        return critDmg; // double the crit
    }

    // ── after a ball→block hit resolves (called from BallSystem after DamageBlock) ──
    /// <summary>Overkill (epic) + Erosion (mythic) — both key off the just-resolved ball→block hit.</summary>
    internal static void OnBlockHit(GameInstance g, Ball b, Block blk, int dmgDealt, int hpBefore)
    {
        if (!g.AnyCards) return;

        // Overkill: a hit overdamaging a block (> 2× its HP) carries the EXCESS to the block BEHIND it
        // (the one on the far side, in line with the ball). Only the surplus spills — no free full hits.
        if (g.HasCard("overkill") && !blk.Indestructible && hpBefore > 0 && dmgDealt > 2 * hpBefore)
        {
            int excess = dmgDealt - hpBefore;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            double bx = c.X - b.Pos.X, by = c.Y - b.Pos.Y; // toward the block = away from the ball = "behind"
            int dc = 0, dr = 0;
            if (System.Math.Abs(bx) >= System.Math.Abs(by)) dc = bx >= 0 ? 1 : -1;
            else dr = by >= 0 ? 1 : -1;
            var behind = g.BlockAt(blk.Col + dc, blk.Row + dr);
            if (behind != null && !behind.Indestructible)
            {
                g._log.Log(g.TickCount, "card", "overkill", $"excess {excess} → block={behind.Id}");
                BlockDamage.DamageBlock(g, behind, excess, igniteSource: false, killMult: 0.25);
            }
        }

        // Erosion: a hit on a plain INDESTRUCTIBLE wall slowly cracks it — a heavy commitment (~16 hits),
        // fewer at higher level. Excludes bosses/statues/special blocks (only Behavior None walls erode).
        if (g.HasCard("erosion") && blk.Indestructible && !blk.Boss
            && blk.Behavior == BlockBehavior.None && !blk.Dead)
        {
            blk.ErosionHits++;
            int threshold = System.Math.Max(4, 16 - (g.CardLevel("erosion") - 1) * 3);
            if (blk.ErosionHits >= threshold)
            {
                blk.Dead = true;
                g.InvalidateBlockGrid();
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                g.RaiseEvent(SimEventKind.BlockDestroyed, c.X, c.Y);
                g._log.Log(g.TickCount, "card", "erosion", $"cracked indestructible block={blk.Id} after {blk.ErosionHits} hits");
            }
        }
    }

    // ── helpers ──
    /// <summary>The lowest Row index (top of the field) among live destructible blocks, or -1 if none.</summary>
    private static int TopLiveRow(GameInstance g)
    {
        int top = int.MaxValue;
        foreach (var b in g.Blocks)
            if (!b.Dead && b.NeedToKill && !b.Indestructible && b.Row < top) top = b.Row;
        return top == int.MaxValue ? -1 : top;
    }

    /// <summary>The highest Row index (bottom of the field) among live destructible blocks, or -1 if none.</summary>
    private static int BottomLiveRow(GameInstance g)
    {
        int bot = -1;
        foreach (var b in g.Blocks)
            if (!b.Dead && b.NeedToKill && !b.Indestructible && b.Row > bot) bot = b.Row;
        return bot;
    }

    private static int LiveDestructibleCount(GameInstance g)
        => g.Blocks.Count(b => !b.Dead && b.NeedToKill && !b.Indestructible);
}
