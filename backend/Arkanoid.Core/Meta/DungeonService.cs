using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure-logic functions for dungeon run lifecycle.
/// No file I/O, no HTTP — all mutation happens on a <see cref="DungeonRun"/> POCO.
/// </summary>
public static class DungeonService
{
    // The bonus pool: relic ids + ball-core ids available as floor-clear choices.
    // The G2 relic web (docs/09): standalone-good, build-enablers, tradeoffs, and
    // biome-conditional picks — variance per the docs/04 §7 choice rules.
    private static readonly string[] RelicPool    =
    {
        "glass_cannon", "flint_core", "pyroclasm", "mana_battery",
        "conductor", "overcharge", "split_shot", "souljar", "lodestone",
        "ember_heart", "second_wind", "midas", "lead_paddle",
        "sapper", "hellwalker", "ghost_lens", "pillar_doctrine",
    };
    private static readonly string[] BallCorePool = { "heavy", "split", "ember", "ghost", "echo", "frost" };

    /// <summary>
    /// Creates a new active run from the given dungeon definition.
    /// FloorIndex starts at 0; no pending choices yet.
    /// </summary>
    public static DungeonRun StartRun(DungeonDef def, int seed)
    {
        return new DungeonRun
        {
            DungeonId      = def.Id,
            Floors         = new List<string>(def.Floors),
            FloorIndex     = 0,
            Relics         = new List<string>(),
            BallCores      = new List<string>(),
            PendingChoices = new List<string>(),
            Active         = true,
            Cleared        = false,
            Seed           = seed,
        };
    }

    /// <summary>
    /// Called when the player clears the current floor.
    /// Returns <c>true</c> if this was the FINAL floor (run is now cleared).
    /// On a non-final floor, populates <see cref="DungeonRun.PendingChoices"/> with 3 distinct options;
    /// the run does NOT advance until <see cref="PickChoice"/> is called.
    /// <paramref name="cfg"/> is accepted for signature compatibility but not required for logic.
    /// </summary>
    public static bool OnFloorCleared(DungeonRun run, ProgressionConfig? cfg = null)
    {
        if (!run.Active) return false;

        bool isLastFloor = run.FloorIndex == run.Floors.Count - 1;

        if (isLastFloor)
        {
            run.Cleared = true;
            run.Active  = false;
            return true;
        }

        // Generate 3 distinct choices using a seeded RNG derived from run.Seed + FloorIndex.
        run.PendingChoices = GenerateChoices(run, 3);
        return false;
    }

    /// <summary>
    /// The player picks one of the 3 pending choices.
    /// Adds it to <see cref="DungeonRun.Relics"/> or <see cref="DungeonRun.BallCores"/>,
    /// clears PendingChoices, and advances FloorIndex.
    /// No-ops if choiceId is not in PendingChoices.
    /// </summary>
    public static void PickChoice(DungeonRun run, string choiceId)
    {
        if (!run.PendingChoices.Contains(choiceId)) return;

        if (IsRelic(choiceId))
            run.Relics.Add(choiceId);
        else
            run.BallCores.Add(choiceId);

        run.PendingChoices.Clear();
        run.FloorIndex++;
    }

    /// <summary>Permadeath: the run ends without clearing. All buffs are lost.</summary>
    public static void Fail(DungeonRun run)
    {
        run.Active  = false;
        run.Cleared = false;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static bool IsRelic(string id) => System.Array.IndexOf(RelicPool, id) >= 0;

    private static List<string> GenerateChoices(DungeonRun run, int count)
    {
        // Combined pool minus already-owned relics/ball-cores when possible.
        var pool = new List<string>();
        foreach (var id in RelicPool)
            if (!run.Relics.Contains(id)) pool.Add(id);
        foreach (var id in BallCorePool)
            if (!run.BallCores.Contains(id)) pool.Add(id);

        // If pool is smaller than count, fall back to full pool (repeats allowed as last resort).
        if (pool.Count < count)
        {
            pool.Clear();
            pool.AddRange(RelicPool);
            pool.AddRange(BallCorePool);
        }

        // Deterministic RNG seeded by run.Seed XOR'd with FloorIndex so each floor is distinct.
        var rng = new Rng(run.Seed ^ (run.FloorIndex * unchecked((int)0x9e3779b9)));
        var choices = new List<string>(count);
        var available = new List<string>(pool);

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int idx = (int)(rng.NextDouble() * available.Count);
            idx = System.Math.Clamp(idx, 0, available.Count - 1);
            choices.Add(available[idx]);
            available.RemoveAt(idx);
        }

        return choices;
    }
}
