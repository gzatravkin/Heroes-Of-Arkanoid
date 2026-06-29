using System.Text.Json.Serialization;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Relics;
namespace Arkanoid.Core.Net;

public sealed class BallDto
{
    [JsonPropertyName("id")]      public int    Id      { get; set; }
    [JsonPropertyName("x")]       public double X       { get; set; }
    [JsonPropertyName("y")]       public double Y       { get; set; }
    [JsonPropertyName("ignited")] public bool   Ignited { get; set; }
    [JsonPropertyName("decayed")] public bool   Decayed { get; set; }
    [JsonPropertyName("ghost")]   public bool   Ghost   { get; set; }
    [JsonPropertyName("summoned")]public bool   Summoned{ get; set; }
    /// <summary>Ball radius relative to the base ball (1.0 = normal). Lets the renderer draw
    /// duplicated/smaller balls at their true size (docs/01 §61) instead of a fixed size.</summary>
    [JsonPropertyName("radiusScale")] public double RadiusScale { get; set; } = 1.0;
}

public sealed class ProjectileDto
{
    [JsonPropertyName("id")]   public int    Id   { get; set; }
    [JsonPropertyName("x")]    public double X    { get; set; }
    [JsonPropertyName("y")]    public double Y    { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
}

public sealed class BlockDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("maxHp")] public int MaxHp { get; set; }
    [JsonPropertyName("sprite")] public string Sprite { get; set; } = "";
    [JsonPropertyName("ballPhases")] public bool BallPhases { get; set; }
    [JsonPropertyName("teleporter")] public bool Teleporter { get; set; }
    [JsonPropertyName("indestructible")] public bool Indestructible { get; set; }
    [JsonPropertyName("boss")] public bool Boss { get; set; }
    [JsonPropertyName("flipX")] public bool FlipX { get; set; }
    [JsonPropertyName("flipY")] public bool FlipY { get; set; }
    [JsonPropertyName("shielded")] public bool Shielded { get; set; }
    /// <summary>Emitter about to fire — the renderer shows the attack tell (docs/11 R2).</summary>
    [JsonPropertyName("charging")] public bool Charging { get; set; }
    /// <summary>Statue pacified by an Altar/Vase — renderer swaps to the *Active art.</summary>
    [JsonPropertyName("allied")] public bool Allied { get; set; }
    /// <summary>Statue level from broken Vases — renderer tints levelled statues.</summary>
    [JsonPropertyName("level")] public int Level { get; set; }
    /// <summary>Block is currently on fire (ignite/firewall burn DoT) — renderer overlays flames.</summary>
    [JsonPropertyName("burning")] public bool Burning { get; set; }
    /// <summary>Cursed by Lich's Gaze (§3) — the renderer tints it; the ball deals bonus damage.</summary>
    [JsonPropertyName("cursed")] public bool Cursed { get; set; }
    /// <summary>Caverns union-of-sticks bridge block — renderer tints it so the linkage reads.</summary>
    [JsonPropertyName("union")] public bool Union { get; set; }
    /// <summary>5-HP elite tier — renderer tints it cold/steel so it reads as extra-tough (2026-06-16).</summary>
    [JsonPropertyName("elite")] public bool Elite { get; set; }
}

public sealed class HazardDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    /// <summary>True while the hazard is in its telegraph/warm-up (renderer flashes it as a warning).</summary>
    [JsonPropertyName("warming")] public bool Warming { get; set; }
}

public sealed class PhoenixDto
{
    [JsonPropertyName("id")]    public int    Id    { get; set; }
    [JsonPropertyName("x")]     public double X     { get; set; }
    [JsonPropertyName("y")]     public double Y     { get; set; }
    [JsonPropertyName("angle")] public double Angle { get; set; }
}

public sealed class WallDto
{
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("width")] public double Width { get; set; }
}

public sealed class BarrierDto
{
    [JsonPropertyName("y")]       public double Y       { get; set; }
    [JsonPropertyName("centerX")] public double CenterX { get; set; }
    [JsonPropertyName("width")]   public double Width   { get; set; }
}

