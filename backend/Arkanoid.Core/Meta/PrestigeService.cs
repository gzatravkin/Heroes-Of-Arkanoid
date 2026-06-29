using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Prestige campaign loop (plan §B.1) — the reframed "endless". Beating the campaign lets you Ascend into
/// a harder, remixed New Game+: difficulty scales with tier (block HP hardening, reused from
/// <see cref="DungeonService.ApplyTier"/>) and layouts are mutated with biome-appropriate enemies. Pure.
/// </summary>
public static class PrestigeService
{
    public const string BoardId = "prestige";
    /// <summary>Max extra enemies a prestige loop adds to a level (keeps levels winnable).</summary>
    public const int MaxAddedEnemies = 4;

    /// <summary>Can the player ascend? Only after the final campaign boss is cleared.</summary>
    public static bool CanAscend(Profile p) => p.CompletedLevels.Contains("heaven-boss");

    /// <summary>Ascend into the next prestige loop: wipe campaign progress (the loop restarts), keep all
    /// meta (cards, modules, currencies, account level), and bump the tier. Returns the new tier.</summary>
    public static int Ascend(Profile p)
    {
        if (!CanAscend(p)) return p.PrestigeTier;
        p.CompletedLevels.RemoveAll(IsCampaignNode);
        p.PrestigeTier++;
        return p.PrestigeTier;
    }

    /// <summary>Reward scaling: +50% per prestige tier (plan §B.1 "scaling rewards").</summary>
    public static int ScaleReward(int baseAmount, int tier) => baseAmount + baseAmount * tier / 2;

    /// <summary>Competitive standing for the prestige board: tier dominates, progress within the loop breaks ties.</summary>
    public static int PrestigeScore(int tier, int nodesClearedThisLoop) => tier * 1000 + System.Math.Min(999, nodesClearedThisLoop);

    /// <summary>
    /// Apply a prestige loop's mutations to a freshly-built campaign battle: harden every destructible
    /// block by the tier (reused ApplyTier), then sprinkle a few biome-appropriate enemies into free cells.
    /// Conservative by design — only +HP and added (non-gating) enemies, so levels stay winnable.
    /// </summary>
    public static void ApplyMutators(GameInstance g, int tier, int seed)
    {
        if (tier <= 0) return;
        DungeonService.ApplyTier(g, tier); // +tier HP on every destructible block (the NG+ "+1 per restart")
        g._ngSpeedBonus = 50 * tier;       // NG+ ball speed: +50 to both base and cap per loop (2026-06-16)

        int toAdd = System.Math.Min(MaxAddedEnemies, tier);
        if (toAdd <= 0) return;
        var rng = new Rng(seed ^ unchecked(tier * 0x51ed270b));
        int cols = g.Level.Grid.Cols, rows = System.Math.Min(g.Level.Grid.Rows, 5);
        int added = 0, guard = 0;
        while (added < toAdd && guard++ < 200)
        {
            int c = rng.Range(cols), r = rng.Range(rows);
            if (g.BlockAt(c, r) != null) continue;
            g.Blocks.Add(new Entities.Block
            {
                Id = g.NextBlockId(), Col = c, Row = r,
                Hp = 4 + tier, MaxHp = 4 + tier, TypeId = "prestige_enemy", Sprite = "Beholder1",
                NeedToKill = false, // optional harasser — never gates the clear, so the level stays winnable
                Behavior = Entities.BlockBehavior.Emitter, EmitInterval = 1.6, EmitAim = "ball", MissileKind = "beholdermissile",
            });
            added++;
        }
        g._log.Log(g.TickCount, "prestige", "mutators applied", $"tier={tier} enemies={added}");
    }

    private static bool IsCampaignNode(string id) =>
        id.StartsWith("hell") || id.StartsWith("caverns") || id.StartsWith("village") || id.StartsWith("heaven");
}
