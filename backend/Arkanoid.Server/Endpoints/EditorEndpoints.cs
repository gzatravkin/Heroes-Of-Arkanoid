using System.Text.Json;
using System.Text.RegularExpressions;
using Arkanoid.Core.Blocks;

namespace Arkanoid.Server.Endpoints;

public static class EditorEndpoints
{
    private static readonly Regex SafeId = new(@"^[a-z0-9-]+$", RegexOptions.Compiled);

    public static void Map(WebApplication app, string configRoot, JsonSerializerOptions jsonOpts)
    {
        // GET /editor/blocktypes → palette data: [{id, biome, sprite}]
        app.MapGet("/editor/blocktypes", () =>
        {
            var blocksPath = Path.Combine(configRoot, "blocks.json");
            var json = File.ReadAllText(blocksPath);
            using var doc = JsonDocument.Parse(json);
            var types = doc.RootElement.GetProperty("types");
            var result = new List<object>();
            foreach (var t in types.EnumerateArray())
            {
                result.Add(new
                {
                    id     = t.GetProperty("id").GetString(),
                    biome  = t.GetProperty("biome").GetString(),
                    sprite = t.GetProperty("sprite").GetString(),
                });
            }
            return Results.Json(result, jsonOpts);
        });

        // GET /editor/load?id=<id> → return existing level JSON (404 if missing)
        app.MapGet("/editor/load", (HttpContext ctx) =>
        {
            var id = ctx.Request.Query["id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id) || !SafeId.IsMatch(id))
                return Results.BadRequest("Invalid or missing level id");

            var levelPath = Path.Combine(configRoot, "levels", $"{id}.json");
            if (!File.Exists(levelPath))
                return Results.NotFound($"Level '{id}' not found");

            var json = File.ReadAllText(levelPath);
            using var doc = JsonDocument.Parse(json);
            return Results.Json(doc.RootElement, jsonOpts);
        });

        // POST /editor/save (body: {id,biome,cols,rows,rows_data,legend}) → write config/levels/<id>.json
        app.MapPost("/editor/save", async (HttpContext ctx) =>
        {
            LevelSaveRequest? body;
            try
            {
                body = await ctx.Request.ReadFromJsonAsync<LevelSaveRequest>(jsonOpts);
            }
            catch
            {
                return Results.BadRequest("Invalid JSON body");
            }

            if (body is null)
                return Results.BadRequest("Empty body");

            if (string.IsNullOrWhiteSpace(body.Id) || !SafeId.IsMatch(body.Id))
                return Results.BadRequest("Level id must match ^[a-z0-9-]+$");

            if (body.RowsData is null || body.RowsData.Count == 0)
                return Results.BadRequest("rows_data must not be empty");

            var levelsDir = Path.Combine(configRoot, "levels");
            Directory.CreateDirectory(levelsDir);
            var levelPath = Path.Combine(levelsDir, $"{body.Id}.json");

            var writeOpts = new JsonSerializerOptions(jsonOpts) { WriteIndented = true };
            var levelJson = JsonSerializer.Serialize(body, writeOpts);
            await File.WriteAllTextAsync(levelPath, levelJson);

            return Results.Json(new { ok = true, id = body.Id }, jsonOpts);
        });
    }

    private sealed class LevelSaveRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("biome")]
        public string Biome { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("cols")]
        public int Cols { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rows")]
        public int Rows { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("rows_data")]
        public List<string> RowsData { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("legend")]
        public Dictionary<string, string> Legend { get; set; } = new();
    }
}
