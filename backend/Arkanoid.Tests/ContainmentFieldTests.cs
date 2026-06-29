using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>Containment Field (design §3 rework of Radiation): a placed zone that SUPPRESSES enemy
/// emitters caught inside it (they can't fire) AND melts the blocks within over time. These assert the
/// suppress TRIGGER + identity, not just "a zone exists".</summary>
public class ContainmentFieldTests
{
    private const double Dt = 1.0 / 60.0;

    private static GameInstance Emitter()
    {
        // FullGame auto-serves (Playing, so systems run); Park stops the ball so it doesn't disturb things.
        var g = K.FullGame(
            "{\"types\":[{\"id\":\"e\",\"biome\":\"t\",\"hp\":9,\"sprite\":\"s\",\"needToKill\":true,\"behavior\":\"emitter\",\"emitInterval\":1.0,\"emitAim\":\"down\"}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":3,\"rows_data\":[\".E.\",\"...\",\"...\"],\"legend\":{\"E\":\"e\"}}");
        K.Park(g);
        return g;
    }

    [Fact]
    public void Emitter_Fires_WithoutContainment()
    {
        // Control: a normal emitter fires past its interval.
        var g = Emitter();
        for (int i = 0; i < 120; i++) g.Tick(Dt);
        Assert.NotEmpty(g.Hazards);
    }

    [Fact]
    public void ContainmentField_SuppressesEmitterInside()
    {
        var g = Emitter();
        var em = g.Blocks.First(b => b.Emitter);
        var c = g.Level.Grid.CellCenter(em.Col, em.Row);
        // Project a containment field over the emitter (Suppresses = the Containment Field flag).
        g.Zones.Add(new Zone { Id = 1, X = c.X, Y = c.Y, Radius = 80, LifeRemaining = 100, DamagePerTick = 0, DamageInterval = 1, Alive = true, Suppresses = true });

        for (int i = 0; i < 200; i++) g.Tick(Dt); // well past the 1.0s interval
        Assert.Empty(g.Hazards); // suppressed: not a single hazard fired
    }

    [Fact]
    public void ContainmentField_HoldsCharged_ThenFires_OnExpiry()
    {
        // Zone (1.5s) OUTLIVES the emit interval (1.0s): the emitter reaches its interval while inside
        // the field but is HELD CHARGED (no shot) — then fires the instant the field expires.
        var g = Emitter();
        var em = g.Blocks.First(b => b.Emitter);
        var c = g.Level.Grid.CellCenter(em.Col, em.Row);
        g.Zones.Add(new Zone { Id = 1, X = c.X, Y = c.Y, Radius = 80, LifeRemaining = 1.5, DamagePerTick = 0, DamageInterval = 1, Alive = true, Suppresses = true });

        for (int i = 0; i < 84; i++) g.Tick(Dt); // ~1.4s: past the 1.0s interval but field still up
        Assert.Empty(g.Hazards); // held charged INSIDE the field — proves active suppression

        for (int i = 0; i < 30; i++) g.Tick(Dt); // cross 1.5s: field expires → held-charged emitter fires
        Assert.NotEmpty(g.Hazards);
    }

    [Fact]
    public void ContainmentField_Cast_MeltsBlocksInside()
    {
        // Cast Containment Field (Engineer's reworked Radiation) — its zone melts blocks over time.
        var g = K.FullGame(
            "{\"types\":[{\"id\":\"b\",\"biome\":\"t\",\"hp\":20,\"sprite\":\"s\",\"needToKill\":true}]}",
            "{\"id\":\"t\",\"biome\":\"t\",\"cols\":3,\"rows\":5,\"rows_data\":[\"...\",\"...\",\"...\",\"...\",\".B.\"],\"legend\":{\"B\":\"b\"}}");
        g.SetCharacter("engineer");
        g.SetLoadout(new[] { "radiation" });
        K.Park(g);
        g.ManaValue = 100;
        var blk = g.Blocks.First(b => !b.Dead);
        // Place the field on the block's column so it melts it.
        var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
        g.Paddle.Center = new Arkanoid.Core.Math.Vec2(c.X, g.Paddle.Center.Y);
        int hp0 = blk.Hp;
        g.CastSlot(0); // Containment Field
        Assert.NotEmpty(g.Zones);
        for (int i = 0; i < 180; i++) g.Tick(Dt); // ~3s of melting
        Assert.True(blk.Hp < hp0, "the field melted the block inside it");
    }
}
