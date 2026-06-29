namespace Arkanoid.Core.Meta;

/// <summary>
/// Pure module ownership/equip logic. Manual copy-threshold leveling (owner direction 2026-06-15): the first
/// copy owns the module at level 1; duplicates BANK copies (<see cref="Profile.ModuleCopies"/>); the player
/// spends copies to level up (<see cref="Leveling"/>). One strong slot-bound passive each, applied by ModuleEffects.
/// </summary>
public static class ModuleService
{
    public const int MaxModuleLevel = 5;

    /// <summary>Grant a module copy: first copy → owned at level 1; a duplicate → +1 banked copy (shared rule).</summary>
    public static void Grant(Profile p, string defId)
        => Leveling.GrantCopy(p.OwnedModules, p.ModuleCopies, defId);

    /// <summary>Spend banked copies to raise a module one level (shared rule in <see cref="Leveling"/>).</summary>
    public static bool TryLevelUp(Profile p, string defId)
        => Leveling.TryLevelUp(p.OwnedModules, p.ModuleCopies, defId, MaxModuleLevel);

    /// <summary>Equip an owned module into its def's slot (replaces whatever was there). Fails if not owned.</summary>
    public static bool Equip(Profile p, ModuleCatalog catalog, string defId)
    {
        if (!p.OwnedModules.ContainsKey(defId) || !catalog.TryGet(defId, out var def)) return false;
        p.EquippedModules[def.Slot] = defId;
        return true;
    }

    public static bool Unequip(Profile p, string slot) => p.EquippedModules.Remove(slot);
}
