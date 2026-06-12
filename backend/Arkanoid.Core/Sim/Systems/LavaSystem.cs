using System.Linq;
using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Sim.Systems;

/// <summary>
/// Hell Lava Spawner (docs/11 lava reform): on a cadence each spawner creeps one new
/// lava block into an adjacent empty cell (soft timer pressure — the Tempo half of
/// Hell). Counterplay: kill the spawner — all lava it crept RETRACTS (dies with it).
/// Plain authored `hell_lava` blocks (OwnerId 0) are untouched by retraction.
/// </summary>
internal static class LavaSystem
{
    internal static void Update(GameInstance g, double dt)
    {
        // Lava in the paddle danger zone drains player HP over time.
        CheckDangerZone(g, dt);

        foreach (var blk in g.Blocks.Where(b => !b.Dead && b.LavaSpawner).ToList())
        {
            // Lava only flows after the spawner takes its first hit (hp < maxHp).
            if (blk.Hp >= blk.MaxHp) continue;

            blk.EmitAccumulator += dt;
            if (blk.EmitAccumulator < g.Config.Enemies.LavaCreepInterval) continue;
            blk.EmitAccumulator -= g.Config.Enemies.LavaCreepInterval;
            if (blk.SpawnedCount >= g.Config.Enemies.LavaCreepMax) continue;
            Creep(g, blk);
        }
    }

    private static void CheckDangerZone(GameInstance g, double dt)
    {
        int dangerRow = g.Level.Grid.Rows - 2;
        bool lavaInDanger = g.Blocks.Any(b => !b.Dead && b.Lava && b.Row >= dangerRow);
        if (!lavaInDanger) { g._lavaDrainAccumulator = 0; return; }

        g._lavaDrainAccumulator += dt;
        if (g._lavaDrainAccumulator < g.Config.Enemies.LavaDrainInterval) return;
        g._lavaDrainAccumulator -= g.Config.Enemies.LavaDrainInterval;

        g.Hp = System.Math.Max(0, g.Hp - 1);
        g.RaiseEvent(SimEventKind.LavaDrain, 0, 0);
        g._log.Log(g.TickCount, "lava", "hp drained by lava in danger zone", $"hp={g.Hp}");
    }

    private static void Creep(GameInstance g, Block spawner)
    {
        // Creep frontier: the spawner plus every lava cell it already owns.
        var frontier = new List<Block> { spawner };
        frontier.AddRange(g.Blocks.Where(b => !b.Dead && b.Lava && b.OwnerId == spawner.Id));

        // Sideways-first: lava pools outward around the spawner instead of building a
        // column straight down into the paddle zone (fairness over realism).
        (int dc, int dr)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var src in frontier)
        {
            foreach (var (dc, dr) in dirs)
            {
                int col = src.Col + dc, row = src.Row + dr;
                if (col < 0 || row < 0 || col >= g.Level.Grid.Cols || row >= g.Level.Grid.Rows) continue;
                if (g.BlockAt(col, row) != null) continue;

                var lava = new Block
                {
                    Id = g.NextBlockId(), Col = col, Row = row,
                    Hp = 1, MaxHp = 1, TypeId = "hell_lava",
                    Sprite = "LavaMainPart", NeedToKill = false,
                    Indestructible = true, Behavior = BlockBehavior.Lava,
                };
                lava.OwnerId = spawner.Id;
                g.Blocks.Add(lava);
                spawner.SpawnedCount++;
                var c = g.Level.Grid.CellCenter(col, row);
                g.RaiseEvent(SimEventKind.LavaCreep, c.X, c.Y);
                g._log.Log(g.TickCount, "lava", "crept", $"spawner={spawner.Id} cell=({col},{row})");
                return; // one cell per cadence
            }
        }
    }

    /// <summary>Called from BlockDamage when a spawner dies: its crept lava retracts.</summary>
    internal static void RetractLava(GameInstance g, Block spawner)
    {
        int n = 0;
        foreach (var b in g.Blocks)
            if (!b.Dead && b.Lava && b.OwnerId == spawner.Id) { b.Dead = true; n++; }
        if (n > 0)
        {
            g.RaiseEvent(SimEventKind.LavaRetract, 0, 0);
            g._log.Log(g.TickCount, "lava", "retracted", $"spawner={spawner.Id} cells={n}");
        }
    }
}
