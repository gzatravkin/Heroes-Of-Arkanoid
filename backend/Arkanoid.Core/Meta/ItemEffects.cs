using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure functions that compute aggregate bonuses from a set of equipped items.
/// Used by GameSession (and tests) to apply item passives to a GameInstance.
/// </summary>
public static class ItemEffects
{
    /// <summary>
    /// Apply all equipped-item effects from <paramref name="profile"/> onto <paramref name="game"/>.
    /// Must be called after the GameInstance is created and before the first tick.
    /// </summary>
    public static void Apply(IReadOnlyList<string> equippedItems,
                             IReadOnlyDictionary<string, int> ownedItems,
                             ItemCatalog catalog,
                             GameInstance game)
    {
        foreach (var itemId in equippedItems)
        {
            if (!ownedItems.TryGetValue(itemId, out var tier) || tier < 1) continue;
            if (!catalog.TryGet(itemId, out var def)) continue;
            ApplyOne(def, tier, game);
        }
    }

    private static void ApplyOne(ItemDef def, int tier, GameInstance game)
    {
        switch (def.Effect)
        {
            case "ball_damage":
                game.ItemBallDamageBonus += tier * game.Config.ItemBallDamageBonusPerTier;
                break;

            case "max_mana":
                var manaBonus = def.Id == "torch"
                    ? tier * game.Config.ItemMaxManaBonusSmallPerTier
                    : tier * game.Config.ItemMaxManaBonusPerTier;
                game.ItemMaxManaBonus += manaBonus;
                break;

            case "mana_regen":
                var regenBonus = def.Id == "staff"
                    ? tier * game.Config.ItemManaRegenMultSmallPerTier
                    : tier * game.Config.ItemManaRegenMultPerTier;
                game.ItemManaRegenMultBonus += regenBonus;
                break;

            case "start_life":
                game.Lives += tier * game.Config.ItemStartLifeBonusPerTier;
                break;

            case "treasure":
                var crystalBonus = def.Id == "clover"
                    ? tier * game.Config.ItemTreasureBonusLargePerTier
                    : tier * game.Config.ItemTreasureBonusPerTier;
                game.ItemTreasureBonus += crystalBonus;
                break;

            case "crit_tough":
                game.ItemCritToughBonus += tier * game.Config.ItemCritToughBonusPerTier;
                break;

            case "kill_mana":
                var killManaBonus = def.Id == "orb"
                    ? tier * game.Config.ItemKillManaMultSmallPerTier
                    : tier * game.Config.ItemKillManaMultPerTier;
                game.ItemKillManaMultBonus += killManaBonus;
                break;

            case "paddle_width":
                var widthBonus = def.Id == "hourglass"
                    ? tier * game.Config.ItemPaddleWidthBonusSmallPerTier
                    : tier * game.Config.ItemPaddleWidthBonusPerTier;
                game.Paddle.Width += widthBonus;
                break;
        }
    }

    /// <summary>
    /// Commit pending ManaMax and ManaRegenMult bonuses to the instance state.
    /// Call this AFTER all Apply calls so ManaMaxValue is set correctly.
    /// </summary>
    public static void Commit(GameInstance game)
    {
        game.ManaMaxValue += game.ItemMaxManaBonus;
    }
}
