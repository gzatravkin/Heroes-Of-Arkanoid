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

    // --- Stalactite: hangs from the ceiling; detaches into a falling hazard when a ball
    //     passes beneath its column (Caverns hazard; the Goblin boss also drops these). ---
    public bool Stalactite { get; init; }

    // --- Necromant: while alive, revives destroyed normal blocks after a delay. ---
    public bool Necromant { get; init; }

    // --- WindMaster: pushes nearby balls away (deflects aim) within a radius. ---
    public bool WindMaster { get; init; }

    // --- Shield Statue: periodically grants nearby blocks a temporary damage shield. ---
    public bool   ShieldStatue { get; init; }
    /// <summary>Seconds of remaining damage-immunity granted by a shield statue (mutable).</summary>
    public double ShieldTimer  { get; set; }

    // --- Orientation: mirror asymmetric/corner art so it can sit at any corner/side. ---
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
}
