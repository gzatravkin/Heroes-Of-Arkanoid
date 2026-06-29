using System.Linq;
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
                    ForcedDropEffect = t.ForcedDropEffect,
                    Sprite = t.Sprite, NeedToKill = t.NeedToKill,
                    Indestructible = t.Indestructible,
                    BallPhases = t.BallPhases,
                    IsUnion = t.Union,
                    Behavior = t.Behavior,
                    TeleportColor = t.TeleportColor,
                    EmitInterval = t.EmitInterval, EmitAim = t.EmitAim,
                    ExplodeRadius = t.ExplodeRadius,
                    MissileKind = t.MissileKind,
                    FlipX = t.FlipX, FlipY = t.FlipY, Elite = t.Elite
                });
            }
        }
        return blocks;
    }

    public static LevelData FromFile(string path, BlockCatalog catalog, SimConfig? cfg = null)
        => FromJson(File.ReadAllText(path), catalog, cfg);

    /// <summary>
    /// Continuous Rift (2026-06-16): stack a list of floor level JSONs into ONE multi-floor LevelData —
    /// floor 0's blocks are the starting layout, the rest become <see cref="LevelData.ExtraFloors"/>, so
    /// the sim slides the next floor in (same GameInstance) when the player clears a floor. Block ids are
    /// assigned from a single shared counter so they stay unique across all floors. Grid/biome/id come
    /// from floor 0 (all rift floors share the biome + 12×18 board).
    /// </summary>
    public static LevelData FromRiftFloors(IEnumerable<string> floorJsons, BlockCatalog catalog, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var jsons = floorJsons.ToList();
        if (jsons.Count == 0) throw new InvalidOperationException("rift has no floors");
        var first = JsonSerializer.Deserialize<Dto>(jsons[0]) ?? throw new InvalidOperationException("bad rift floor json");
        var grid = new Grid(first.Cols, first.Rows, cfg.CellSize, cfg.BoardOriginX, cfg.BoardOriginY);
        int nextId = 1;
        var floors = new List<List<Block>>();
        foreach (var j in jsons)
        {
            var dto = JsonSerializer.Deserialize<Dto>(j) ?? throw new InvalidOperationException("bad rift floor json");
            floors.Add(BuildBlocks(dto.RowsData, dto.Legend, catalog, ref nextId));
        }
        return new LevelData
        {
            Id = first.Id, Biome = first.Biome, Grid = grid,
            Blocks = floors[0],
            ExtraFloors = floors.Skip(1).ToList(),
        };
    }

    public static LevelData FromRiftFloorFiles(IEnumerable<string> paths, BlockCatalog catalog, SimConfig? cfg = null)
        => FromRiftFloors(paths.Select(File.ReadAllText), catalog, cfg);
}
