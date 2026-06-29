using System;
using System.Collections.Generic;

namespace Arkanoid.Core.Meta;

/// <summary>
/// Shared equip rule (owner direction 2026-06-15): equipping into a full, capped list DROPS THE OLDEST
/// droppable entry (FIFO) so a new pick always succeeds. Used by cards (flat list) and the spell hotbar
/// (where the signature at slot 0 is never droppable). Modules don't use this — they replace per slot.
/// </summary>
public static class Equipping
{
    /// <summary>Append <paramref name="id"/> to <paramref name="equipped"/>, first dropping the oldest entry
    /// for which <paramref name="droppable"/> is true (default: any) until under <paramref name="cap"/>.
    /// Assumes <paramref name="id"/> is not already present and is allowed to be equipped.</summary>
    public static void EquipFifo(List<string> equipped, string id, int cap, Func<string, bool>? droppable = null)
    {
        while (cap > 0 && equipped.Count >= cap)
        {
            int oldest = droppable == null ? 0 : equipped.FindIndex(x => droppable(x));
            if (oldest < 0) break;            // nothing droppable (e.g. only the locked signature remains)
            equipped.RemoveAt(oldest);
        }
        equipped.Add(id);
    }
}
