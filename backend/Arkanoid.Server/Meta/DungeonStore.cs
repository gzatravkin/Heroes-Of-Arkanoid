using Arkanoid.Core.Meta;
namespace Arkanoid.Server.Meta;

/// <summary>Loads, saves and clears the active DungeonRun JSON. Path injected via DI.</summary>
public sealed class DungeonStore : IDungeonStore
{
    private readonly JsonStore<DungeonRun> _store;

    public DungeonStore(string savesDir)
        => _store = new JsonStore<DungeonRun>(savesDir, "dungeon.json", "dungeon");

    public DungeonRun? Load(string pid = "default") => _store.Load(pid);

    public void Save(DungeonRun run, string pid = "default") => _store.Save(run, pid);

    public void Clear(string pid = "default") => _store.Clear(pid);
}
