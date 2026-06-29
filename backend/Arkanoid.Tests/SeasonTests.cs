using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Season Festival (plan §C): reward track, rollover reset, events, board-changing modifier.</summary>
public class SeasonTests
{
    private static SeasonCatalog Seasons() => SeasonCatalog.FromJson("""
    { "themes": ["Inferno","Frost"], "tokensPerBattle": 10, "track": [
      { "tier": 1, "tokens": 40,  "rewardGems": 50 },
      { "tier": 2, "tokens": 110, "rewardCardDust": 80 },
      { "tier": 3, "tokens": 200, "rewardModuleCores": 30 }
    ]}
    """);
    private static EventCatalog Events() => EventCatalog.FromJson("""
    { "events": [
      { "id": "inferno", "name": "Inferno", "effect": "ball_damage", "magnitude": 1, "tokenPerBattle": 12, "milestoneTokens": 50, "rewardModuleCores": 40 },
      { "id": "frost",   "name": "Frost",   "effect": "max_mana",    "magnitude": 25,"tokenPerBattle": 12, "milestoneTokens": 50, "rewardGems": 80 }
    ]}
    """);

    [Fact]
    public void Theme_RotatesBySeason()
    {
        var s = Seasons();
        Assert.Equal("Inferno", s.ThemeFor(0));
        Assert.Equal("Frost", s.ThemeFor(1));
        Assert.Equal("Inferno", s.ThemeFor(2));
    }

    [Fact]
    public void AddTokens_Accumulates_AndDrivesScore()
    {
        var p = new Profile();
        SeasonService.AddTokens(p, 0, 30);
        SeasonService.AddTokens(p, 0, 30);
        Assert.Equal(60, p.Season.Tokens);
        Assert.Equal(60, SeasonService.SeasonScore(p));
    }

    [Fact]
    public void SeasonRollover_ResetsTrack()
    {
        var p = new Profile();
        SeasonService.AddTokens(p, 0, 100);
        SeasonService.ClaimTier(p, Seasons(), 0, 1);
        SeasonService.EnsureSeason(p, 1); // new season
        Assert.Equal(0, p.Season.Tokens);
        Assert.Empty(p.Season.ClaimedTiers);
    }

    [Fact]
    public void ClaimTier_RequiresThreshold_GrantsOnce()
    {
        var p = new Profile();
        var cat = Seasons();
        SeasonService.AddTokens(p, 0, 45); // past tier 1 (40), short of tier 2 (110)
        Assert.False(SeasonService.ClaimTier(p, cat, 0, 2).Ok); // not enough
        var r = SeasonService.ClaimTier(p, cat, 0, 1);
        Assert.True(r.Ok);
        Assert.Equal(50, p.Souls);                     // tier-1 gems → Souls (economy rework)
        Assert.False(SeasonService.ClaimTier(p, cat, 0, 1).Ok); // idempotent
    }

    [Fact]
    public void Event_RotatesWeekly_AndModifierChangesBoard()
    {
        var ev = Events();
        Assert.Equal("inferno", ev.Current(0)!.Id);
        Assert.Equal("frost",   ev.Current(1)!.Id);

        // The inferno modifier (+1 ball damage) actually changes the battle (plan §C identity test).
        var g = K.OneBlock(5);
        int before = g.ItemBallDamageBonus;
        EventService.ApplyModifier(g, ev.Current(0)!);
        Assert.Equal(before + 1, g.ItemBallDamageBonus);
    }

    [Fact]
    public void EventMilestone_ClaimsOnce_AtThreshold()
    {
        var p = new Profile();
        var ev = Events().Current(0)!; // inferno, milestone 50, +40 cores
        EventService.AddTokens(p, 0, 30);
        Assert.False(EventService.ClaimMilestone(p, ev, 0).Ok); // short
        EventService.AddTokens(p, 0, 30); // now 60
        var r = EventService.ClaimMilestone(p, ev, 0);
        Assert.True(r.Ok);
        Assert.Equal(40, p.Sparks); // event cores → Sparks (economy rework)
        Assert.False(EventService.ClaimMilestone(p, ev, 0).Ok); // idempotent
    }

    [Fact]
    public void EventRollover_ResetsEventProgress()
    {
        var p = new Profile();
        EventService.AddTokens(p, 0, 60);
        EventService.EnsureEvent(p, 1);
        Assert.Equal(0, p.Season.EventTokens);
        Assert.False(p.Season.EventClaimed);
    }
}
