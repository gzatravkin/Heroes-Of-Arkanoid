using System.Net.WebSockets;
using System.Text.Json;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Relics;
using Arkanoid.Server;
using Arkanoid.Server.Endpoints;
using Arkanoid.Server.Meta;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Resolve configRoot before services so path is available for DI registration.
var configRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config"));
if (!Directory.Exists(configRoot))
    configRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config"));
var savesDir = builder.Configuration["SavesDir"]
    ?? Path.Combine(configRoot, "..", "saves");

// Register stores via DI: path is now an injected configuration value, not a hard-coded AppContext relative.
builder.Services.AddSingleton<IProfileStore>(_ => new ProfileStore(savesDir));
builder.Services.AddSingleton<IDungeonStore>(_ => new DungeonStore(savesDir));

var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// --- All catalogs loaded once at startup; no per-session disk reads ---
var campaignCatalog   = CampaignCatalog.FromFile(Path.Combine(configRoot, "campaign.json"));
var dungeonCatalog    = DungeonCatalog.FromFile(Path.Combine(configRoot, "dungeons.json"));
var characterCatalog  = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));
var itemCatalog       = ItemCatalog.FromFile(Path.Combine(configRoot, "items.json"));
var blockCatalog      = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
var relicCatalog      = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
var bonusCatalogPath  = Path.Combine(configRoot, "bonuses.json");
var bonusCatalog      = File.Exists(bonusCatalogPath) ? BonusCatalog.FromFile(bonusCatalogPath) : null;
var progressionConfig = ProgressionConfig.Default;
var profileStore  = app.Services.GetRequiredService<IProfileStore>();
var dungeonStore  = app.Services.GetRequiredService<IDungeonStore>();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapGet("/", () => "Arkanoid server up");

ProfileEndpoints.Map(app, profileStore, campaignCatalog, dungeonCatalog, progressionConfig, itemCatalog, jsonOpts);
DungeonEndpoints.Map(app, dungeonStore, dungeonCatalog, profileStore, progressionConfig, jsonOpts);
CharacterEndpoints.Map(app, characterCatalog, profileStore, jsonOpts);
ItemEndpoints.Map(app, itemCatalog, profileStore, jsonOpts);
RelicEndpoints.Map(app, relicCatalog, jsonOpts);
EditorEndpoints.Map(app, configRoot, jsonOpts);

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var levelId = context.Request.Query["level"].FirstOrDefault() ?? "hell-1";
    var seed = int.TryParse(context.Request.Query["seed"].FirstOrDefault(), out var s) ? s : 1;
    var runId = context.Request.Query["run"].FirstOrDefault() ?? $"sess-{DateTime.UtcNow:HHmmss-fff}";
    var pid   = context.Request.Query["pid"].FirstOrDefault() ?? "default";
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot, blockCatalog, relicCatalog, bonusCatalog,
                                  profileStore, dungeonStore, itemCatalog, pid);
    await session.RunAsync(levelId, seed, runId, context.RequestAborted);
});

app.Run("http://localhost:5080");
