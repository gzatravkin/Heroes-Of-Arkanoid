namespace Arkanoid.Core.Meta;

/// <summary>Pure card ownership/equip logic. Manual copy-threshold leveling (owner direction 2026-06-15):
/// the first copy grants level 1; duplicates BANK copies; the player spends copies to level up (<see cref="Leveling"/>).</summary>
public static class CardService
{
    public const int MaxCardLevel = 10;

    /// <summary>Grant a card copy: first copy → owned at level 1; a duplicate → +1 banked copy.</summary>
    public static void Grant(Profile p, string cardId)
    {
        if (p.OwnedCards.TryGetValue(cardId, out var own))
            own.Copies++;                                 // duplicate → bank a copy (manual level-up)
        else
            p.OwnedCards[cardId] = new CardOwn { Level = 1, Copies = 0 };
    }

    /// <summary>Spend banked copies to raise a card one level (shared rule in <see cref="Leveling"/>).</summary>
    public static bool TryLevelUp(Profile p, string cardId)
        => p.OwnedCards.TryGetValue(cardId, out var own) && Leveling.TryLevelUp(own, MaxCardLevel);

    /// <summary>Equip a card. Owner direction 2026-06-15: when the slots are full, the OLDEST equipped card
    /// is dropped to make room (FIFO) so a new pick always succeeds. Fails only if unowned/already equipped.</summary>
    public static bool Equip(Profile p, string cardId)
    {
        if (!p.OwnedCards.ContainsKey(cardId)) return false;
        if (p.EquippedCards.Contains(cardId)) return false;
        Equipping.EquipFifo(p.EquippedCards, cardId, p.CardSlots);   // full → drop oldest (shared rule)
        return true;
    }

    public static bool Unequip(Profile p, string cardId) => p.EquippedCards.Remove(cardId);
}
