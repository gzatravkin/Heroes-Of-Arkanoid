namespace Arkanoid.Core.Entities;

/// <summary>
/// A block's special behaviour. A block has exactly ONE behaviour (mutually exclusive),
/// so this single enum replaces the former pile of independent booleans. Structural
/// properties (Indestructible / BallPhases / NeedToKill) and parametric fields
/// (EmitInterval, ExplodeRadius, TeleportColor, …) remain separate and orthogonal.
/// </summary>
public enum BlockBehavior
{
    None = 0,
    Boss,          // fires hazards via BossSystem
    Teleporter,    // warps the ball to a same-colour partner
    Emitter,       // periodically fires a hazard (Hell spawner / Beholder / Melee statue)
    Bomb,          // explodes on death, chaining to neighbours
    Stalactite,    // drops into a falling hazard when a ball passes beneath
    Necromant,     // revives destroyed normal blocks while alive
    WindMaster,    // pushes nearby balls away
    ShieldStatue,  // periodically shields nearby blocks
    Portal,        // toggles the ball's ghost phase
    Bat,           // grabs the ball, then flies away
    Lava,          // deadly block — drains the ball on contact
    Altar,         // ball-hit pacifies (allies) the Heaven statues for a while
    Vase,          // on death, pacifies the Heaven statues
    Cart,          // periodically rolls a cart hazard across the board (Caverns)
    Cauldron,      // siphons the player's mana while alive; refunds it on death (Witchland)
    LavaSpawner,   // creeps lava into adjacent empty cells; killing it retracts its lava (Hell)
}

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

    /// <summary>The single special behaviour this block has (see <see cref="BlockBehavior"/>).</summary>
    public BlockBehavior Behavior { get; init; } = BlockBehavior.None;

    // Readable, derived accessors — single source of truth is Behavior (no stored-bool soup).
    public bool Boss         => Behavior == BlockBehavior.Boss;
    public bool Teleporter   => Behavior == BlockBehavior.Teleporter;
    public bool Emitter      => Behavior == BlockBehavior.Emitter;
    public bool Bomb         => Behavior == BlockBehavior.Bomb;
    public bool Stalactite   => Behavior == BlockBehavior.Stalactite;
    public bool Necromant    => Behavior == BlockBehavior.Necromant;
    public bool WindMaster   => Behavior == BlockBehavior.WindMaster;
    public bool ShieldStatue => Behavior == BlockBehavior.ShieldStatue;
    public bool Portal       => Behavior == BlockBehavior.Portal;
    public bool Bat          => Behavior == BlockBehavior.Bat;
    public bool Lava         => Behavior == BlockBehavior.Lava;
    public bool Altar        => Behavior == BlockBehavior.Altar;
    public bool Vase         => Behavior == BlockBehavior.Vase;
    public bool Cart         => Behavior == BlockBehavior.Cart;
    public bool Cauldron     => Behavior == BlockBehavior.Cauldron;
    public bool LavaSpawner  => Behavior == BlockBehavior.LavaSpawner;
    /// <summary>True for Heaven statues that the Altar/Vase can pacify.</summary>
    public bool IsStatue     => Emitter && EmitAim == "paddle" || ShieldStatue;

    // --- Parametric fields (read by the relevant system for the matching behaviour) ---
    /// <summary>Teleporter colour group (0 red / 1 blue / 2 green). Warps pair within a colour.</summary>
    public int    TeleportColor   { get; init; }
    /// <summary>Emitter: seconds between emitted hazards.</summary>
    public double EmitInterval    { get; init; }
    /// <summary>Emitter: "down" | "paddle" | "ball" — what the emitted hazard aims at.</summary>
    public string EmitAim         { get; init; } = "down";
    /// <summary>Emitter/Shield: runtime cadence accumulator (mutable).</summary>
    public double EmitAccumulator { get; set; }
    /// <summary>Bomb: explosion radius in cells.</summary>
    public int    ExplodeRadius   { get; init; }
    /// <summary>Emitter: hazard kind tag — tells the renderer which missile art to use.</summary>
    public string MissileKind     { get; init; } = "";
    /// <summary>Shield: seconds of remaining damage-immunity granted to this block (mutable).</summary>
    public double ShieldTimer     { get; set; }
    /// <summary>Statue: seconds remaining ALLIED by an Altar — fights for the player (mutable).</summary>
    public double AllyTimer        { get; set; }
    /// <summary>Statue: permanent level-ups from broken Vases — faster fire, bigger kill reward (mutable).</summary>
    public int    StatueLevel     { get; set; }
    /// <summary>Cauldron: mana siphoned from the player so far — refunded when it dies (mutable).</summary>
    public double StoredMana      { get; set; }
    /// <summary>Runtime-spawned block (creeping lava): id of the spawner that created it (mutable).</summary>
    public int    OwnerId         { get; set; }
    /// <summary>LavaSpawner: how many lava cells it has crept so far (mutable).</summary>
    public int    SpawnedCount    { get; set; }

    // --- Orientation: mirror asymmetric/corner art so it can sit at any corner/side. ---
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
}
