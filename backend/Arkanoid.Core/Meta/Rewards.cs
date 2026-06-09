namespace Arkanoid.Core.Meta;

public sealed class RewardResult
{
    public bool FirstClear     { get; init; }
    public int  ExpGained      { get; init; }
    public int  PointsGained   { get; init; }
    public int  CrystalsGained { get; init; }
    public int  NewLevel       { get; init; }
    public bool LeveledUp      { get; init; }
}

public static class Rewards
{
    /// <summary>
    /// Grants first-clear rewards to <paramref name="p"/> for completing <paramref name="levelId"/>.
    /// Idempotent: subsequent calls with the same levelId return FirstClear=false and grant nothing.
    /// Mutates <paramref name="p"/> in-place.
    /// </summary>
    /// <param name="treasureBonus">
    /// Extra crystals from equipped treasure items (from GameInstance.ItemTreasureBonus).
    /// Only applied on first clear.
    /// </param>
    public static RewardResult GrantLevelCompletion(Profile p, string levelId, ProgressionConfig cfg,
                                                     int treasureBonus = 0)
    {
        if (p.CompletedLevels.Contains(levelId))
        {
            return new RewardResult
            {
                FirstClear     = false,
                ExpGained      = 0,
                PointsGained   = 0,
                CrystalsGained = 0,
                NewLevel       = p.Level,
                LeveledUp      = false,
            };
        }

        var totalCrystals = cfg.CrystalsRewardPerLevel + treasureBonus;

        p.CompletedLevels.Add(levelId);
        p.Exp      += cfg.ExpRewardPerLevel;
        p.Points   += cfg.PointsRewardPerLevel;
        p.Crystals += totalCrystals;

        int startingLevel = p.Level;
        int totalPointsFromLevelUps = 0;

        // Level-up loop: consume EXP thresholds until insufficient.
        while (p.Exp >= cfg.ExpToLevel(p.Level))
        {
            p.Exp -= cfg.ExpToLevel(p.Level);
            p.Level++;
            totalPointsFromLevelUps += cfg.PointsRewardPerLevel; // modest bonus per level gained
        }

        p.Points += totalPointsFromLevelUps;

        return new RewardResult
        {
            FirstClear     = true,
            ExpGained      = cfg.ExpRewardPerLevel,
            PointsGained   = cfg.PointsRewardPerLevel + totalPointsFromLevelUps,
            CrystalsGained = totalCrystals,
            NewLevel       = p.Level,
            LeveledUp      = p.Level > startingLevel,
        };
    }
}