public sealed class ZoneDto
{
    [JsonPropertyName("x")]      public double X      { get; set; }
    [JsonPropertyName("y")]      public double Y      { get; set; }
    [JsonPropertyName("radius")] public double Radius { get; set; }
}

public sealed class EventDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    /// <summary>Optional integer payload — only serialized when non-zero (e.g. boss phase number).</summary>
    [JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("extra")] public int Extra { get; set; }
}

public sealed class PillarDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("w")] public double W { get; set; }
    [JsonPropertyName("h")] public double H { get; set; }
}

public sealed class LichBeamDto
{
    [JsonPropertyName("x")]     public double X     { get; set; }
    [JsonPropertyName("y")]     public double Y     { get; set; }
    [JsonPropertyName("angle")] public double Angle { get; set; }
    [JsonPropertyName("len")]   public double Len   { get; set; }
}

public sealed class TwinTetherDto
{
    [JsonPropertyName("x1")] public double X1 { get; set; }
    [JsonPropertyName("y1")] public double Y1 { get; set; }
    [JsonPropertyName("x2")] public double X2 { get; set; }
    [JsonPropertyName("y2")] public double Y2 { get; set; }
}

public sealed class MinionDto
{
    [JsonPropertyName("id")]    public int    Id    { get; set; }
    [JsonPropertyName("kind")]  public string Kind  { get; set; } = ""; // "bonewalker" | "golem"
    [JsonPropertyName("x")]     public double X     { get; set; }
    [JsonPropertyName("y")]     public double Y     { get; set; }
    [JsonPropertyName("w")]     public double W     { get; set; }
    [JsonPropertyName("h")]     public double H     { get; set; }
    /// <summary>Golem fire-soak (current/max) — the renderer draws a soak pip. 0/0 for the bonewalker.</summary>
    [JsonPropertyName("hp")]    public int    Hp    { get; set; }
    [JsonPropertyName("maxHp")] public int    MaxHp { get; set; }
    /// <summary>Bonewalker walk-duration remaining as a 0..1 fraction — the renderer draws a life bar. 0 for the golem.</summary>
    [JsonPropertyName("lifeFrac")] public double LifeFrac { get; set; }
}

public sealed class RelicDto
{
    [JsonPropertyName("id")]   public string Id   { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
}

public sealed class ActiveEffectDto
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("timeLeft")] public double TimeLeft { get; set; }
}

