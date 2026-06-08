using System.Text.Json.Serialization;
using Arkanoid.Core.Sim;
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
}

public sealed class EventDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = ""; // e.g. blockDestroyed, spellCast
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
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
    [JsonPropertyName("paddleX")] public double PaddleX { get; set; }
    [JsonPropertyName("paddleW")] public double PaddleW { get; set; }
    [JsonPropertyName("paddleH")] public double PaddleH { get; set; }
    [JsonPropertyName("cellSize")] public double CellSize { get; set; }
    [JsonPropertyName("balls")] public List<BallDto> Balls { get; set; } = new();
    [JsonPropertyName("blocks")] public List<BlockDto> Blocks { get; set; } = new();
    [JsonPropertyName("events")] public List<EventDto> Events { get; set; } = new();

    public static Snapshot From(GameInstance g, long tick)
    {
        var s = new Snapshot {
            Tick = tick, Phase = g.Phase.ToString(),
            Lives = g.Lives, SpareBalls = g.SpareBalls,
            Mana = g.ManaValue, ManaMax = g.Config.ManaMax,
            BoardW = g.Level.Grid.Width, BoardH = g.Level.Grid.Height,
            PaddleX = g.Paddle.Center.X, PaddleW = g.Paddle.Width, PaddleH = g.Paddle.Height,
            CellSize = g.Config.CellSize
        };
        foreach (var b in g.Balls)
            s.Balls.Add(new BallDto { Id = b.Id, X = b.Pos.X, Y = b.Pos.Y, Ignited = b.IgniteHitsLeft > 0 });
        foreach (var blk in g.Blocks)
        {
            if (blk.Dead) continue;
            var c = g.Level.Grid.CellCenter(blk.Col, blk.Row);
            s.Blocks.Add(new BlockDto { Id = blk.Id, X = c.X, Y = c.Y, Hp = blk.Hp, MaxHp = blk.MaxHp, Sprite = blk.Sprite });
        }
        s.Events.AddRange(g.DrainEvents());
        return s;
    }
}
