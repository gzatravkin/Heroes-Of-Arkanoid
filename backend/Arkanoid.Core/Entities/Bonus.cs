using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

/// <summary>A falling bonus pickup spawned when a block is destroyed.</summary>
public sealed class Bonus
{
    public int    Id   { get; init; }
    public Vec2   Pos;
    public Vec2   Vel;
    /// <summary>Catalog effect id of this bonus (e.g. "extra_ball", "heal").</summary>
    public string Type     { get; init; } = "";
    /// <summary>Atlas icon key for the renderer (e.g. "ui/bonus/BonusSplit").</summary>
    public string Icon     { get; init; } = "";
    /// <summary>Ball/pickup count (extra_ball: 1 vs 2).</summary>
    public int    Count    { get; init; } = 1;
    /// <summary>Override duration (0 = use SimConfig default).</summary>
    public double Duration { get; init; }
    /// <summary>Full-refill flag (mana_surge powerup variant).</summary>
    public bool   Full     { get; init; }
    public bool   Alive = true;
    /// <summary>§1 Sleight of Hand: this pickup was spawned AS a duplicate — it cannot itself be
    /// re-duplicated, so a centre-caught duplicate never chains into infinite copies.</summary>
    public bool   NoDuplicate = false;
}
