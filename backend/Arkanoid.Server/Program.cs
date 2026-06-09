using System.Net.WebSockets;
using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
using Arkanoid.Server.Endpoints;
using Arkanoid.Server.Meta;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// config dir is ../../config relative to the server project at runtime
var configRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config"));
if (!Directory.Exists(configRoot))
    configRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config"));

// --- Meta singletons (loaded once at startup) ---
var campaignCatalog   = CampaignCatalog.FromFile(Path.Combine(configRoot, "campaign.json"));
var dungeonCatalog    = DungeonCatalog.FromFile(Path.Combine(configRoot, "dungeons.json"));
var characterCatalog  = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));
var progressionConfig = ProgressionConfig.Default;
var profileStore      = new ProfileStore();
var dungeonStore      = new DungeonStore();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapGet("/", () => "Arkanoid server up");

ProfileEndpoints.Map(app, profileStore, campaignCatalog, progressionConfig, jsonOpts);
DungeonEndpoints.Map(app, dungeonStore, dungeonCatalog, profileStore, progressionConfig, jsonOpts);
CharacterEndpoints.Map(app, characterCatalog, profileStore, jsonOpts);

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var levelId = context.Request.Query["level"].FirstOrDefault() ?? "hell-1";
    var seed = int.TryParse(context.Request.Query["seed"].FirstOrDefault(), out var s) ? s : 1;
    var runId = context.Request.Query["run"].FirstOrDefault() ?? $"sess-{DateTime.UtcNow:HHmmss-fff}";
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot, profileStore, dungeonStore);
    await session.RunAsync(levelId, seed, runId, context.RequestAborted);
});

app.Run("http://localhost:5080");
