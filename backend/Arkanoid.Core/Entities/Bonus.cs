using Arkanoid.Core.Math;
namespace Arkanoid.Core.Entities;

/// <summary>A falling bonus pickup spawned when a block is destroyed.</summary>
public sealed class Bonus
{
    public int    Id   { get; init; }
    public Vec2   Pos;
    public Vec2   Vel;
    /// <summary>Catalog id of this bonus (e.g. "extra_ball", "heal").</summary>
    public string Type { get; init; } = "";
    /// <summary>Atlas icon key for the renderer (e.g. "ui/bonus/BonusSplit").</summary>
    public string Icon { get; init; } = "";
    public bool   Alive = true;
}
