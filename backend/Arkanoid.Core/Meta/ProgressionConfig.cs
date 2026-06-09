namespace Arkanoid.Core.Meta;

public sealed class ProgressionConfig
{
    /// <summary>Base EXP required to advance from level 1 to level 2.</summary>
    public int ExpBase { get; set; } = 100;

    /// <summary>Multiplicative growth factor per level (geometric curve).</summary>
    public double ExpGrowth { get; set; } = 1.1;

    /// <summary>EXP awarded on first clear of a level.</summary>
    public int ExpRewardPerLevel { get; set; } = 120;

    /// <summary>Upgrade points awarded on first clear of a level.</summary>
    public int PointsRewardPerLevel { get; set; } = 2;

    /// <summary>Crystals awarded on first clear of a level.</summary>
    public int CrystalsRewardPerLevel { get; set; } = 10;

    /// <summary>Maximum spell upgrade level a player can reach.</summary>
    public int MaxSpellLevel { get; set; } = 10;

    /// <summary>
    /// Probability (0..1) that clearing a campaign node tears open a Rift — the
    /// opt-in dungeon run entry point. Tunable here so designers can dial how often
    /// rifts interrupt the campaign without touching code.
    /// </summary>
    public double RiftChance { get; set; } = 0.34;

    /// <summary>EXP threshold to advance from <paramref name="level"/> to the next level.</summary>
    public int ExpToLevel(int level) => (int)global::System.Math.Round(ExpBase * global::System.Math.Pow(ExpGrowth, level - 1));

    public static ProgressionConfig Default { get; } = new();
}
