using System.Linq;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>Daily missions (plan §A.2): deterministic rolls, progress tracking, claim rewards, streak.</summary>
public class DailyTests
{
    private static MissionCatalog Catalog() => MissionCatalog.FromJson("""
    { "missions": [
      { "id": "demo_s", "name": "Demolition",  "metric": "blocks_destroyed", "target": 150, "rewardGems": 15, "rewardCardDust": 20 },
      { "id": "demo_l", "name": "Big Demo",     "metric": "blocks_destroyed", "target": 400, "rewardGems": 40, "rewardCardDust": 60 },
      { "id": "win_1",  "name": "Persistent",   "metric": "levels_won",      "target": 1,   "rewardGems": 15, "rewardCardDust": 20 },
      { "id": "win_2",  "name": "Victor",       "metric": "levels_won",      "target": 2,   "rewardGems": 25, "rewardCardDust": 35 },
      { "id": "play_3", "name": "Warm-up",      "metric": "battles_played",  "target": 3,   "rewardGems": 15, "rewardCardDust": 20 },
      { "id": "play_6", "name": "Marathon",     "metric": "battles_played",  "target": 6,   "rewardGems": 30, "rewardCardDust": 45 }
    ]}
    """);

    [Fact]
    public void EnsureToday_Rolls3Distinct_Deterministic()
    {
        var cat = Catalog();
        var p1 = new Profile(); DailyService.EnsureToday(p1, cat, dayId: 10, weekId: 1);
        var p2 = new Profile(); DailyService.EnsureToday(p2, cat, dayId: 10, weekId: 1);
        Assert.Equal(3, p1.Daily.Missions.Count);
        Assert.Equal(3, p1.Daily.Missions.Select(m => m.Id).Distinct().Count());
        Assert.Equal(p1.Daily.Missions.Select(m => m.Id), p2.Daily.Missions.Select(m => m.Id)); // deterministic
    }

    [Fact]
    public void NewDay_Rerolls_AndResetsProgress()
    {
        var cat = Catalog();
        var p = new Profile();
        DailyService.EnsureToday(p, cat, 10, 1);
        DailyService.Record(p, cat, 10, 1, "blocks_destroyed", 9999);
        DailyService.EnsureToday(p, cat, 11, 1); // next day
        Assert.Equal(11, p.Daily.DayId);
        Assert.All(p.Daily.Missions, m => Assert.Equal(0, m.Progress));
    }

    [Fact]
    public void Record_AddsProgress_ToMatchingMetric_Capped()
    {
        var cat = Catalog();
        var p = new Profile();
        // Force a known mission set by picking a day whose roll includes a blocks mission; just record big.
        DailyService.EnsureToday(p, cat, 10, 1);
        DailyService.Record(p, cat, 10, 1, "blocks_destroyed", 1_000_000);
        foreach (var ms in p.Daily.Missions)
        {
            cat.TryGet(ms.Id, out var def);
            if (def!.Metric == "blocks_destroyed") Assert.Equal(def.Target, ms.Progress); // capped, not overshooting
        }
    }

    [Fact]
    public void Claim_RequiresComplete_GrantsReward_Idempotent()
    {
        var cat = Catalog();
        var p = new Profile();
        DailyService.EnsureToday(p, cat, 10, 1);
        var first = p.Daily.Missions[0];
        cat.TryGet(first.Id, out var def);

        // Incomplete → claim fails.
        Assert.False(DailyService.Claim(p, cat, 10, 1, first.Id).Ok);

        // Complete it, then claim → reward granted.
        DailyService.Record(p, cat, 10, 1, def!.Metric, def.Target);
        int gemsBefore = p.Souls, dustBefore = p.Sparks; // gems→Souls, dust→Sparks (economy rework)
        var res = DailyService.Claim(p, cat, 10, 1, first.Id);
        Assert.True(res.Ok);
        Assert.Equal(gemsBefore + def.RewardGems, p.Souls);
        Assert.Equal(dustBefore + def.RewardCardDust, p.Sparks);

        // Second claim → no double reward.
        var again = DailyService.Claim(p, cat, 10, 1, first.Id);
        Assert.False(again.Ok);
        Assert.Equal(gemsBefore + def.RewardGems, p.Souls);
    }

    [Fact]
    public void Streak_BuildsOverConsecutiveDays_ChestAt7()
    {
        var cat = Catalog();
        var p = new Profile();
        bool sawChest = false;
        for (int day = 1; day <= 7; day++)
        {
            DailyService.EnsureToday(p, cat, day, 1);
            // Complete everything this day.
            DailyService.Record(p, cat, day, 1, "blocks_destroyed", 1_000_000);
            DailyService.Record(p, cat, day, 1, "levels_won", 1_000_000);
            DailyService.Record(p, cat, day, 1, "battles_played", 1_000_000);
            DailyClaimResult last = new();
            foreach (var ms in p.Daily.Missions.ToList())
                last = DailyService.Claim(p, cat, day, 1, ms.Id);
            if (last.StreakBonus) sawChest = true;
        }
        Assert.True(sawChest, "a 7-day streak should award the streak chest");
    }

    [Fact]
    public void WeekPool_Deterministic_AndDiffersByWeek()
    {
        var cat = Catalog();
        var w1a = DailyService.WeekPool(cat, 1).Select(m => m.Id).ToList();
        var w1b = DailyService.WeekPool(cat, 1).Select(m => m.Id).ToList();
        var w2  = DailyService.WeekPool(cat, 2).Select(m => m.Id).ToList();
        Assert.Equal(w1a, w1b);
        Assert.NotEqual(w1a, w2);
    }
}
