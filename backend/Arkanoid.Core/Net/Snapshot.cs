using System.Text.Json.Serialization;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Relics;
namespace Arkanoid.Core.Net;

public sealed class BallDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("ignited")] public bool Ignited { get; set; }
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
            s.Balls.Add(new BallDto { Id = b.Id, X = b.Pos.X, Y = b.Pos.Y, Ignited = b.IgniteHitsLeft > 0 });
        foreach (var pr in g.Projectiles)
            s.Balls.Add(new BallDto { Id = 10000 + pr.Id, X = pr.Pos.X, Y = pr.Pos.Y, Ignited = true });
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
        s.Events.AddRange(g.DrainEvents());
        return s;
    }
}
