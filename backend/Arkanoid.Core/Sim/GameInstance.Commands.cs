using Arkanoid.Core.Sim.Systems;
namespace Arkanoid.Core.Sim;

// Legacy per-spell command surface (Fire Mage input kinds + paddle-mod wiring).
// Kept separate so GameInstance.cs stays content-ID-free.
public sealed partial class GameInstance
{
    public void CastFireball()  => SpellSystem.Cast(this, GetSpellDef("fireball"));
    public void CastIgnite()    => SpellSystem.Cast(this, GetSpellDef("ignite"));
    public void CastFireWall()  => SpellSystem.Cast(this, GetSpellDef("firewall"));
    public void CastTurret()    => SpellSystem.Cast(this, GetSpellDef("turret"));
    public void CastPhoenix()   => SpellSystem.Cast(this, GetSpellDef("phoenix"));

    public void AddPaddleMod(string id)
    {
        if (!PaddleMods.Add(id)) return;
        _log.Log(TickCount, "paddlemod", "added", id);
        if (id == "mod_wide")      Paddle.Width *= 1.2;
        else if (id == "mod_grip") Paddle.DeflectAngleBonusDeg += 10;
    }
}
