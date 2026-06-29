using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Shared finalizer for the passive-stat channel on <see cref="GameInstance"/> (the <c>Item*Bonus</c>
/// accumulators). Cards, Modules, live events and rift modifiers all add into those fields; this
/// commits the accumulated ManaMax into the live mana pool after every passive layer has applied.
/// (The standalone item shop was removed in the 2026-06-15 economy rework; the accumulator name is kept.)
/// </summary>
public static class ItemEffects
{
    /// <summary>
    /// Commit pending ManaMax bonuses to the instance state. Call AFTER all passive layers
    /// (cards, modules, events) have applied so ManaMaxValue is set correctly.
    /// </summary>
    public static void Commit(GameInstance game)
    {
        game.ManaMaxValue += game.ItemMaxManaBonus;
    }
}
