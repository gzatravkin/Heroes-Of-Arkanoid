using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Shared block-damage and fire-spread logic.  Called by BallSystem, SpellSystem, and
/// any other system that destroys blocks — single implementation, no duplication.
/// </summary>
internal static class BlockDamage
{
    /// <summary>
    /// Apply <paramref name="dmg"/> to <paramref name="blk"/>.  If the block dies and
    /// <paramref name="igniteSource"/> is true, fire may spread to neighbours.
    /// If <paramref name="decaySource"/> is true, necromancer decay spread applies instead.
    /// </summary>
    internal static void DamageBlock(GameInstance g, Block blk, int dmg, bool igniteSource, bool decaySource = false)
    {
        // Fireshot power-up: plain-wall indestructible blocks (Behavior == None, not Boss)
        // become destructible while the effect is active.
        bool fireshotBreakable = g.Powerups.FireshotActive && blk.Indestructible
            && blk.Behavior == BlockBehavior.None && !blk.Boss;
        if (blk.Indestructible && !fireshotBreakable) return;
        if (blk.ImmunityTimer > 0) { g.RaiseEvent(SimEventKind.ShieldBlock, 0, 0); return; } // shielded: immune
        blk.Hp -= dmg;
        g.MarkBlocksDirty();
        g._log.Log(g.TickCount, "block", blk.Hp <= 0 ? "destroyed" : "hit",
                   $"id={blk.Id} col={blk.Col} row={blk.Row} hp={blk.Hp} dmg={dmg} ignite={igniteSource} decay={decaySource}");
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.BlockDestroyed, c.X, c.Y);
            ApplyKillEconomy(g);
            if (blk.ForcedDropEffect != null)
                BonusSystem.TrySpawnTypedBonus(g, c.X, c.Y, blk.ForcedDropEffect);
            else if (blk.Behavior != BlockBehavior.None && !blk.Boss)
                BonusSystem.SpawnGuaranteed(g, c.X, c.Y);
            else
                BonusSystem.TrySpawnBonus(g, c.X, c.Y);
            if (igniteSource && Modifiers.ShouldSpreadFire(g))
                SpreadFire(g, blk);
            if (decaySource)
                SpreadDecay(g, blk);
            if (blk.Bomb)
                Explode(g, blk);
            if (blk.Vase)
                BallSystem.LevelUpStatues(g); // risk/reward: statues get stronger, but pay more
            // Cauldron refund: everything it siphoned comes back (docs/11 Economy axis).
            if (blk.Cauldron && blk.StoredMana > 0)
            {
                g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + blk.StoredMana);
                g.RaiseEvent(SimEventKind.ManaRefund, c.X, c.Y);
                g._log.Log(g.TickCount, "cauldron", "refunded", $"mana={blk.StoredMana:F0}");
            }
            // Lava spawner death: its crept lava retracts (the counterplay).
            if (blk.LavaSpawner)
                LavaSystem.RetractLava(g, blk);
            RelicSystem.OnBlockDestroyed(g, c);
            // Vase reward side: a levelled statue pays bonus mana on death.
            if (blk.IsStatue && blk.StatueLevel > 0)
                g.ManaValue = System.Math.Min(g.ManaMaxValue,
                    g.ManaValue + blk.StatueLevel * g.Config.Enemies.VaseKillManaPerLevel);
            ReviverSystem.OnBlockDestroyed(g, blk);
        }
    }

    /// Mana gain, combo advance, crystal payout, and speed escalation on every block kill.
    private static void ApplyKillEconomy(GameInstance g)
    {
        g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + Modifiers.KillManaGain(g));
        g.Combo.BricksDestroyed++;
        g.Combo.Count++;
        g.Combo.Multiplier = System.Math.Min(4, 1 + g.Combo.Count / 3);
        g.Crystals += g.Combo.Multiplier;
        var speedMult = System.Math.Min(1.4, 1.0 + System.Math.Floor(g.Combo.BricksDestroyed / 20.0) * 0.05);
        foreach (var ball in g.Balls)
        {
            if (!ball.Alive || ball.Vel.Length < 1e-6) continue;
            var targetSpeed = g.Config.BallSpeed * speedMult;
            if (System.Math.Abs(ball.Vel.Length - targetSpeed) > 0.001)
                ball.Vel = ball.Vel.Normalized() * targetSpeed;
        }
    }

    /// <summary>
    /// Bomb block detonation: damage every block within ExplodeRadius cells (Chebyshev),
    /// chaining into other bombs the same frame (a freshly-killed bomb re-enters DamageBlock).
    /// </summary>
    internal static void Explode(GameInstance g, Block origin)
    {
        int radius = origin.ExplodeRadius > 0 ? origin.ExplodeRadius : 1;
        if (g.HasRelic("sapper")) radius += g.Config.SapperRadiusBonus; // caverns-keyed relic
        var c = g.Level.Grid.CellCenter(origin.Col, origin.Row);
        g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
        g._log.Log(g.TickCount, "bomb", "exploded", $"id={origin.Id} radius={radius}");
        // BlockAt() returns null for already-dead blocks, so chains self-filter without a snapshot.
        for (int dc = -radius; dc <= radius; dc++)
            for (int dr = -radius; dr <= radius; dr++)
            {
                if (dc == 0 && dr == 0) continue;
                var nb = g.BlockAt(origin.Col + dc, origin.Row + dr);
                if (nb == null) continue;
                DamageBlock(g, nb, g.Config.Enemies.BombDamage, igniteSource: false);
            }
    }

    /// <summary>
    /// Spread necromancer decay from a killed block to neighbours within
    /// Manhattan distance ≤ DecaySpreadRange, chipping each by DecaySpreadChip.
    /// Routes kills through DamageBlock so combo, crystals, and relic counters fire correctly.
    /// </summary>
    internal static void SpreadDecay(GameInstance g, Block origin)
    {
        int range = g.Config.DecaySpreadRange;
        int chip  = g.Config.DecaySpreadChip;
        for (int dc = -range; dc <= range; dc++)
            for (int dr = -range; dr <= range; dr++)
            {
                if (dc == 0 && dr == 0) continue;
                if (System.Math.Abs(dc) + System.Math.Abs(dr) > range) continue;
                var nb = g.BlockAt(origin.Col + dc, origin.Row + dr);
                if (nb == null) continue;
                var c = g.Level.Grid.CellCenter(nb.Col, nb.Row);
                g.RaiseEvent(SimEventKind.Decay, c.X, c.Y);
                DamageBlock(g, nb, chip, igniteSource: false, decaySource: false);
            }
    }

    /// <summary>Spread fire from a just-destroyed ignited block to its neighbours.
    /// Routes kills through DamageBlock so combo, crystals, and relic counters fire correctly.
    /// igniteSource:false stops chaining — spread kills do not themselves spread.</summary>
    internal static void SpreadFire(GameInstance g, Block origin)
    {
        var chip = Modifiers.SpreadChip(g);
        (int dc, int dr)[] cardinal  = { (1,0), (-1,0), (0,1), (0,-1) };
        (int dc, int dr)[] diagonals = { (1,1), (1,-1), (-1,1), (-1,-1) };
        var neighbors = Modifiers.SpreadIncludesDiagonals(g)
            ? cardinal.Concat(diagonals)
            : (IEnumerable<(int, int)>)cardinal;
        foreach (var (dc, dr) in neighbors)
        {
            var nb = g.BlockAt(origin.Col + dc, origin.Row + dr);
            if (nb == null) continue;
            var c = g.Level.Grid.CellCenter(nb.Col, nb.Row);
            g.RaiseEvent(SimEventKind.Burn, c.X, c.Y);
            DamageBlock(g, nb, chip, igniteSource: false, decaySource: false);
        }
    }
}
