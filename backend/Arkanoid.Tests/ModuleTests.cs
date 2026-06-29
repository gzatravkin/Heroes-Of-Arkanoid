using Arkanoid.Core.Meta;
using Arkanoid.Core.Sim;
using Xunit;

/// <summary>
/// Modules — collected like Cards (economy rework, docs/2026-06-14): owned (defId→level), duplicates level
/// (capped), one strong slot-bound passive each (no sub-stats/reroll). Tests assert ownership/equip + the
/// run-start apply (legacy stat effect via RunModifier; §2 "module" effect registered for ModuleSystem).
/// </summary>
public class ModuleTests
{
    private static ModuleCatalog Catalog() => ModuleCatalog.FromJson("""
    { "modules": [
      { "id": "razor_ball", "name": "Razor Ball", "slot": "ball",  "rarity": "rare", "effect": "ball_damage", "magnitude": 1 },
      { "id": "wide_mod",   "name": "Wide Frame", "slot": "paddle","rarity": "rare", "effect": "paddle_mod", "effectValue": "mod_wide" },
      { "id": "tidal_core", "name": "Tidal Core", "slot": "core",  "rarity": "epic", "effect": "module" }
    ]}
    """);

    [Fact]
    public void Grant_BanksCopies_AndManualLevelUp_SpendsThreshold()
    {
        // Owner direction 2026-06-15: duplicates BANK copies; the player levels up manually for 2L+1 copies.
        var p = new Profile();
        ModuleService.Grant(p, "razor_ball");                       // first copy → owned L1, 0 banked
        Assert.Equal(1, p.OwnedModules["razor_ball"]);
        Assert.Equal(0, p.ModuleCopies.GetValueOrDefault("razor_ball"));

        Assert.False(ModuleService.TryLevelUp(p, "razor_ball"));     // L1→L2 needs 3 copies
        ModuleService.Grant(p, "razor_ball");
        ModuleService.Grant(p, "razor_ball");
        ModuleService.Grant(p, "razor_ball");
        Assert.Equal(3, p.ModuleCopies["razor_ball"]);
        Assert.True(ModuleService.TryLevelUp(p, "razor_ball"));       // spend 3 → L2
        Assert.Equal(2, p.OwnedModules["razor_ball"]);
        Assert.Equal(0, p.ModuleCopies["razor_ball"]);
    }

    [Fact]
    public void Equip_ByDefId_PlacesInSlot_AndReplaces_RespectsOwnership()
    {
        var cat = Catalog(); var p = new Profile();
        Assert.False(ModuleService.Equip(p, cat, "razor_ball")); // not owned
        ModuleService.Grant(p, "razor_ball");
        ModuleService.Grant(p, "wide_mod");
        Assert.True(ModuleService.Equip(p, cat, "razor_ball"));
        Assert.Equal("razor_ball", p.EquippedModules["ball"]);
        Assert.True(ModuleService.Equip(p, cat, "wide_mod"));
        Assert.Equal("wide_mod", p.EquippedModules["paddle"]); // different slot
    }

    [Fact]
    public void ModuleEffects_AppliesLegacyStatMain_ScaledByLevel()
    {
        var cat = Catalog(); var p = new Profile { OwnedModules = new() { ["razor_ball"] = 3 } };
        ModuleService.Equip(p, cat, "razor_ball");
        var g = K.OneBlock(5);
        int before = g.ItemBallDamageBonus;
        ModuleEffects.Apply(p, cat, g);
        Assert.Equal(before + 3, g.ItemBallDamageBonus); // magnitude 1 × level 3
    }

    [Fact]
    public void ModuleEffects_PaddleModule_WidensPaddle()
    {
        var cat = Catalog(); var p = new Profile { OwnedModules = new() { ["wide_mod"] = 1 } };
        ModuleService.Equip(p, cat, "wide_mod");
        var g = K.OneBlock(5);
        double before = g.Paddle.Width;
        ModuleEffects.Apply(p, cat, g);
        Assert.True(g.Paddle.Width > before);
    }

    [Fact]
    public void ModuleEffects_RegistersSlotPassive_AtLevel_ForModuleSystem()
    {
        // A §2 "module"-effect module is registered (not RunModifier'd) so ModuleSystem can run it.
        var cat = Catalog(); var p = new Profile { OwnedModules = new() { ["tidal_core"] = 4 } };
        ModuleService.Equip(p, cat, "tidal_core");
        var g = K.OneBlock(5);
        ModuleEffects.Apply(p, cat, g);
        Assert.True(g.HasModule("tidal_core"));
        Assert.Equal(4, g.ModuleLevel("tidal_core"));
    }
}
