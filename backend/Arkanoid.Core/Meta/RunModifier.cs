using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// One place that maps a meta "effect" (from a Card or a Module) onto a <see cref="GameInstance"/> at run
/// start, reusing the proven item/relic modifier hooks. Shared by <see cref="CardEffects"/> and
/// <see cref="ModuleEffects"/> so a "+ball damage" means the same thing everywhere.
/// </summary>
public static class RunModifier
{
    public static void Apply(string effect, double magnitude, string effectValue, GameInstance game)
    {
        switch (effect)
        {
            case "ball_damage":   game.ItemBallDamageBonus    += (int)magnitude; break;
            case "max_mana":      game.ItemMaxManaBonus       += magnitude;      break;
            case "start_mana":    game.ManaValue              += magnitude;      break;
            case "kill_mana":     game.ItemKillManaMultBonus  += magnitude;      break;
            case "crit_tough":    game.ItemCritToughBonus     += (int)magnitude; break;
            case "crystal_bonus": game.ItemCrystalBonus       += (int)magnitude; break;
            case "start_life":    game.Hp                     += (int)magnitude; break;
            case "paddle_mod":    if (!string.IsNullOrEmpty(effectValue)) game.AddPaddleMod(effectValue); break;
            case "ball_core":     if (!string.IsNullOrEmpty(effectValue)) game.AddBallCore(effectValue);  break;
        }
    }
}