public sealed class BonusDto
{
    [JsonPropertyName("id")]   public int    Id   { get; set; }
    [JsonPropertyName("x")]    public double X    { get; set; }
    [JsonPropertyName("y")]    public double Y    { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
}

public sealed class LoadoutSlotDto
{
    [JsonPropertyName("id")]        public string Id        { get; set; } = "";
    [JsonPropertyName("name")]      public string Name      { get; set; } = "";
    [JsonPropertyName("icon")]      public string Icon      { get; set; } = "";
    [JsonPropertyName("manaCost")]  public int    ManaCost  { get; set; }
    [JsonPropertyName("level")]     public int    Level     { get; set; }
    [JsonPropertyName("signature")] public bool   Signature { get; set; }
}

/// <summary>One §8 rift modifier offered in the mid-floor draft (id + name + one-line desc).</summary>
public sealed class RiftChoiceDto
{
    [JsonPropertyName("id")]   public string Id   { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("desc")] public string Desc { get; set; } = "";
}

public sealed class Snapshot
{
    [JsonPropertyName("tick")] public long Tick { get; set; }
    [JsonPropertyName("phase")] public string Phase { get; set; } = "";
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("spareBalls")] public int SpareBalls { get; set; }
    [JsonPropertyName("mana")] public double Mana { get; set; }
    [JsonPropertyName("manaMax")] public double ManaMax { get; set; }
    [JsonPropertyName("boardW")] public double BoardW { get; set; }
    [JsonPropertyName("boardH")] public double BoardH { get; set; }
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
    [JsonPropertyName("paddleX")] public double PaddleX { get; set; }
    [JsonPropertyName("paddleW")] public double PaddleW { get; set; }
    [JsonPropertyName("paddleH")] public double PaddleH { get; set; }
    /// <summary>True during post-hit i-frames — the renderer flashes the paddle so the player reads invulnerability.</summary>
    [JsonPropertyName("paddleInvuln")] public bool PaddleInvuln { get; set; }
    [JsonPropertyName("cellSize")] public double CellSize { get; set; }
    [JsonPropertyName("cols")]     public int    Cols      { get; set; }
    [JsonPropertyName("rows")]     public int    Rows      { get; set; }
    [JsonPropertyName("balls")]        public List<BallDto>        Balls       { get; set; } = new();
    [JsonPropertyName("projectiles")] public List<ProjectileDto> Projectiles { get; set; } = new();
    [JsonPropertyName("blocks")] public List<BlockDto> Blocks { get; set; } = new();
    [JsonPropertyName("walls")] public List<WallDto> Walls { get; set; } = new();
    [JsonPropertyName("turretActive")]  public bool TurretActive { get; set; }
    /// <summary>Tesla Grid (§3 Engineer): armed=spell is active this level; left/right=wall charged.</summary>
    [JsonPropertyName("teslaArmed")]        public bool TeslaArmed        { get; set; }
    [JsonPropertyName("teslaLeftCharged")]  public bool TeslaLeftCharged  { get; set; }
    [JsonPropertyName("teslaRightCharged")] public bool TeslaRightCharged { get; set; }
    [JsonPropertyName("activeRelics")]  public List<RelicDto> ActiveRelics { get; set; } = new();
    [JsonPropertyName("hazards")] public List<HazardDto> Hazards { get; set; } = new();
    [JsonPropertyName("phoenixes")] public List<PhoenixDto> Phoenixes { get; set; } = new();
    [JsonPropertyName("events")] public List<EventDto> Events { get; set; } = new();
    [JsonPropertyName("bossActive")] public bool BossActive { get; set; }
    [JsonPropertyName("bossHp")]     public int  BossHp     { get; set; }
    [JsonPropertyName("bossMaxHp")]  public int  BossMaxHp  { get; set; }
    [JsonPropertyName("bonuses")]    public List<BonusDto> Bonuses { get; set; } = new();
    [JsonPropertyName("widePaddleActive")] public bool WidePaddleActive { get; set; }
    [JsonPropertyName("widePaddleTimer")]  public double WidePaddleTimer  { get; set; }
    [JsonPropertyName("slowBallActive")]   public bool SlowBallActive   { get; set; }
    [JsonPropertyName("slowBallTimer")]    public double SlowBallTimer    { get; set; }
    [JsonPropertyName("barriers")]        public List<BarrierDto> Barriers { get; set; } = new();
    [JsonPropertyName("zones")]           public List<ZoneDto>    Zones    { get; set; } = new();
    [JsonPropertyName("skeletonActive")]  public bool SkeletonActive  { get; set; }
    [JsonPropertyName("spellDrainActive")]     public bool SpellDrainActive     { get; set; }
    /// <summary>Extra crystals to be awarded at level completion (treasure-item bonus).</summary>
    [JsonPropertyName("crystalBonus")]    public int  CrystalBonus    { get; set; }
    /// <summary>In-run Gold (docs/04 §5) — spending currency shown in the HUD; spent at shop floors.</summary>
    [JsonPropertyName("gold")]            public int  Gold            { get; set; }
    /// <summary>WindMaster push radius in world units — the renderer draws the aura circle at this size.</summary>
    [JsonPropertyName("windRadius")]      public double WindRadius    { get; set; }
    /// <summary>Objective timer mode: "" | "survive" (win at 0) | "limit" (lose at 0).</summary>
    [JsonPropertyName("timerMode")]       public string TimerMode     { get; set; } = "";
    /// <summary>Seconds remaining on the objective timer (0 when no timer).</summary>
    [JsonPropertyName("timeLeft")]        public double TimeLeft      { get; set; }
    /// <summary>Multi-floor collapse: current floor (1-based) and total floors (1 = single).</summary>
    [JsonPropertyName("floor")]           public int    Floor         { get; set; }
    [JsonPropertyName("floorCount")]      public int    FloorCount    { get; set; }
    /// <summary>Continuous Rift (2026-06-16): the HUD shows depth + the banked depth-reward + the next milestone.</summary>
    [JsonPropertyName("isRift")]            public bool IsRift            { get; set; }
    [JsonPropertyName("riftReward")]        public int  RiftReward        { get; set; }
    [JsonPropertyName("riftNextMilestone")] public int  RiftNextMilestone { get; set; }
    /// <summary>§8 mid-rift draft: while true the sim is frozen and the player must pick one of DraftChoices.</summary>
    [JsonPropertyName("awaitingDraft")]     public bool AwaitingDraft     { get; set; }
    [JsonPropertyName("draftChoices")]      public System.Collections.Generic.List<RiftChoiceDto> DraftChoices { get; set; } = new();
    /// <summary>Total destructible blocks destroyed this level — drives the speed-escalation HUD (docs plan 2026-06-10).</summary>
    [JsonPropertyName("bricksDestroyedThisLevel")] public int BricksDestroyedThisLevel { get; set; }

