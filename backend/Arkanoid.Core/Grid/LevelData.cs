using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Grid;

public sealed class LevelData
{
    public string Id { get; init; } = "";
    public string Biome { get; init; } = "";
    public Grid Grid { get; init; } = null!;
    public List<Block> Blocks { get; init; } = new();

    // --- Objective flavors + pacing modes (docs/12 identity matrix) ---
    /// <summary>Caverns Demolition: lose when play time exceeds this (0 = off).</summary>
    public double TimeLimit { get; init; }
    /// <summary>Heaven Judgement: win by surviving this many seconds (0 = off).</summary>
    public double SurviveTime { get; init; }
    /// <summary>Hell pressure: all blocks descend one row every N seconds (0 = off). Overrun = lose.</summary>
    public double DescendInterval { get; init; }
    /// <summary>Heaven escalation: all statues level up every N seconds (0 = off).</summary>
    public double EscalateInterval { get; init; }
    /// <summary>Caverns multi-floor collapse: pre-built block sets for floors after the first.</summary>
    public List<List<Block>> ExtraFloors { get; init; } = new();
}
