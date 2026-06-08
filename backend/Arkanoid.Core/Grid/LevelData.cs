using Arkanoid.Core.Entities;
namespace Arkanoid.Core.Grid;

public sealed class LevelData
{
    public string Id { get; init; } = "";
    public string Biome { get; init; } = "";
    public Grid Grid { get; init; } = null!;
    public List<Block> Blocks { get; init; } = new();
}
