using System.Linq;
using Arkanoid.Core.Meta;
using Xunit;

/// <summary>
/// League + anti-cheat logic (social plan §0.3 / §3.3 / §A.3). Tested against the in-memory adapter; the
/// SQLite adapter shares the exact same <see cref="ILeaderboardStore"/> contract and is exercised E2E.
/// </summary>
public class LeaderboardTests
{
    private static ILeaderboardStore Store() => new InMemoryLeaderboardStore();

    [Fact]
    public void UpsertScore_KeepsMax()
    {
        var s = Store();
        s.UpsertScore("trial", "0", new ScoreRecord { PlayerId = "a", Score = 100 });
        s.UpsertScore("trial", "0", new ScoreRecord { PlayerId = "a", Score = 80 });  // lower → ignored
        s.UpsertScore("trial", "0", new ScoreRecord { PlayerId = "a", Score = 150 }); // higher → kept
        Assert.Equal(150, s.GetScore("trial", "0", "a")!.Score);
    }

    [Fact]
    public void Submit_Plausible_EntersBoard()
    {
        var s = Store();
        Assert.True(LeaderboardService.Submit(s, "a", "A", "trial", "0", 5000));
        Assert.Equal(5000, s.GetScore("trial", "0", "a")!.Score);
    }

    // ── The headline requirement: impossible scores → silent shadow-ban ──────────

    [Fact]
    public void Submit_ImpossibleScore_Strikes_ThenShadows_Silently()
    {
        var s = Store();
        int impossible = LeaderboardService.MaxPlausibleScore("trial") + 1;

        // First impossible submit: rejected from public board, struck, but call "succeeds" shape.
        Assert.False(LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible));
        Assert.Equal(1, s.GetPlayerState("cheater").Strikes);
        Assert.False(s.GetPlayerState("cheater").Shadowed);

        // Second: crosses the threshold → shadowed.
        Assert.False(LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible));
        Assert.True(s.GetPlayerState("cheater").Shadowed);
    }

    [Fact]
    public void ShadowedScore_IsInvisible_ToOtherPlayers_ButVisibleToSelf()
    {
        var s = Store();
        int impossible = LeaderboardService.MaxPlausibleScore("trial") + 1;
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible);
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible); // now shadowed
        LeaderboardService.Submit(s, "honest", "H", "trial", "0", 6000);

        // Honest player's public cohort must NOT contain the cheater.
        var honestView = LeaderboardService.GenerateCohort(s, "honest", "H", "trial", "0");
        Assert.DoesNotContain(honestView, e => e.PlayerId == "cheater");

        // The cheater still sees their own row (no signal that they're banned).
        var cheaterView = LeaderboardService.GenerateCohort(s, "cheater", "C", "trial", "0");
        Assert.Contains(cheaterView, e => e.IsMe);
    }

    [Fact]
    public void OnceShadowed_EvenLegitScore_StaysExcluded()
    {
        var s = Store();
        int impossible = LeaderboardService.MaxPlausibleScore("trial") + 1;
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible);
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible); // shadowed
        // Now they submit a legit score — still shadowed, so still excluded publicly.
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", 4000);
        LeaderboardService.Submit(s, "honest", "H", "trial", "0", 100);
        var honestView = LeaderboardService.GenerateCohort(s, "honest", "H", "trial", "0");
        Assert.DoesNotContain(honestView, e => e.PlayerId == "cheater");
    }

    [Fact]
    public void Store_TopScores_ExcludesShadowed_WhenPublicRead()
    {
        // The shadow-ban invariant lives at the store level (what a real multiplayer board reads).
        var s = Store();
        int impossible = LeaderboardService.MaxPlausibleScore("trial") + 1;
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible);
        LeaderboardService.Submit(s, "cheater", "C", "trial", "0", impossible); // shadowed
        LeaderboardService.Submit(s, "honest", "H", "trial", "0", 100);

        Assert.DoesNotContain(s.TopScores("trial", "0", 50, includeShadowed: false), r => r.PlayerId == "cheater");
        Assert.Contains(s.TopScores("trial", "0", 50, includeShadowed: true), r => r.PlayerId == "cheater");
    }

    // ── Cohort + leagues ──────────────────────────────────────────────────────

    [Fact]
    public void Cohort_IsFullSize_ContainsMe_Ranked()
    {
        var s = Store();
        LeaderboardService.Submit(s, "me", "Me", "trial", "0", 3000);
        var cohort = LeaderboardService.GenerateCohort(s, "me", "Me", "trial", "0");
        Assert.Equal(LeaderboardService.CohortSize, cohort.Count);
        Assert.Single(cohort.Where(e => e.IsMe));
        for (int i = 0; i < cohort.Count; i++) Assert.Equal(i + 1, cohort[i].Rank);
        Assert.True(cohort.Zip(cohort.Skip(1)).All(p => p.First.Score >= p.Second.Score)); // sorted desc
    }

    [Fact]
    public void Cohort_BotScores_ScaleWithTier()
    {
        var s = Store();
        // Wood player's bots should be weaker than a Diamond player's bots (mean grows with tier).
        var woodState = s.GetPlayerState("a"); woodState.Tier = (int)LeagueTier.Wood; s.SetPlayerState(woodState);
        var diaState  = s.GetPlayerState("b"); diaState.Tier  = (int)LeagueTier.Diamond; s.SetPlayerState(diaState);

        var woodBots = LeaderboardService.GenerateCohort(s, "a", "A", "trial", "0").Where(e => e.IsBot).Average(e => e.Score);
        var diaBots  = LeaderboardService.GenerateCohort(s, "b", "B", "trial", "0").Where(e => e.IsBot).Average(e => e.Score);
        Assert.True(diaBots > woodBots, $"diamond bots ({diaBots}) should beat wood bots ({woodBots})");
    }

    [Fact]
    public void Cohort_Deterministic_SamePlayerWeekTier()
    {
        var s = Store();
        var a = LeaderboardService.GenerateCohort(s, "me", "Me", "trial", "5").Select(e => (e.PlayerId, e.Score)).ToList();
        var b = LeaderboardService.GenerateCohort(s, "me", "Me", "trial", "5").Select(e => (e.PlayerId, e.Score)).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void ResolveWeek_TopRank_Promotes_AndIsIdempotent()
    {
        var s = Store();
        // A monstrous (but plausible) score guarantees rank 1 → promotion from Wood.
        LeaderboardService.Submit(s, "me", "Me", "trial", "3", LeaderboardService.MaxPlausibleScore("trial"));
        var res = LeaderboardService.ResolveWeek(s, "me", "Me", "trial", 3);
        Assert.True(res.Resolved);
        Assert.True(res.Promoted);
        Assert.Equal((int)LeagueTier.Bronze, res.NewTier);
        Assert.True(res.RewardMedals > 0);

        // Re-resolving the same week does nothing (idempotent).
        var again = LeaderboardService.ResolveWeek(s, "me", "Me", "trial", 3);
        Assert.False(again.Resolved);
        Assert.Equal((int)LeagueTier.Bronze, s.GetPlayerState("me").Tier);
    }

    [Fact]
    public void ResolveWeek_NoScore_Demotes_FromHigherTier()
    {
        var s = Store();
        var st = s.GetPlayerState("me"); st.Tier = (int)LeagueTier.Gold; s.SetPlayerState(st);
        // No score submitted → rank near the bottom of a bot cohort → demotion.
        var res = LeaderboardService.ResolveWeek(s, "me", "Me", "trial", 2);
        Assert.True(res.Resolved);
        Assert.True(res.Demoted);
        Assert.Equal((int)LeagueTier.Silver, res.NewTier);
    }
}
