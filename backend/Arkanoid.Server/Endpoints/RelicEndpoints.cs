using System.Text.Json;
using Arkanoid.Core.Relics;

namespace Arkanoid.Server.Endpoints;

public static class RelicEndpoints
{
    public static void Map(WebApplication app, RelicCatalog relicCatalog, JsonSerializerOptions jsonOpts)
    {
        app.MapGet("/relics", () => Results.Json(new { relics = relicCatalog.All }, jsonOpts));
    }
}
