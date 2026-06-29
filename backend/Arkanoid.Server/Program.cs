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
// Local SQLite for social systems (leaderboard/leagues/seasons) — no cloud.
builder.Services.AddSingleton(_ => new SqliteDb(savesDir));
builder.Services.AddSingleton<Arkanoid.Core.Meta.ILeaderboardStore>(sp => new SqliteLeaderboardStore(sp.GetRequiredService<SqliteDb>()));

var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// --- All catalogs loaded once at startup; no per-session disk reads ---
var campaignCatalog   = CampaignCatalog.FromFile(Path.Combine(configRoot, "campaign.json"));
var dungeonCatalog    = DungeonCatalog.FromFile(Path.Combine(configRoot, "dungeons.json"));
var characterCatalog  = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));
var cardCatalogPath   = Path.Combine(configRoot, "cards.json");
var cardCatalog       = File.Exists(cardCatalogPath) ? CardCatalog.FromFile(cardCatalogPath) : CardCatalog.FromJson("{\"cards\":[]}");
var missionCatalogPath= Path.Combine(configRoot, "missions.json");
var missionCatalog    = File.Exists(missionCatalogPath) ? MissionCatalog.FromFile(missionCatalogPath) : MissionCatalog.FromJson("{\"missions\":[]}");
var moduleCatalogPath = Path.Combine(configRoot, "modules.json");
var moduleCatalog     = File.Exists(moduleCatalogPath) ? ModuleCatalog.FromFile(moduleCatalogPath) : ModuleCatalog.FromJson("{\"modules\":[]}");
var seasonCatalogPath = Path.Combine(configRoot, "seasons.json");
var seasonCatalog     = File.Exists(seasonCatalogPath) ? SeasonCatalog.FromFile(seasonCatalogPath) : SeasonCatalog.FromJson("{\"themes\":[],\"track\":[]}");
var eventCatalogPath  = Path.Combine(configRoot, "events.json");
var eventCatalog      = File.Exists(eventCatalogPath) ? EventCatalog.FromFile(eventCatalogPath) : EventCatalog.FromJson("{\"events\":[]}");
var blockCatalog      = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
var relicCatalog      = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
var bonusCatalogPath  = Path.Combine(configRoot, "bonuses.json");
var bonusCatalog      = File.Exists(bonusCatalogPath) ? BonusCatalog.FromFile(bonusCatalogPath) : null;
var progressionConfig = ProgressionConfig.Default;
var seasonClock   = SeasonClock.Default;
var profileStore  = app.Services.GetRequiredService<IProfileStore>();
var dungeonStore  = app.Services.GetRequiredService<IDungeonStore>();
var sqliteDb      = app.Services.GetRequiredService<SqliteDb>();
var leaderboardStore = app.Services.GetRequiredService<Arkanoid.Core.Meta.ILeaderboardStore>();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapGet("/", () => "Arkanoid server up");

ProfileEndpoints.Map(app, profileStore, campaignCatalog, dungeonCatalog, progressionConfig, moduleCatalog, jsonOpts);
DungeonEndpoints.Map(app, dungeonStore, dungeonCatalog, profileStore, progressionConfig, jsonOpts);
DungeonShopEndpoints.Map(app, dungeonStore, jsonOpts);
MetaEndpoints.Map(app, seasonClock, jsonOpts);
LeaderboardEndpoints.Map(app, leaderboardStore, seasonClock, profileStore, jsonOpts);
CardEndpoints.Map(app, cardCatalog, profileStore, jsonOpts);
DailyEndpoints.Map(app, missionCatalog, seasonClock, profileStore, jsonOpts);
PrestigeEndpoints.Map(app, profileStore, leaderboardStore, jsonOpts);
ModuleEndpoints.Map(app, moduleCatalog, profileStore, jsonOpts);
RollEndpoints.Map(app, cardCatalog, moduleCatalog, profileStore, jsonOpts);
SeasonEndpoints.Map(app, seasonCatalog, eventCatalog, seasonClock, leaderboardStore, profileStore, cardCatalog, jsonOpts);
CharacterEndpoints.Map(app, characterCatalog, profileStore, jsonOpts);
SpellEndpoints.Map(app, characterCatalog, profileStore, jsonOpts);
// Item shop removed (economy rework 2026-06-15): the passive-item system is folded into Cards
// (now surfaced as "Items"); acquisition is random rolls only. ItemEndpoints + InventoryScene retired.
RelicEndpoints.Map(app, relicCatalog, jsonOpts);
EditorEndpoints.Map(app, configRoot, jsonOpts);
DevEndpoints.Map(app, campaignCatalog, characterCatalog, profileStore, jsonOpts);

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var levelId = context.Request.Query["level"].FirstOrDefault() ?? "hell-1";
    var seed = int.TryParse(context.Request.Query["seed"].FirstOrDefault(), out var s) ? s : 1;
    var runId = context.Request.Query["run"].FirstOrDefault() ?? $"sess-{DateTime.UtcNow:HHmmss-fff}";
    var pid   = context.Request.Query["pid"].FirstOrDefault() ?? "default";
    var mode  = context.Request.Query["mode"].FirstOrDefault() ?? "";
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot, blockCatalog, relicCatalog, bonusCatalog,
                                  profileStore, dungeonStore, pid, missionCatalog,
                                  leaderboardStore, mode, seasonCatalog, eventCatalog);
    await session.RunAsync(levelId, seed, runId, context.RequestAborted);
});

app.Run("http://localhost:5080");
