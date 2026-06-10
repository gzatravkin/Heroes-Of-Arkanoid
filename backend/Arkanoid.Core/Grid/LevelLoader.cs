using System.Text.Json;
using System.Text.Json.Serialization;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Grid;

public static class LevelLoader
{
    private sealed class Dto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("biome")] public string Biome { get; set; } = "";
        [JsonPropertyName("cols")] public int Cols { get; set; }
        [JsonPropertyName("rows")] public int Rows { get; set; }
        [JsonPropertyName("rows_data")] public List<string> RowsData { get; set; } = new();
        [JsonPropertyName("legend")] public Dictionary<string, string> Legend { get; set; } = new();
        // Objective flavors + pacing modes (docs/12) — all optional.
        [JsonPropertyName("timeLimit")]        public double TimeLimit        { get; set; }
        [JsonPropertyName("surviveTime")]      public double SurviveTime      { get; set; }
        [JsonPropertyName("descendInterval")]  public double DescendInterval  { get; set; }
        [JsonPropertyName("escalateInterval")] public double EscalateInterval { get; set; }
        /// <summary>Extra floors (multi-floor collapse): each entry is its own rows_data, same legend.</summary>
        [JsonPropertyName("floors")] public List<List<string>> Floors { get; set; } = new();
    }

    public static LevelData FromJson(string json, BlockCatalog catalog, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("bad level json");
        var grid = new Grid(dto.Cols, dto.Rows, cfg.CellSize, cfg.BoardOriginX, cfg.BoardOriginY);
        int nextId = 1;
        var blocks = BuildBlocks(dto.RowsData, dto.Legend, catalog, ref nextId);
        var extraFloors = new List<List<Block>>();
        foreach (var floorRows in dto.Floors)
            extraFloors.Add(BuildBlocks(floorRows, dto.Legend, catalog, ref nextId));
        return new LevelData
        {
            Id = dto.Id, Biome = dto.Biome, Grid = grid, Blocks = blocks,
            TimeLimit = dto.TimeLimit, SurviveTime = dto.SurviveTime,
            DescendInterval = dto.DescendInterval, EscalateInterval = dto.EscalateInterval,
            ExtraFloors = extraFloors,
        };
    }

    private static List<Block> BuildBlocks(
        List<string> rowsData, Dictionary<string, string> legend, BlockCatalog catalog, ref int nextId)
    {
        var blocks = new List<Block>();
        for (int row = 0; row < rowsData.Count; row++)
        {
            var line = rowsData[row];
            for (int col = 0; col < line.Length; col++)
            {
                var ch = line[col].ToString();
                if (ch == "." || !legend.TryGetValue(ch, out var typeId)) continue;
                var t = catalog.Get(typeId);
                blocks.Add(new Block {
                    Id = nextId++, Col = col, Row = row,
                    Hp = t.Hp, MaxHp = t.Hp, TypeId = t.Id,
                    Sprite = t.Sprite, NeedToKill = t.NeedToKill,
                    Indestructible = t.Indestructible,
                    BallPhases = t.BallPhases,
                    Behavior = t.Behavior,
                    TeleportColor = t.TeleportColor,
                    EmitInterval = t.EmitInterval, EmitAim = t.EmitAim,
                    ExplodeRadius = t.ExplodeRadius,
                    MissileKind = t.MissileKind,
                    FlipX = t.FlipX, FlipY = t.FlipY
                });
            }
        }
        return blocks;
    }

    public static LevelData FromFile(string path, BlockCatalog catalog, SimConfig? cfg = null)
        => FromJson(File.ReadAllText(path), catalog, cfg);
}
