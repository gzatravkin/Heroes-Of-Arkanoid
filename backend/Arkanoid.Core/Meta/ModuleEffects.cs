using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>
/// Applies a profile's EQUIPPED modules onto a fresh <see cref="GameInstance"/> at run start. §2 modules are
/// one strong slot-bound PASSIVE each (no sub-stats): they carry effect "module" and run as bespoke
/// behaviours in <see cref="Sim.Systems.ModuleSystem"/> (registered via <see cref="GameInstance.SetModules"/>).
/// Legacy stat modules still apply their RunModifier main + substats here. Caller commits ManaMax.
/// </summary>
public static class ModuleEffects
{
    public static void Apply(Profile profile, ModuleCatalog catalog, GameInstance game)
    {
        var active = new Dictionary<string, int>();
        foreach (var kv in profile.EquippedModules)
        {
            var defId = kv.Value; // slot → equipped module def id
            if (!catalog.TryGet(defId, out var def)) continue;
            int level = profile.OwnedModules.TryGetValue(defId, out var l) ? System.Math.Max(1, l) : 1;

            active[def.Id] = level;

            // §2 modules apply via ModuleSystem at the sim hooks, NOT as a run-start RunModifier.
            if (def.Effect is "module" or "") continue;

            RunModifier.Apply(def.Effect, def.Magnitude * level, def.EffectValue, game);
        }
        game.SetModules(active); // register the equipped set so the in-play module passives can fire
    }
}
