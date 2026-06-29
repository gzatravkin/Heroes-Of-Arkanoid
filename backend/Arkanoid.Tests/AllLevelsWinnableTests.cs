using System.IO;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Verifies every production level can be won by a perfect-tracking AI with chip-assist:
///
///   1. Paddle snaps to the lowest alive ball's X every tick (never drains).
///   2. Lives = 9999, SpareBalls = 99 (tests reachability, not hazard-dodge skill).
///   3. If no NeedToKill block dies for 5 sim-seconds the AI calls chipBlocks(1) —
///      one point of damage to every destructible block, simulating optimal spell use.
///      This breaks necromant revival loops, teleporter deadlocks, and tight clusters
///      that the ball can't reach on its own.
///   4. For time-limited levels the stuck threshold is tightened to 2s so spell usage
///      is aggressive enough to beat the clock.
///
/// Output per test: level-id, final phase, sim-seconds, drain count, chip-assist count.
/// chip-assists > 0 means the level required spell help to clear (design note, not a bug).
///
/// Expected wall-clock runtime: under 2 seconds for all levels combined.
/// </summary>
public class AllLevelsWinnableTests
{
    private readonly ITestOutputHelper _out;
    public AllLevelsWinnableTests(ITestOutputHelper output) => _out = output;

    // ── Config ────────────────────────────────────────────────────────────────

    private static string FindConfigRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "config", "blocks.json")))
                return Path.Combine(dir, "config");
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Cannot find config/blocks.json — run tests from within the repo.");
    }

    // ── Level discovery ───────────────────────────────────────────────────────

    // hell-teleport / village-ghost are legacy non-campaign levels kept only as dungeon/rift test floors.
    private static readonly HashSet<string> SkipList = new() { "test-editor-auto", "hell-winnable", "hell-teleport", "village-ghost" };

    public static IEnumerable<object[]> AllLevelIds()
    {
        var levelsDir = Path.Combine(FindConfigRoot(), "levels");
        foreach (var path in Directory.GetFiles(levelsDir, "*.json").OrderBy(p => p))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            if (!SkipList.Contains(id))
                yield return new object[] { id };
        }
    }

    // ── Test ─────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllLevelIds))]
    public void Level_IsWinnableWithPerfectPlay(string levelId)
    {
        var configRoot = FindConfigRoot();
        var catalog    = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
        var level      = LevelLoader.FromFile(
                             Path.Combine(configRoot, "levels", $"{levelId}.json"), catalog);
        var relics     = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
        var bonusPath  = Path.Combine(configRoot, "bonuses.json");
        var bonuses    = File.Exists(bonusPath) ? BonusCatalog.FromFile(bonusPath) : null;

        var g = new GameInstance(level, SimConfig.Default, seed: 1, relics: relics, bonuses: bonuses);
        g.ApplyCheat("setLives", 9999);  // invincible to hazard damage — tests reachability not dodge
        g.ApplyCheat("setBalls", 99);    // enough spares to never hard-lose on a drain

        var (phase, simSecs, drains, chips) = RunPerfectPlay(g);

        _out.WriteLine(
            $"{levelId,-30}  {phase,-8}  {simSecs,6:F1}s  {drains,2} drain(s)  {chips,2} chip(s)");

        Assert.True(phase == GamePhase.Won,
            $"{levelId}: expected Won but got {phase} after {simSecs:F1}s " +
            $"({drains} drain(s), {chips} chip-assist(s))");
    }

    // ── AI ────────────────────────────────────────────────────────────────────

    private static (GamePhase Phase, double SimSeconds, int Drains, int Chips) RunPerfectPlay(GameInstance g)
    {
        const int MaxTicks = 60 * 600; // hard cap: 600 sim-seconds
        var dt = g.Config.FixedDt;

        // Tight chip threshold for time-limited levels so spell use beats the clock.
        double stuckSecs = g.Level.TimeLimit > 0 ? 2.0 : 5.0;

        int drains        = 0;
        int chips         = 0;
        int prevSpares    = g.SpareBalls;
        int prevNtk       = g.Blocks.Count(b => !b.Dead && b.NeedToKill);
        double lastProgress = 0.0;

        for (int i = 0; i < MaxTicks; i++)
        {
            if (g.Phase == GamePhase.Serving)
                g.Serve();

            if (g.Phase == GamePhase.Playing)
            {
                // Snap paddle to the lowest alive ball (closest to the drain line).
                var ball = g.Balls
                    .Where(b => b.Alive)
                    .OrderByDescending(b => b.Pos.Y)
                    .FirstOrDefault();
                if (ball != null)
                    g.SetPaddleX(ball.Pos.X);

                g.Tick(dt);

                // Track drains.
                if (g.SpareBalls < prevSpares)
                {
                    drains += prevSpares - g.SpareBalls;
                    prevSpares = g.SpareBalls;
                }

                // Chip-assist: if NeedToKill count hasn't dropped in stuckSecs, deal
                // 1 damage to every destructible block (simulates perfect spell use).
                int ntk = g.Blocks.Count(b => !b.Dead && b.NeedToKill);
                if (ntk < prevNtk)
                {
                    prevNtk = ntk;
                    lastProgress = g.ElapsedPlayTime;
                }
                else if (g.ElapsedPlayTime - lastProgress >= stuckSecs)
                {
                    g.ApplyCheat("chipBlocks", 1);

                    // chipBlocks skips boss blocks. When only boss NeedToKill blocks remain
                    // (non-boss blocks are gone) the ball alone may not reach boss cells
                    // reliably (board empty → straight-vertical oscillation). Use damageBlock
                    // on each alive boss block to represent focused spell fire.
                    bool onlyBossRemains = g.Blocks.All(b => b.Dead || !b.NeedToKill || b.Boss);
                    if (onlyBossRemains)
                    {
                        foreach (var boss in g.Blocks.Where(b => !b.Dead && b.Boss).ToList())
                            g.ApplyCheat("damageBlock", boss.Id);
                    }

                    chips++;
                    lastProgress = g.ElapsedPlayTime;
                }
            }

            if (g.Phase is GamePhase.Won or GamePhase.Lost)
                return (g.Phase, g.ElapsedPlayTime, drains, chips);
        }

        return (g.Phase, g.ElapsedPlayTime, drains, chips);
    }
}
