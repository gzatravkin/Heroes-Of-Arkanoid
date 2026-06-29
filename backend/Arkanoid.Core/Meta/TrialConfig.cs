namespace Arkanoid.Core.Meta;

/// <summary>
/// The Weekly Trial (plan §A.3): one shared, server-chosen level + seed per week so every player faces
/// the identical gauntlet — fair to compare, and (since the battle runs server-side) the resulting score
/// is authoritative. The board id is "trial"; the period is the week id.
/// </summary>
public static class TrialConfig
{
    public const string BoardId = "trial";
    /// <summary>The fixed gauntlet level for the trial (a dense Hell level).</summary>
    public const string LevelId = "hell-7";

    /// <summary>Server-owned weekly seed — the client never chooses it (anti-cheat).</summary>
    public static int SeedFor(int weekId) => unchecked(weekId * 2_000_003 + 12345) & 0x7fffffff;

    /// <summary>Score from a finished trial run (server-authoritative inputs only).</summary>
    public static int Score(int blocksDestroyed, bool won) => blocksDestroyed * 10 + (won ? 5000 : 0);
}
