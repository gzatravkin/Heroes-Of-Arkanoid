using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Shared block-damage and fire-spread logic.  Called by BallSystem, SpellSystem, and
/// any other system that destroys blocks — single implementation, no duplication.
/// </summary>
internal static class BlockDamage
{
    /// <summary>§3 Rot &amp; Collapse: max-HP a rot hit strips from a block (permanent withering).</summary>
    private const int RotMaxHpLoss = 2;

    /// <summary>
    /// Apply <paramref name="dmg"/> to <paramref name="blk"/>.  If the block dies and
    /// <paramref name="igniteSource"/> is true, fire may spread to neighbours.
    /// If <paramref name="decaySource"/> is true, necromancer decay spread applies instead.
    /// </summary>
    /// <param name="killMult">Mana fraction granted on kill: 1.0 = direct ball kill (full), 0.5 = spell/minion kill, 0.25 = chain/explosion kill.</param>
    internal static void DamageBlock(GameInstance g, Block blk, int dmg, bool igniteSource, bool decaySource = false, double killMult = 1.0)
    {
        // Fireshot power-up: plain-wall indestructible blocks (Behavior == None, not Boss)
        // become destructible while the effect is active.
        bool fireshotBreakable = g.FireshotActive && blk.Indestructible
            && blk.Behavior == BlockBehavior.None && !blk.Boss;
        if (blk.Indestructible && !fireshotBreakable) return;
        if (blk.ImmunityTimer > 0) { g.RaiseEvent(SimEventKind.ShieldBlock, 0, 0); return; } // shielded: immune
        // §3 Rot & Collapse: a rot hit permanently lowers the block's MAX HP (it withers).
        if (decaySource && blk.MaxHp > 1)
        {
            blk.MaxHp = System.Math.Max(1, blk.MaxHp - RotMaxHpLoss);
            if (blk.Hp > blk.MaxHp) blk.Hp = blk.MaxHp;
        }
        blk.Hp -= dmg;
        g.MarkBlocksDirty();
        bool killed = blk.Hp <= 0 && !blk.Dead;
        // Fire-Mage ignite: lighting blocks afire is the spread mechanism (over time, not instant).
        // A surviving block catches fire (gen 0); a killed block seeds its neighbours (gen 1).
        if (igniteSource && Modifiers.ShouldSpreadFire(g))
        {
            if (killed) BurnSystem.LightNeighbours(g, blk, 1);
            else        BurnSystem.LightBlock(g, blk, 0);
        }
        if (killed)
        {
            blk.Dead = true;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            g.RaiseEvent(SimEventKind.BlockDestroyed, c.X, c.Y);
            ApplyKillEconomy(g, killMult);
            // §5.5 Fire Mage ★5: a CRIT kill ignites a nearby block (crit↔ignite synergy).
            if (g.LastHitWasCrit && g.HasPerk(Meta.StatResolver.FmCritKillIgnite))
            {
                BurnSystem.LightNeighbours(g, blk, 1);
                g._log.Log(g.TickCount, "perk", "fm_s5 crit-kill ignite", $"block={blk.Id}");
            }
            // §5.5 Necromancer ★5: a full-combo kill may raise a helper-ball.
            if (g.HasPerk(Meta.StatResolver.NecroComboHelper)
                && g.Combo.Multiplier >= 4 && g.Rng.NextDouble() < 0.15)
                g.SpawnHelperBall();
            // §3 Ashfall: while armed, an IGNITE-kill (a burning block destroyed) rains an ember down its column.
            if (g._ashfallTimer > 0 && blk.BurnRemaining > 0)
                AshfallSystem.RainEmber(g, blk);
            if (blk.ForcedDropEffect != null)
                BonusSystem.TrySpawnTypedBonus(g, c.X, c.Y, blk.ForcedDropEffect);
            else if (blk.Behavior != BlockBehavior.None && !blk.Boss)
                BonusSystem.SpawnGuaranteed(g, c.X, c.Y);
            else
                BonusSystem.TrySpawnBonus(g, c.X, c.Y);
            if (decaySource)
            {
                SpreadDecay(g, blk);
                GravitySystem.CollapseColumn(g, blk.Col); // §3 Rot & Collapse: the column above falls in
            }
            if (blk.Bomb)
                Explode(g, blk);
            if (blk.UnionGroup > 0)
                CollapseUnion(g, blk);
            if (blk.Vase)
                BallSystem.LevelUpStatues(g); // risk/reward: statues get stronger, but pay more
            // Cauldron refund: everything it siphoned comes back (docs/11 Economy axis).
            if (blk.Cauldron && blk.StoredMana > 0)
            {
                g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + blk.StoredMana);
                g.RaiseEvent(SimEventKind.ManaRefund, c.X, c.Y);
            }
            // Lava spawner death: its crept lava retracts (the counterplay).
            if (blk.LavaSpawner)
                LavaSystem.RetractLava(g, blk);
            RelicSystem.OnBlockDestroyed(g, c);
            CardSystem.OnBlockDestroyed(g, blk, g.LastHitWasCrit); // §1 Cards: on-kill triggers (Opening Gambit, …)
            ModuleSystem.OnBlockDestroyed(g, blk); // §2 Pressure Cooker: kills push the field back up
            // Vase reward side: a levelled statue pays bonus mana on death.
            if (blk.IsStatue && blk.StatueLevel > 0)
                g.ManaValue = System.Math.Min(g.ManaMaxValue,
                    g.ManaValue + blk.StatueLevel * g.Config.Enemies.VaseKillManaPerLevel);
            ReviverSystem.OnBlockDestroyed(g, blk);
        }
    }

    /// Mana gain, combo advance, crystal payout, and speed escalation on every block kill.
    private static void ApplyKillEconomy(GameInstance g, double killMult = 1.0)
    {
        g.ManaValue = System.Math.Min(g.ManaMaxValue, g.ManaValue + Modifiers.KillManaGain(g, killMult));
        // §5.5 Necromancer ★1: heal 1 HP per 60 kills (up to the run's max HP).
        if (g.HasPerk(Meta.StatResolver.NecroHeal) && ++g._perkKillCounter >= 60)
        {
            g._perkKillCounter = 0;
            if (g.StatMaxHp <= 0 || g.Hp < g.StatMaxHp) g.SetHp(g.Hp + 1);
        }
        g.Combo.BricksDestroyed++;
        g.Combo.Count++;
        g.Combo.Multiplier = System.Math.Min(4, 1 + g.Combo.Count / 3);
        g.Crystals += ModuleSystem.KillCrystals(g, g.Combo.Multiplier); // §2 Toll Roads gates kill gold to crit/perfect
        // Ball speed is now driven by the time-based ramp (GameInstance tick step), not brick count.
    }

    /// <summary>
    /// Bomb block detonation: damage every block within ExplodeRadius cells (Chebyshev),
    /// chaining into other bombs the same frame (a freshly-killed bomb re-enters DamageBlock).
    /// </summary>
    internal static void Explode(GameInstance g, Block origin)
    {
        int radius = origin.ExplodeRadius > 0 ? origin.ExplodeRadius : 1;
        if (Modifiers.HasSapper(g)) radius += Modifiers.SapperRadiusBonus(g); // caverns-keyed relic
        var c = g.Level.Grid.CellCenter(origin.Col, origin.Row);
        g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
        // BlockAt() returns null for already-dead blocks, so chains self-filter without a snapshot.
        for (int dc = -radius; dc <= radius; dc++)
            for (int dr = -radius; dr <= radius; dr++)
            {
                if (dc == 0 && dr == 0) continue;
                var nb = g.BlockAt(origin.Col + dc, origin.Row + dr);
                if (nb == null) continue;
                DamageBlock(g, nb, g.Config.Enemies.BombDamage, igniteSource: false, killMult: 0.25);
            }
    }

    /// <summary>
    /// Overload Charge detonation: kill the charged block (if alive) then chain-damage all blocks within
    /// <paramref name="radius"/> cells. Called from SpellSystem.UpdateKitSpells after the 0.5 s delay.
    /// </summary>
    internal static void ExplodeOverload(GameInstance g, int col, int row, int radius)
    {
        if (col < 0 || row < 0) return;
        var c = g.Level.Grid.CellCenter(col, row);
        g.RaiseEvent(SimEventKind.Explosion, c.X, c.Y);
        var origin = g.BlockAt(col, row);
        if (origin != null) DamageBlock(g, origin, origin.MaxHp, igniteSource: false, killMult: 0.25);
        for (int dc = -radius; dc <= radius; dc++)
            for (int dr = -radius; dr <= radius; dr++)
            {
                if (dc == 0 && dr == 0) continue;
                var nb = g.BlockAt(col + dc, row + dr);
                if (nb == null) continue;
                DamageBlock(g, nb, g.Config.Enemies.BombDamage, igniteSource: false, killMult: 0.25);
            }
    }

    /// <summary>
    /// Caverns union-of-sticks: when one block of a connected bridge dies, the rest collapse with it.
    /// Members are killed via DamageBlock so each gets its full death FX/economy; already-dead members
    /// self-filter, so a member's own collapse call doesn't recurse endlessly.
    /// </summary>
    internal static void CollapseUnion(GameInstance g, Block origin)
    {
        foreach (var b in g.Blocks)
        {
            if (b.Dead || b.Id == origin.Id || b.UnionGroup != origin.UnionGroup) continue;
            DamageBlock(g, b, b.Hp, igniteSource: false, killMult: 0.25); // lethal — collapse the rest of the bridge
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
                DamageBlock(g, nb, chip, igniteSource: false, decaySource: false, killMult: 0.25);
            }
    }

}