    // --- Power-up active states (task 1.2) ---
    /// <summary>True while the Fire Shot power-up is active (ball destroys indestructible bricks).</summary>
    [JsonPropertyName("fireshotActive")] public bool   FireshotActive { get; set; }
    /// <summary>Seconds remaining on the Fire Shot power-up.</summary>
    [JsonPropertyName("fireshotTimer")]  public double FireshotTimer  { get; set; }
    /// <summary>True while the powerup_shield auto-save is armed (one-touch ball save, distinct from the Paladin barrier).</summary>
    [JsonPropertyName("shieldActive")]   public bool   AutoSaveActive { get; set; }

    // --- Combo multiplier (task 1.3) ---
    /// <summary>Current combo multiplier (1–4). >1 means the player has a consecutive-hit streak.</summary>
    [JsonPropertyName("comboMultiplier")] public int ComboMultiplier { get; set; } = 1;

    /// <summary>Reckoning (§3) meter fill 0..1 (0 when not armed) — the HUD shows the charge bar.</summary>
    [JsonPropertyName("reckoningCharge")] public double ReckoningCharge { get; set; }

    /// <summary>Lich's Gaze (§3) sweeping beam, or null — the renderer draws the ray.</summary>
    [JsonPropertyName("lichBeam")] public LichBeamDto? LichBeam { get; set; }

    /// <summary>Lance of Dawn (§3) temporary pillars the renderer draws as solid blocks.</summary>
    [JsonPropertyName("pillars")] public List<PillarDto> Pillars { get; set; } = new();

    /// <summary>Bonewalker / Bone Golem (§3) summoned minions the renderer draws as figures.</summary>
    [JsonPropertyName("minions")] public List<MinionDto> Minions { get; set; } = new();

    /// <summary>Twin Soul Core (§2) tether segment between the two twins, or null — the renderer draws it.</summary>
    [JsonPropertyName("twinTether")] public TwinTetherDto? TwinTether { get; set; }

    /// <summary>All currently active timed effects with their remaining duration.</summary>
    [JsonPropertyName("activeEffects")] public List<ActiveEffectDto> ActiveEffects { get; set; } = new();

    /// <summary>The equipped spell loadout, ordered (slot 0 = signature). Drives the HUD hotbar
    /// and CastSlot indexing; changes mid-run when a spell is drafted (docs/04 §3/§5).</summary>
    [JsonPropertyName("loadout")] public List<LoadoutSlotDto> Loadout { get; set; } = new();

    /// <summary>Dungeon miniboss floor (docs/04 §6.2) — the HUD shows a warning banner.</summary>
    [JsonPropertyName("minibossFloor")] public bool MinibossFloor { get; set; }

