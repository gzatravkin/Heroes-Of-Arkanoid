using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Design-fidelity tests for the continuous Rift (owner 2026-06-16): the whole rift is ONE GameInstance
/// (all floors stacked as ExtraFloors), clearing a floor slides the next in WITHOUT a reload, surviving
/// indestructible "leftovers" relocate to the side columns, and the depth reward steps up at 3/5/7/10.
/// </summary>
public class RiftContinuousTests
{
    private const string Types =
        "{\"types\":[" +
        "{\"id\":\"b\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":true}," +
        "{\"id\":\"w\",\"biome\":\"hell\",\"hp\":1,\"sprite\":\"s\",\"needToKill\":false,\"indestructible\":true}]}";

    // floor 0: a row of destructibles up top, two indestructible walls in the MIDDLE (cols 2,3).
    private const string Floor0 =
        "{\"id\":\"f0\",\"biome\":\"hell\",\"cols\":6,\"rows\":6,\"rows_data\":" +
        "[\"BBBBBB\",\"..WW..\",\"......\",\"......\",\"......\",\"......\"],\"legend\":{\"B\":\"b\",\"W\":\"w\"}}";
    // floor 1: just a couple of destructibles.
    private const string Floor1 =
        "{\"id\":\"f1\",\"biome\":\"hell\",\"cols\":6,\"rows\":6,\"rows_data\":" +
        "[\"..BB..\",\"......\",\"......\",\"......\",\"......\",\"......\"],\"legend\":{\"B\":\"b\"}}";

    private static GameInstance TwoFloorRift()
    {
        var cat = BlockCatalog.FromJson(Types);
        var level = LevelLoader.FromRiftFloors(new[] { Floor0, Floor1 }, cat);
        var g = new GameInstance(level, SimConfig.Default, seed: 1);
        g.SetRiftMode(true);
        g.Serve();
        return g;
    }

    [Fact]
    public void Rift_StacksAllFloors_InOneInstance()
    {
        var g = TwoFloorRift();
        Assert.True(g.RiftMode);
        Assert.Single(g.ExtraFloors);             // floor 1 waiting as an ExtraFloor
        Assert.Equal(0, g.FloorIndex);            // starting on floor 0
        // block ids are unique across floors (shared counter)
        var allIds = g.Blocks.Select(b => b.Id).Concat(g.ExtraFloors[0].Select(b => b.Id)).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
    }

    [Fact]
    public void Rift_ClearFloor_Descends_AndSlidesLeftoversToSides()
    {
        var g = TwoFloorRift();
        // clear every destructible on floor 0 (the ball would do this in play)
        foreach (var blk in g.Blocks.Where(b => b.NeedToKill).ToList()) blk.Dead = true;
        g.Tick(SimConfig.Default.FixedDt);                                // → pauses for the §8 draft
        g.PickRiftModifier(g.RiftDraftChoices[0]);                        // pick advances + slides the next floor

        Assert.Equal(1, g.FloorIndex);                                   // descended to floor 1
        Assert.Contains(g.Blocks, b => !b.Dead && b.NeedToKill);         // floor 1's blocks slid in
        var walls = g.Blocks.Where(b => !b.Dead && b.Indestructible).ToList();
        Assert.Equal(2, walls.Count);                                    // both walls survived (immortal leftovers)
        Assert.All(walls, w => Assert.True(w.Col == 0 || w.Col == 5,      // ...and moved to the side columns
            $"leftover wall should sit on a side edge, was col {w.Col}"));
    }

    [Fact]
    public void Rift_FloorClear_OffersDraft_ThenPickAppliesLive_AndAdvances()
    {
        var g = TwoFloorRift();
        // clear floor 0 → the sim should PAUSE for the §8 draft (not slide the next floor yet).
        foreach (var blk in g.Blocks.Where(b => b.NeedToKill).ToList()) blk.Dead = true;
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.AwaitingRiftDraft, "clearing a floor should pause for the §8 draft");
        Assert.Equal(3, g.RiftDraftChoices.Count);
        Assert.Equal(0, g.FloorIndex); // not advanced while awaiting

        // frozen: further ticks do nothing until a pick.
        g.Tick(SimConfig.Default.FixedDt);
        Assert.True(g.AwaitingRiftDraft);

        // pick a live boon (Twin Serve = +1 ball), which also advances to floor 1.
        int ballsBefore = g.SpareBalls;
        g.PickRiftModifier("twin_serve");
        Assert.False(g.AwaitingRiftDraft);
        Assert.Equal(ballsBefore + 1, g.SpareBalls);                     // applied LIVE
        Assert.Equal(1, g.FloorIndex);                                   // advanced to floor 1
        Assert.Contains(g.Blocks, b => !b.Dead && b.NeedToKill);         // floor 1's blocks slid in
    }

    [Fact]
    public void RiftReward_StepsUpAtMilestones()
    {
        Assert.Equal(1.00, RiftModifierService.MilestoneMult(2), 3);  // before any milestone
        Assert.Equal(1.35, RiftModifierService.MilestoneMult(3), 3);  // depth 3
        Assert.Equal(1.70, RiftModifierService.MilestoneMult(5), 3);  // depth 5
        Assert.Equal(2.05, RiftModifierService.MilestoneMult(7), 3);  // depth 7
        Assert.Equal(2.40, RiftModifierService.MilestoneMult(10), 3); // depth 10
        Assert.Equal(3, RiftModifierService.NextMilestone(2));
        Assert.Equal(5, RiftModifierService.NextMilestone(3));
        Assert.Equal(0, RiftModifierService.NextMilestone(10));       // none left
        // the milestone multiplier makes a depth-3 clear pay more than the base curve alone.
        int baseAt3 = 20 * 3 + 10 * 3 * 3;                            // accelerating base curve
        Assert.True(RiftModifierService.DepthCrystals(3, 10, 1.0) > baseAt3,
            "depth-3 reward should exceed the base curve thanks to the milestone step-up");
    }
}
