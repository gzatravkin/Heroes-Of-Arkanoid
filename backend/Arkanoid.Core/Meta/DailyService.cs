using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Arkanoid.Core.Meta;

public sealed class DailyMissionState
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("progress")] public int    Progress { get; set; }
    [JsonPropertyName("claimed")]  public bool   Claimed  { get; set; }
}

/// <summary>Per-profile daily-mission state (plan §A.2). Three missions per day from a weekly pool.</summary>
public sealed class DailyState
{
    [JsonPropertyName("dayId")]       public int DayId       { get; set; } = -1;
    [JsonPropertyName("weekId")]      public int WeekId      { get; set; } = -1;
    [JsonPropertyName("missions")]    public List<DailyMissionState> Missions { get; set; } = new();
    [JsonPropertyName("streak")]      public int Streak      { get; set; } = 0;
    [JsonPropertyName("lastClaimDay")]public int LastClaimDay{ get; set; } = -1;
    [JsonPropertyName("streakClaimedDay")] public int StreakClaimedDay { get; set; } = -1;
}

/// <summary>Reward returned by a daily claim.</summary>
public sealed class DailyClaimResult
{
    public bool Ok          { get; set; }
    public int  Gems        { get; set; }
    public int  CardDust    { get; set; }
    public bool StreakBonus { get; set; }
    public int  Streak      { get; set; }
}

/// <summary>
/// Pure daily-mission logic (plan §A.2): rolls 3 deterministic missions/day from a deterministic
/// weekly pool, tracks metric progress, and pays Gems + Card Dust on claim with a 7-day streak chest.
/// </summary>
public static class DailyService
{
    public const int DailyCount = 3;
    public const int WeekPoolSize = 6;
    public const int StreakTarget = 7;
    public const int StreakBonusGems = 100;
    public const int StreakBonusCardDust = 150;

    /// <summary>Re-roll the day's missions if the day changed (deterministic per day, from a weekly pool).</summary>
    public static void EnsureToday(Profile p, MissionCatalog catalog, int dayId, int weekId)
    {
        if (p.Daily.DayId == dayId) return;

        // Streak bookkeeping: a fresh day that isn't consecutive with the last *claim* resets the streak.
        if (p.Daily.LastClaimDay >= 0 && p.Daily.LastClaimDay < dayId - 1)
            p.Daily.Streak = 0;

        var pool = WeekPool(catalog, weekId);
        var rng = new Sim.Rng(unchecked(dayId * 73856093));
        var chosen = PickDistinct(pool, DailyCount, rng);

        p.Daily.DayId    = dayId;
        p.Daily.WeekId   = weekId;
        p.Daily.Missions = chosen.Select(m => new DailyMissionState { Id = m.Id }).ToList();
    }

    /// <summary>Record metric progress against today's missions (capped at target).</summary>
    public static void Record(Profile p, MissionCatalog catalog, int dayId, int weekId, string metric, int amount)
    {
        if (amount <= 0) return;
        EnsureToday(p, catalog, dayId, weekId);
        foreach (var ms in p.Daily.Missions)
        {
            if (!catalog.TryGet(ms.Id, out var def) || def.Metric != metric) continue;
            ms.Progress = System.Math.Min(def.Target, ms.Progress + amount);
        }
    }

    /// <summary>Claim a completed mission's reward (idempotent per mission). Awards Gems + Card Dust, and
    /// the streak chest when all of today's missions are claimed on a new day.</summary>
    public static DailyClaimResult Claim(Profile p, MissionCatalog catalog, int dayId, int weekId, string missionId)
    {
        EnsureToday(p, catalog, dayId, weekId);
        var ms = p.Daily.Missions.FirstOrDefault(m => m.Id == missionId);
        if (ms == null || ms.Claimed) return new DailyClaimResult { Ok = false };
        if (!catalog.TryGet(missionId, out var def)) return new DailyClaimResult { Ok = false };
        if (ms.Progress < def.Target) return new DailyClaimResult { Ok = false };

        ms.Claimed = true;
        Wallet.Add(p, Currency.Souls, def.RewardGems);
        Wallet.Add(p, Currency.Sparks, def.RewardCardDust);
        var result = new DailyClaimResult { Ok = true, Gems = def.RewardGems, CardDust = def.RewardCardDust };

        // Streak: count this day once, when ALL of today's missions are claimed.
        if (p.Daily.Missions.All(m => m.Claimed) && p.Daily.LastClaimDay != dayId)
        {
            p.Daily.Streak = (p.Daily.LastClaimDay == dayId - 1) ? p.Daily.Streak + 1 : 1;
            p.Daily.LastClaimDay = dayId;
            if (p.Daily.Streak >= StreakTarget && p.Daily.StreakClaimedDay != dayId)
            {
                p.Daily.StreakClaimedDay = dayId;
                p.Daily.Streak = 0; // reset the meter after the chest
                Wallet.Add(p, Currency.Souls, StreakBonusGems);
                Wallet.Add(p, Currency.Sparks, StreakBonusCardDust);
                result.StreakBonus = true;
            }
        }
        result.Streak = p.Daily.Streak;
        return result;
    }

    /// <summary>The deterministic week pool — a subset of the catalog reshuffled each week.</summary>
    public static IReadOnlyList<MissionDef> WeekPool(MissionCatalog catalog, int weekId)
    {
        var all = catalog.Missions.ToList();
        var rng = new Sim.Rng(unchecked(weekId * 19349663 + 17));
        Shuffle(all, rng);
        return all.Take(System.Math.Min(WeekPoolSize, all.Count)).ToList();
    }

    private static List<MissionDef> PickDistinct(IReadOnlyList<MissionDef> pool, int count, Sim.Rng rng)
    {
        var copy = pool.ToList();
        Shuffle(copy, rng);
        return copy.Take(System.Math.Min(count, copy.Count)).ToList();
    }

    private static void Shuffle<T>(List<T> list, Sim.Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Range(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
