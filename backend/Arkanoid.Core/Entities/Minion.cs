namespace Arkanoid.Core.Entities;

/// <summary>§3 Necromancer summons — a friendly minion entity, distinct from the ball (its own position
/// and behaviour). Two bespoke kinds share only this data carrier; the per-tick logic lives in
/// <see cref="Sim.Systems.BonewalkerSystem"/> and <see cref="Sim.Systems.BoneGolemSystem"/>:
/// <list type="bullet">
/// <item><b>bonewalker</b> — walks the block rooftops, meleeing whatever block it stands on.</item>
/// <item><b>golem</b> — a bodyguard that climbs a column, bulldozing its blocks and bodying enemy fire.</item>
/// </list></summary>
public sealed class Minion
{
    public int    Id            { get; init; }
    public string Kind          { get; init; } = ""; // "bonewalker" | "golem"
    public double X             { get; set; }
    public double Y             { get; set; }
    public double Width         { get; set; }
    public double Height        { get; set; }
    /// <summary>Golem fire-soak (drops as it tanks hazards; dies at 0). Unused by the bonewalker.</summary>
    public int    Hp            { get; set; }
    public int    MaxHp         { get; set; }
    /// <summary>Bonewalker walk-duration (seconds). Unused by the golem (it lives until soaked or off-top).</summary>
    public double LifeRemaining { get; set; }
    /// <summary>Bonewalker initial walk-duration — basis for the on-screen life bar (life fraction).</summary>
    public double MaxLife       { get; set; }
    /// <summary>Bonewalker horizontal heading: +1 right, -1 left.</summary>
    public int    Dir           { get; set; } = 1;
    /// <summary>Melee / bulldoze cadence accumulator.</summary>
    public double StepAccum     { get; set; }
    public bool   Alive         { get; set; } = true;
}
