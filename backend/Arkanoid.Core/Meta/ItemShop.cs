namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure meta logic for buying, equipping, and unequipping items.
/// All methods mutate <see cref="Profile"/> in place; callers persist the profile.
/// </summary>
public static class ItemShop
{
    public const int MaxEquipped = 3;

    /// <summary>
    /// Attempt to purchase (or upgrade) <paramref name="itemId"/> to the next tier.
    /// Returns <c>true</c> on success; <c>false</c> if already at max tier or insufficient crystals.
    /// </summary>
    public static bool TryBuy(Profile profile, ItemCatalog catalog, string itemId)
    {
        if (!catalog.TryGet(itemId, out var def)) return false;

        var ownedTier = profile.OwnedItems.GetValueOrDefault(itemId, 0);
        var nextTier  = ownedTier + 1;
        if (nextTier > def.MaxTier) return false;

        var cost = def.CostForTier(nextTier);
        if (profile.Crystals < cost) return false;

        profile.Crystals -= cost;
        profile.OwnedItems[itemId] = nextTier;
        return true;
    }

    /// <summary>
    /// Equip an item the player owns (tier ≥ 1). Fails silently when:
    /// • item not owned, • already equipped, • 3-slot limit reached.
    /// Returns <c>true</c> on change.
    /// </summary>
    public static bool Equip(Profile profile, string itemId)
    {
        if (profile.OwnedItems.GetValueOrDefault(itemId, 0) < 1) return false;
        if (profile.EquippedItems.Contains(itemId)) return false;
        if (profile.EquippedItems.Count >= MaxEquipped) return false;

        profile.EquippedItems.Add(itemId);
        return true;
    }

    /// <summary>Unequip an item. Returns <c>true</c> if it was equipped.</summary>
    public static bool Unequip(Profile profile, string itemId)
        => profile.EquippedItems.Remove(itemId);
}
