using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;

namespace Arkanoid.Server;

internal static class GameInitializer
{
    internal static GameInstance Build(
        string levelId, int seed, string configRoot,
        BlockCatalog blockCatalog, RelicCatalog relicCatalog, BonusCatalog? bonusCatalog,
        IProfileStore profileStore, IDungeonStore dungeonStore,
        string pid, ISimLog log)
        => Arkanoid.Core.GameInitializer.Build(
            levelId, seed, configRoot,
            blockCatalog, relicCatalog, bonusCatalog,
            profileStore, dungeonStore, pid, log);
}
