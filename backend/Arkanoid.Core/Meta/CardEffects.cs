using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Applies a profile's EQUIPPED cards onto a fresh <see cref="GameInstance"/> at run start (plan §A.1).
/// Reuses the proven item/relic modifier hooks so card power is real and tested. Call after the instance
/// is built and before the first tick (same contract as <see cref="ItemEffects"/>).
/// </summary>
public static class CardEffects
{
    public static void Apply(Profile profile, CardCatalog catalog, GameInstance game)
    {
        var active = new Dictionary<string, int>();
        foreach (var cardId in profile.EquippedCards)
        {
            if (!profile.OwnedCards.TryGetValue(cardId, out var own) || own.Level < 1) continue; // must be owned
            if (!catalog.TryGet(cardId, out var def)) continue;
            active[cardId] = own.Level;
            ApplyOne(def, own.Level, game);
        }
        // §1 Cards: register the equipped set so the in-play card triggers (CardSystem) can fire.
        game.SetCards(active);
        // NOTE: the caller commits ManaMax once after item + card effects (don't double-commit here).
    }

    /// <summary>§1 trigger cards carry effect "trigger" — their behaviour lives in CardSystem at the sim
    /// hook points, not as a run-start modifier. Legacy stat cards (ball_damage, max_mana, …) still apply
    /// their run-start RunModifier here.</summary>
    private static void ApplyOne(CardDef def, int level, GameInstance game)
    {
        if (def.Effect is "trigger" or "") return; // §1 cards apply via CardSystem, not RunModifier
        RunModifier.Apply(def.Effect, def.Magnitude * level, def.EffectValue, game);
    }
}
