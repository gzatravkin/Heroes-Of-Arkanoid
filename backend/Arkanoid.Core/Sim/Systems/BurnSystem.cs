using Arkanoid.Core.Entities;
using System.Collections.Generic;
using System.Linq;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Fire-Mage ignite burn (owner redesign 2026-06-16): a SLOW, LIMITED damage-over-time. An ignite-imbued
/// deflect LIGHTS a block (gen 0) — it does no direct damage; the block then burns 1 dmg every
/// FireConfig.BurnInterval (~7s) and the fire CREEPS one block further every FireConfig.SpreadInterval,
/// forming a chain capped to (SpreadBlocksBase + spell level) blocks per seed. Each block's total burn
/// damage is capped at min(BurnDamageCap, BurnDamageBase + spell level). Identity: "set the board
/// smouldering, then survive while it slowly clears" — NOT an instant board-clear.
/// </summary>
internal static class BurnSystem
{
    private static readonly (int dc, int dr)[] Cardinal  = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    private static readonly (int dc, int dr)[] Diagonals = { (1, 1), (1, -1), (-1, 1), (-1, -1) };

    /// <summary>Max spread generation reachable from a seed (so total chain = SpreadBlocksBase + spell level).</summary>
    internal static int SpreadGenCap(GameInstance g)
        => System.Math.Max(0, g.Config.Fire.SpreadBlocksBase + g.SpellLevel("ignite") - 1);

    /// <summary>How many 1-damage burn ticks a single block suffers before the fire caps out.</summary>
    internal static int BurnTicks(GameInstance g)
        => System.Math.Min(g.Config.Fire.BurnDamageCap, g.Config.Fire.BurnDamageBase + g.SpellLevel("ignite"));

    /// <summary>Set a block alight at the given spread generation (no-op if dead/indestructible/already burning).
    /// <paramref name="duration"/> &lt; 0 uses the spell-level burn budget; callers (e.g. the firewall) can pass a
    /// shorter explicit burn. <paramref name="noSpread"/> lights it without seeding the creep chain.</summary>
    internal static void LightBlock(GameInstance g, Block blk, int gen, double duration = -1, bool noSpread = false)
    {
        if (blk.Dead || blk.Indestructible || blk.BurnRemaining > 0) return;
        // +0.5 interval cushion so exactly BurnTicks damage ticks land before the burn expires
        // (avoids the final tick racing the float countdown to zero on the same frame).
        blk.BurnRemaining   = duration >= 0 ? duration : g.Config.Fire.BurnInterval * (BurnTicks(g) + 0.5);
        blk.BurnGen         = gen;
        blk.BurnAccum       = 0;
        blk.BurnSpreadAccum = 0;
        blk.BurnSpawned     = noSpread; // a non-spreading light is treated as "already spawned its child"
        g.MarkBlocksDirty(); // surface the new burning state to the snapshot immediately
    }

    /// <summary>Light all cardinal (and, under pyroclasm, diagonal) neighbours of a block. Used by ★ perks.</summary>
    internal static void LightNeighbours(GameInstance g, Block origin, int gen)
    {
        var dirs = Modifiers.SpreadIncludesDiagonals(g)
            ? Cardinal.Concat(Diagonals)
            : (IEnumerable<(int, int)>)Cardinal;
        foreach (var (dc, dr) in dirs)
        {
            var nb = g.BlockAt(origin.Col + dc, origin.Row + dr);
            if (nb != null) LightBlock(g, nb, gen);
        }
    }

    internal static void Update(GameInstance g, double dt)
    {
        var burning = g.Blocks.Where(b => !b.Dead && b.BurnRemaining > 0).ToList();
        if (burning.Count == 0) return;

        double burnIv   = g.Config.Fire.BurnInterval;
        double spreadIv  = g.Config.Fire.SpreadInterval;
        int    damage   = Modifiers.BurnDamage(g);
        int    genCap   = SpreadGenCap(g);
        bool   canSpread = Modifiers.ShouldSpreadFire(g);
        var    toLight  = new List<(Block nb, int gen)>();

        foreach (var blk in burning)
        {
            // --- slow fire creep: light ONE not-yet-burning neighbour, once, after SpreadInterval ---
            if (canSpread && !blk.BurnSpawned && blk.BurnGen < genCap)
            {
                blk.BurnSpreadAccum += dt;
                if (blk.BurnSpreadAccum >= spreadIv)
                {
                    var dirs = Modifiers.SpreadIncludesDiagonals(g)
                        ? Cardinal.Concat(Diagonals)
                        : (IEnumerable<(int, int)>)Cardinal;
                    foreach (var (dc, dr) in dirs)
                    {
                        var nb = g.BlockAt(blk.Col + dc, blk.Row + dr);
                        if (nb != null && nb.BurnRemaining <= 0 && !nb.Dead && !nb.Indestructible)
                        {
                            toLight.Add((nb, blk.BurnGen + 1));
                            blk.BurnSpawned = true; // exactly one child per block → a creeping chain, not a burst
                            break;
                        }
                    }
                }
            }

            // --- slow DoT: 1 damage every BurnInterval ---
            blk.BurnAccum += dt;
            while (blk.BurnAccum >= burnIv && !blk.Dead)
            {
                blk.BurnAccum -= burnIv;
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                g.RaiseEvent(SimEventKind.Burn, c.X, c.Y);
                BlockDamage.DamageBlock(g, blk, damage, igniteSource: false, killMult: 0.5);
            }

            blk.BurnRemaining -= dt;
            if (blk.BurnRemaining <= 0)
            {
                blk.BurnRemaining = 0; blk.BurnGen = 0; blk.BurnSpawned = false; blk.BurnSpreadAccum = 0;
                g.MarkBlocksDirty();
            }
        }

        foreach (var (nb, gen) in toLight) LightBlock(g, nb, gen);
    }
}
