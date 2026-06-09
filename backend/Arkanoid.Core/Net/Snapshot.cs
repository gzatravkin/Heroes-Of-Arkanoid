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
}

public sealed class HazardDto
{
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
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
    [JsonPropertyName("type")] public string Type { get; set; } = ""; // e.g. blockDestroyed, spellCast
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
}

public sealed class RelicDto
{
    [JsonPropertyName("id")]   public string Id   { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
}

public sealed class BonusDto
{
    [JsonPropertyName("id")]   public int    Id   { get; set; }
    [JsonPropertyName("x")]    public double X    { get; set; }
    [JsonPropertyName("y")]    public double Y    { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = "";
}

public sealed class Snapshot
{
    [JsonPropertyName("tick")] public long Tick { get; set; }
    [JsonPropertyName("phase")] public string Phase { get; set; } = "";
    [JsonPropertyName("lives")] public int Lives { get; set; }
    [JsonPropertyName("spareBalls")] public int SpareBalls { get; set; }
    [JsonPropertyName("mana")] public double Mana { get; set; }
    [JsonPropertyName("manaMax")] public double ManaMax { get; set; }
    [JsonPropertyName("boardW")] public double BoardW { get; set; }
    [JsonPropertyName("boardH")] public double BoardH { get; set; }
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
    [JsonPropertyName("paddleX")] public double PaddleX { get; set; }
    [JsonPropertyName("paddleW")] public double PaddleW { get; set; }
    [JsonPropertyName("paddleH")] public double PaddleH { get; set; }
    [JsonPropertyName("cellSize")] public double CellSize { get; set; }
    [JsonPropertyName("balls")] public List<BallDto> Balls { get; set; } = new();
    [JsonPropertyName("blocks")] public List<BlockDto> Blocks { get; set; } = new();
    [JsonPropertyName("walls")] public List<WallDto> Walls { get; set; } = new();
    [JsonPropertyName("turretActive")]  public bool TurretActive { get; set; }
    [JsonPropertyName("activeRelics")]  public List<RelicDto> ActiveRelics { get; set; } = new();
    [JsonPropertyName("hazards")] public List<HazardDto> Hazards { get; set; } = new();
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
    [JsonPropertyName("drainActive")]     public bool DrainActive     { get; set; }

    public static Snapshot From(GameInstance g, long tick)
    {
        var s = new Snapshot {
            Tick = tick, Phase = g.Phase.ToString(),
            Lives = g.Lives, SpareBalls = g.SpareBalls,
            Mana = g.ManaValue, ManaMax = g.ManaMaxValue,
            BoardW = g.Level.Grid.Width, BoardH = g.Level.Grid.Height,
            Biome = g.Level.Biome,
            PaddleX = g.Paddle.Center.X, PaddleW = g.Paddle.Width, PaddleH = g.Paddle.Height,
            CellSize = g.Config.CellSize
        };
        foreach (var b in g.Balls)
            s.Balls.Add(new BallDto { Id = b.Id, X = b.Pos.X, Y = b.Pos.Y, Ignited = b.IgniteHitsLeft > 0, Decayed = b.DecayHitsLeft > 0 });
        // Legacy: projectiles also appear in Balls list for backwards compat (id offset)
        foreach (var pr in g.Projectiles)
            s.Balls.Add(new BallDto { Id = 10000 + pr.Id, X = pr.Pos.X, Y = pr.Pos.Y, Ignited = pr.Kind is "fireball" or ""});
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead) continue;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            s.Blocks.Add(new BlockDto { Id = blk.Id, X = c.X, Y = c.Y, Hp = blk.Hp, MaxHp = blk.MaxHp, Sprite = blk.Sprite, BallPhases = blk.BallPhases, Teleporter = blk.Teleporter, Indestructible = blk.Indestructible, Boss = blk.Boss });
        }
        foreach (var w in g.FireWalls)
            s.Walls.Add(new WallDto { Y = w.Y, Width = w.Width });
        foreach (var hz in g.Hazards)
            s.Hazards.Add(new HazardDto { X = hz.Pos.X, Y = hz.Pos.Y });
        s.TurretActive = g.TurretActive;
        var aliveBossBlocks = g.Blocks.Where(b => !b.Dead && b.Boss).ToList();
        s.BossActive = aliveBossBlocks.Count > 0;
        s.BossHp     = aliveBossBlocks.Sum(b => b.Hp);
        s.BossMaxHp  = aliveBossBlocks.Sum(b => b.MaxHp);
        foreach (var id in g.ActiveRelics)
        {
            // catalog may not be present in unit test contexts; fall back to id
            RelicDef? def = null;
            g.RelicCatalog?.TryGet(id, out def);
            s.ActiveRelics.Add(new RelicDto {
                Id   = id,
                Name = def?.Name ?? id,
                Icon = def?.Icon ?? ""
            });
        }
        foreach (var bn in g.Bonuses)
            s.Bonuses.Add(new BonusDto { Id = bn.Id, X = bn.Pos.X, Y = bn.Pos.Y, Type = bn.Type, Icon = bn.Icon });
        s.WidePaddleActive = g._widePaddleActive;
        s.WidePaddleTimer  = g._widePaddleTimer;
        s.SlowBallActive   = g._slowBallActive;
        s.SlowBallTimer    = g._slowBallTimer;
        foreach (var br in g.Barriers)
            s.Barriers.Add(new BarrierDto { Y = br.Y, CenterX = br.CenterX, Width = br.Width });
        foreach (var zn in g.Zones)
            s.Zones.Add(new ZoneDto { X = zn.X, Y = zn.Y, Radius = zn.Radius });
        s.SkeletonActive = g.SkeletonActive;
        s.DrainActive    = g.DrainActive;
        s.Events.AddRange(g.DrainEvents());
        return s;
    }
}
