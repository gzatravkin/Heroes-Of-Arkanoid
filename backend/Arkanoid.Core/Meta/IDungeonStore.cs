namespace Arkanoid.Core.Meta;

/// <summary>Seam for loading, saving and clearing per-profile dungeon runs. Implement with DungeonStore for production.</summary>
public interface IDungeonStore
{
    DungeonRun? Load(string pid = "default");
    void Save(DungeonRun run, string pid = "default");
    void Clear(string pid = "default");
}