    /// <summary>
    /// Build a snapshot. When <paramref name="cachedBlocks"/> is non-null and the caller has
    /// verified BlockVersion is unchanged, block DTO objects are reused — no per-block allocation.
    /// </summary>
    public static Snapshot From(GameInstance g, long tick, List<BlockDto>? cachedBlocks = null)
    {
        var s = new Snapshot {
            Tick = tick, Phase = g.Phase.ToString(),
            Hp = g.Hp, SpareBalls = g.SpareBalls, Gold = g.Gold,
            Mana = g.ManaValue, ManaMax = g.ManaMaxValue,
            ReckoningCharge = Arkanoid.Core.Sim.Systems.ReckoningSystem.Charge(g),
            LichBeam = g.LichBeam is null ? null : new LichBeamDto
            {
                X = g.LichBeam.OriginX, Y = g.LichBeam.OriginY,
                Angle = g.LichBeam.Angle, Len = g.LichBeam.Length,
            },
            Pillars = g.Pillars.Where(p => p.Alive)
                .Select(p => new PillarDto { X = p.CenterX, Y = p.CenterY, W = p.Width, H = p.Height }).ToList(),
            Minions = g.Minions.Where(m => m.Alive)
                .Select(m => new MinionDto { Id = m.Id, Kind = m.Kind, X = m.X, Y = m.Y, W = m.Width, H = m.Height, Hp = m.Hp, MaxHp = m.MaxHp,
                    LifeFrac = m.MaxLife > 0 ? System.Math.Clamp(m.LifeRemaining / m.MaxLife, 0, 1) : 0 }).ToList(),
            TwinTether = g.TwinTether is { } tt ? new TwinTetherDto { X1 = tt.X1, Y1 = tt.Y1, X2 = tt.X2, Y2 = tt.Y2 } : null,
            BoardW = g.Level.Grid.Width, BoardH = g.Level.Grid.Height,
            Biome = g.Level.Biome,
            PaddleX = g.Paddle.Center.X, PaddleW = g.Paddle.Width, PaddleH = g.Paddle.Height, PaddleInvuln = g.DamageImmune,
            CellSize = g.Config.CellSize,
            Cols = g.Level.Grid.Cols,
            Rows = g.Level.Grid.Rows,
        };
        foreach (var b in g.Balls)
            s.Balls.Add(new BallDto { Id = b.Id, X = b.Pos.X, Y = b.Pos.Y, Ignited = b.IgniteHitsLeft > 0, Decayed = b.DecayHitsLeft > 0, Ghost = b.Ghost, Summoned = b.Summoned,
                RadiusScale = g.Config.BallRadius > 0 ? b.Radius / g.Config.BallRadius : 1.0 });
        foreach (var pr in g.Projectiles)
            s.Projectiles.Add(new ProjectileDto { Id = pr.Id, X = pr.Pos.X, Y = pr.Pos.Y, Kind = pr.Kind });
        if (cachedBlocks != null)
        {
            s.Blocks = cachedBlocks; // reuse caller's cached list — no new BlockDto allocations
        }
        else
        {
            foreach (var blk in g.Blocks)
            {
                if (blk.Dead) continue;
                var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
                var emitInterval = blk.EmitInterval > 0 ? blk.EmitInterval : g.Config.Enemies.DefaultEmitInterval;
                var charging = (blk.Emitter && blk.AllyTimer <= 0
                        && emitInterval - blk.EmitAccumulator <= g.Config.Enemies.EmitTelegraphWindow)
                    || (blk.Stalactite && blk.StalArmTimer >= 0);  // shaking before it drops
                s.Blocks.Add(new BlockDto { Id = blk.Id, X = c.X, Y = c.Y, Hp = blk.Hp, MaxHp = blk.MaxHp, Sprite = blk.Sprite, BallPhases = blk.BallPhases, Teleporter = blk.Teleporter, Indestructible = blk.Indestructible, Boss = blk.Boss, FlipX = blk.FlipX, FlipY = blk.FlipY, Shielded = blk.ImmunityTimer > 0, Charging = charging, Allied = blk.AllyTimer > 0, Level = blk.StatueLevel, Burning = blk.BurnRemaining > 0, Cursed = blk.Cursed, Union = blk.IsUnion, Elite = blk.Elite });
            }
        }
        foreach (var w in g.FireWalls)
            s.Walls.Add(new WallDto { Y = w.Y, Width = w.Width });
        foreach (var hz in g.Hazards)
            s.Hazards.Add(new HazardDto { X = hz.Pos.X, Y = hz.Pos.Y, Kind = hz.Kind, Warming = hz.Warmup > 0 });
        foreach (var ph in g.Phoenixes)
            s.Phoenixes.Add(new PhoenixDto { Id = ph.Id, X = ph.Pos.X, Y = ph.Pos.Y, Angle = ph.Angle });
        s.TurretActive = g.TurretActive;
        s.TeslaArmed        = g._teslaArmed;
        s.TeslaLeftCharged  = g._teslaLeftCharged;
        s.TeslaRightCharged = g._teslaRightCharged;
        var aliveBossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        s.BossActive = aliveBossBlocks.Count > 0;
        s.BossHp     = aliveBossBlocks.Sum(b => b.Hp);
        s.BossMaxHp  = aliveBossBlocks.Sum(b => b.MaxHp);
        foreach (var id in g.ActiveRelics)
        {
            g.RelicCatalog.TryGet(id, out RelicDef? def);
            s.ActiveRelics.Add(new RelicDto {
                Id   = id,
                Name = def?.Name ?? id,
                Icon = def?.Icon ?? ""
            });
        }
        foreach (var bn in g.Bonuses)
            s.Bonuses.Add(new BonusDto { Id = bn.Id, X = bn.Pos.X, Y = bn.Pos.Y, Type = bn.Type, Icon = bn.Icon });
        s.WidePaddleActive = g.WidePaddleActive;
        s.WidePaddleTimer  = g.WidePaddleTimer;
        s.SlowBallActive   = g.SlowBallActive;
        s.SlowBallTimer    = g.SlowBallTimer;
        foreach (var br in g.Barriers)
            s.Barriers.Add(new BarrierDto { Y = br.Y, CenterX = br.CenterX, Width = br.Width });
        foreach (var zn in g.Zones)
            s.Zones.Add(new ZoneDto { X = zn.X, Y = zn.Y, Radius = zn.Radius });
        s.SkeletonActive  = g.SkeletonActive;
        s.SpellDrainActive     = g.SpellDrainActive;
        s.CrystalBonus    = g.ItemCrystalBonus;
        s.WindRadius      = g.Config.Enemies.WindMasterRadius;
        if (g.Level.SurviveTime > 0)
        { s.TimerMode = "survive"; s.TimeLeft = System.Math.Max(0, g.Level.SurviveTime - g.ElapsedPlayTime); }
        else if (g.Level.TimeLimit > 0)
        { s.TimerMode = "limit"; s.TimeLeft = System.Math.Max(0, g.Level.TimeLimit - g.ElapsedPlayTime); }
        s.Floor      = g.FloorIndex + 1;
        s.FloorCount = g.ExtraFloors.Count + 1;
        if (g.RiftMode)
        {
            s.IsRift = true;
            int cleared = g.FloorIndex;                 // floors fully cleared so far
            int total   = g.ExtraFloors.Count + 1;
            s.RiftReward        = Meta.RiftModifierService.DepthCrystals(cleared, total, g.RiftRewardMult);
            s.RiftNextMilestone = Meta.RiftModifierService.NextMilestone(cleared);
            if (g.AwaitingRiftDraft)
            {
                s.AwaitingDraft = true;
                foreach (var id in g.RiftDraftChoices)
                {
                    var m = Meta.RiftModifierService.Get(id);
                    if (m != null) s.DraftChoices.Add(new RiftChoiceDto { Id = m.Id, Name = m.Name, Desc = m.Desc });
                }
            }
        }
        s.BricksDestroyedThisLevel = g.BricksDestroyedThisLevel;
        s.FireshotActive = g.FireshotActive;
        s.FireshotTimer  = g.FireshotTimer;
        s.AutoSaveActive = g.AutoSaveActive;
        s.ComboMultiplier = g.ComboMultiplier;
        foreach (var ef in g._effects)
            s.ActiveEffects.Add(new ActiveEffectDto { Id = ef.Id, TimeLeft = ef.Remaining });
        s.MinibossFloor = g.MinibossFloor;
        var sigId = g.SignatureId;
        foreach (var id in g.Loadout)
        {
            var disp = g.SpellDisplay(id);
            var def  = g.GetSpellDef(id);
            s.Loadout.Add(new LoadoutSlotDto {
                Id        = id,
                Name      = disp?.Name ?? id,
                Icon      = disp?.Icon ?? "",
                ManaCost  = (int)(def?.ManaCost ?? (disp?.ManaCost ?? 0)),
                Level     = g.SpellLevel(id),
                Signature = id == sigId,
            });
        }
        foreach (var ev in g.DrainEvents())
            s.Events.Add(new EventDto { Type = KindToWire(ev.Kind), X = ev.X, Y = ev.Y, Extra = ev.Payload });
        return s;
    }

