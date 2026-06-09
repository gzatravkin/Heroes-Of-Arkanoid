namespace Arkanoid.Core.Entities;

public sealed class Block
{
    public int Id { get; init; }          // stable runtime id for snapshots
    public int Col { get; init; }
    public int Row { get; init; }
    public int Hp { get; set; }
    public int MaxHp { get; init; }
    public string TypeId { get; init; } = "";
    public string Sprite { get; init; } = "";
    public bool NeedToKill { get; init; }
    public bool Dead { get; set; }
    /// <summary>Ball/projectile/firewall damage is ignored; ball still bounces off it.</summary>
    public bool Indestructible { get; init; }
    /// <summary>Ball passes through with no collision/damage; projectiles and firewalls still damage it.</summary>
    public bool BallPhases { get; init; }
    /// <summary>Warps the ball to another teleporter of the same colour (Hell signature mechanic).</summary>
    public bool Teleporter { get; init; }
    /// <summary>Teleporter colour group (0 red / 1 blue / 2 green). Warps pair within a colour.</summary>
    public int  TeleportColor { get; init; }
    /// <summary>Boss block: periodically fires falling hazards that damage player HP on paddle contact.</summary>
    public bool Boss { get; init; }

    // --- Enemy emitter (Hell ball-spawner / Witchland beholder / Heaven melee statue) ---
    /// <summary>Periodically fires a hazard. Aim controlled by <see cref="EmitAim"/>.</summary>
    public bool   Emitter      { get; init; }
    /// <summary>Seconds between emitted hazards.</summary>
    public double EmitInterval { get; init; }
    /// <summary>"down" | "paddle" | "ball" — what the emitted hazard aims at.</summary>
    public string EmitAim      { get; init; } = "down";
    /// <summary>Runtime cadence accumulator (mutable).</summary>
    public double EmitAccumulator { get; set; }

    // --- Bomb: explodes on death, damaging blocks within ExplodeRadius cells (chains). ---
    public bool Bomb          { get; init; }
    public int  ExplodeRadius { get; init; }

    // --- Orientation: mirror asymmetric/corner art so it can sit at any corner/side. ---
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
}
