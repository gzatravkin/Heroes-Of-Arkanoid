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
    Reviver,       // revives destroyed normal blocks while alive (distinct from the Necromancer player class)
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
    BossVase,      // Seraph-summoned: self-shatters after a fuse and levels his adds; player kill defuses it
}

public sealed class Block
{
    public int Id { get; init; }          // stable runtime id for snapshots
    // Col/Row are mutable: the Goblin boss hops between anchors, and the Hell
    // descend pacing mode presses every block downward (docs/11 + docs/12).
    public int Col { get; set; }
    public int Row { get; set; }
    public int Hp { get; set; }
    // MaxHp is mutable: rift ascension tiers harden every destructible block.
    public int MaxHp { get; set; }
    public string TypeId { get; init; } = "";
    /// <summary>Effect tag this block guarantees on death (set from BlockType.ForcedDropEffect in JSON).</summary>
    public string? ForcedDropEffect { get; init; }
    public string Sprite { get; init; } = "";
    public bool NeedToKill { get; init; }
    public bool Dead { get; set; }

    /// <summary>Erosion (§1 card): ball hits accumulated against this INDESTRUCTIBLE block; it cracks once
    /// the threshold is reached (~16 hits, fewer at higher card level).</summary>
    public int ErosionHits { get; set; }

    /// <summary>Ball/projectile/firewall damage is ignored; ball still bounces off it.</summary>
    public bool Indestructible { get; init; }
    /// <summary>Ball passes through with no collision/damage; projectiles and firewalls still damage it.</summary>
    public bool BallPhases { get; init; }

    /// <summary>The single special behaviour this block has (see <see cref="BlockBehavior"/>).</summary>
    public BlockBehavior Behavior { get; init; } = BlockBehavior.None;

    /// <summary>Caverns "union-of-sticks": this block is part of a connected bridge. Adjacent union
    /// blocks share a <see cref="UnionGroup"/> and collapse together when any one is destroyed.</summary>
    public bool IsUnion { get; init; }
    /// <summary>Union group id (assigned at load by adjacency flood-fill; 0 = not in a union). Mutable.</summary>
    public int  UnionGroup { get; set; }

    // Readable, derived accessors — single source of truth is Behavior (no stored-bool soup).
    public bool Boss         => Behavior == BlockBehavior.Boss;
    public bool Teleporter   => Behavior == BlockBehavior.Teleporter;
    public bool Emitter      => Behavior == BlockBehavior.Emitter || ForcedEmitter;
    /// <summary>§8 Cursed Bounty: a normal block forced to also fire hazards (the modifier's downside), set at
    /// rift battle start. Honoured by EmitterSystem alongside the behaviour-driven emitters.</summary>
    public bool ForcedEmitter { get; set; }
    public bool Bomb         => Behavior == BlockBehavior.Bomb;
    public bool Stalactite   => Behavior == BlockBehavior.Stalactite;
    public bool Reviver      => Behavior == BlockBehavior.Reviver;
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
    public bool BossVase     => Behavior == BlockBehavior.BossVase;
    /// <summary>True for Heaven statues that the Altar/Vase can pacify.</summary>
    public bool IsStatue     => (Emitter && EmitAim == "paddle") || ShieldStatue;

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
    public double ImmunityTimer     { get; set; }
    /// <summary>Stalactite: seconds left in its shake/telegraph before it detaches and falls; -1 = not armed (mutable).</summary>
    public double StalArmTimer      { get; set; } = -1;
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
    /// <summary>BossVase: seconds until it self-shatters and levels the Seraph's adds (mutable).</summary>
    public double FuseTimer       { get; set; }

    /// <summary>Lich's Gaze (§3): cursed by the beam — the player's ball deals bonus damage to it. Mutable.</summary>
    public bool Cursed { get; set; }

    // --- Fire-Mage burn state (ignite DoT + ring-by-ring spread; see FireConfig). ---
    /// <summary>Seconds of burning remaining (0 = not on fire). Mutable.</summary>
    public double BurnRemaining   { get; set; }
    /// <summary>Cadence accumulator between burn damage ticks. Mutable.</summary>
    public double BurnAccum        { get; set; }
    /// <summary>Spread generation: 0 = originally ignited, increments outward. Caps propagation. Mutable.</summary>
    public int    BurnGen          { get; set; }
    /// <summary>Cadence accumulator for the slow fire creep (separate from the burn-damage cadence). Mutable.</summary>
    public double BurnSpreadAccum  { get; set; }
    /// <summary>True once this burning block has lit its single chain-child (ignite creeps one block at a time). Mutable.</summary>
    public bool   BurnSpawned      { get; set; }

    // --- Orientation: mirror asymmetric/corner art so it can sit at any corner/side. ---
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
    /// <summary>5-HP elite tier — renders with a distinct cold tint (difficulty rework 2026-06-16).</summary>
    public bool Elite { get; init; }

    /// <summary>Shallow-clone all fields. Used by GameInstance to own its own block list
    /// so LevelData remains an immutable template that can be safely re-used or cached.</summary>
    public Block Clone() => new Block
    {
        Id = Id, TypeId = TypeId, ForcedDropEffect = ForcedDropEffect,
        Sprite = Sprite, NeedToKill = NeedToKill, Indestructible = Indestructible,
        BallPhases = BallPhases, Behavior = Behavior, TeleportColor = TeleportColor,
        IsUnion = IsUnion, UnionGroup = UnionGroup,
        EmitInterval = EmitInterval, EmitAim = EmitAim, ExplodeRadius = ExplodeRadius,
        MissileKind = MissileKind, FlipX = FlipX, FlipY = FlipY, Elite = Elite,
        BurnSpreadAccum = BurnSpreadAccum, BurnSpawned = BurnSpawned,
        Col = Col, Row = Row, Hp = Hp, MaxHp = MaxHp, Dead = Dead,
        EmitAccumulator = EmitAccumulator, ImmunityTimer = ImmunityTimer, AllyTimer = AllyTimer,
        StatueLevel = StatueLevel, StoredMana = StoredMana, OwnerId = OwnerId,
        SpawnedCount = SpawnedCount, FuseTimer = FuseTimer,
    };
}