    private static string KindToWire(SimEventKind k) => k switch
    {
        SimEventKind.SpellCast       => "spellCast",
        SimEventKind.SpellFizzle     => "spellFizzle",
        SimEventKind.Ignite          => "ignite",
        SimEventKind.Burn            => "burn",
        SimEventKind.Decay           => "decay",
        SimEventKind.Lightning       => "lightning",
        SimEventKind.Radiation       => "radiation",
        SimEventKind.Phoenix         => "phoenix",
        SimEventKind.SkeletonShot    => "skeletonShot",
        SimEventKind.TurretShot      => "turretShot",
        SimEventKind.Penetration     => "penetration",
        SimEventKind.Judgement       => "judgement",
        SimEventKind.Frost           => "frost",
        SimEventKind.BlockDestroyed  => "blockDestroyed",
        SimEventKind.Explosion       => "explosion",
        SimEventKind.Crit            => "crit",
        SimEventKind.Deflect         => "deflect",
        SimEventKind.PerfectDeflect  => "perfectDeflect",
        SimEventKind.GhostPortal     => "ghostPortal",
        SimEventKind.Teleport        => "teleport",
        SimEventKind.EnemyShot       => "enemyShot",
        SimEventKind.AllyShot        => "allyShot",
        SimEventKind.BatGrab         => "batGrab",
        SimEventKind.BatRelease      => "batRelease",
        SimEventKind.Cart            => "cart",
        SimEventKind.Stalactite      => "stalactite",
        SimEventKind.Corrupt         => "corrupt",
        SimEventKind.DeathMark       => "deathMark",
        SimEventKind.BossTelegraph   => "bossTelegraph",
        SimEventKind.BossAttack      => "bossAttack",
        SimEventKind.BossPhase       => "bossPhase",
        SimEventKind.BossHop         => "bossHop",
        SimEventKind.FistTelegraph   => "fistTelegraph",
        SimEventKind.FistSlam        => "fistSlam",
        SimEventKind.WitchGrabCast   => "witchGrabCast",
        SimEventKind.WitchGrab       => "witchGrab",
        SimEventKind.WitchThrow      => "witchThrow",
        SimEventKind.WitchGrabPopped => "witchGrabPopped",
        SimEventKind.SeraphVase      => "seraphVase",
        SimEventKind.SeraphAdd       => "seraphAdd",
        SimEventKind.VaseShatter     => "vaseShatter",
        SimEventKind.VaseLevelUp     => "vaseLevelUp",
        SimEventKind.LavaCreep       => "lavaCreep",
        SimEventKind.LavaRetract     => "lavaRetract",
        SimEventKind.LavaDrain       => "lavaDrain",
        SimEventKind.BonusCaught     => "bonusCaught",
        SimEventKind.SplitShot       => "splitShot",
        SimEventKind.PlayerHit       => "playerHit",
        SimEventKind.ShieldSave      => "shieldSave",
        SimEventKind.ShieldBlock     => "shieldBlock",
        SimEventKind.Shield          => "shield",
        SimEventKind.BarrierHit      => "barrierHit",
        SimEventKind.SecondWind      => "secondWind",
        SimEventKind.ManaRefund      => "manaRefund",
        SimEventKind.LevelWon        => "levelWon",
        SimEventKind.LevelLost       => "levelLost",
        SimEventKind.TimeUp          => "timeUp",
        SimEventKind.Overrun         => "overrun",
        SimEventKind.Descend         => "descend",
        SimEventKind.FloorDown       => "floorDown",
        SimEventKind.Revive          => "revive",
        SimEventKind.ReviveCancelled => "reviveCancelled",
        SimEventKind.Altar           => "altar",
        SimEventKind.MidasCrystals   => "midasCrystals",
        _                            => k.ToString(),
    };
}
