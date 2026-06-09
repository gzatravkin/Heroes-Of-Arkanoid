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
    }

    public static LevelData FromJson(string json, BlockCatalog catalog, SimConfig? cfg = null)
    {
        cfg ??= SimConfig.Default;
        var dto = JsonSerializer.Deserialize<Dto>(json) ?? throw new InvalidOperationException("bad level json");
        var grid = new Grid(dto.Cols, dto.Rows, cfg.CellSize, cfg.BoardOriginX, cfg.BoardOriginY);
        var blocks = new List<Block>();
        int nextId = 1;
        for (int row = 0; row < dto.RowsData.Count; row++)
        {
            var line = dto.RowsData[row];
            for (int col = 0; col < line.Length; col++)
            {
                var ch = line[col].ToString();
                if (ch == "." || !dto.Legend.TryGetValue(ch, out var typeId)) continue;
                var t = catalog.Get(typeId);
                blocks.Add(new Block {
                    Id = nextId++, Col = col, Row = row,
                    Hp = t.Hp, MaxHp = t.Hp, TypeId = t.Id,
                    Sprite = t.Sprite, NeedToKill = t.NeedToKill,
                    Indestructible = t.Indestructible,
                    BallPhases = t.BallPhases,
                    Teleporter = t.Teleporter, TeleportColor = t.TeleportColor,
                    Boss = t.Boss,
                    Emitter = t.Emitter, EmitInterval = t.EmitInterval, EmitAim = t.EmitAim,
                    Bomb = t.Bomb, ExplodeRadius = t.ExplodeRadius,
                    Stalactite = t.Stalactite, Necromant = t.Necromant, WindMaster = t.WindMaster,
                    ShieldStatue = t.ShieldStatue,
                    FlipX = t.FlipX, FlipY = t.FlipY
                });
            }
        }
        return new LevelData { Id = dto.Id, Biome = dto.Biome, Grid = grid, Blocks = blocks };
    }

    public static LevelData FromFile(string path, BlockCatalog catalog, SimConfig? cfg = null)
        => FromJson(File.ReadAllText(path), catalog, cfg);
}
