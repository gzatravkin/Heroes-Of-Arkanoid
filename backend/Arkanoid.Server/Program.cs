using System.Net.WebSockets;
using System.Text.Json;
using Arkanoid.Core.Meta;
using Arkanoid.Server;
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
var progressionConfig = ProgressionConfig.Default;
var profileStore      = new ProfileStore();

var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

app.MapGet("/", () => "Arkanoid server up");

// GET /profile → current profile
app.MapGet("/profile", () =>
{
    var profile = profileStore.Load();
    return Results.Json(profile, jsonOpts);
});

// GET /campaign → nodes with unlocked/completed flags
app.MapGet("/campaign", () =>
{
    var profile   = profileStore.Load();
    var completed = new HashSet<string>(profile.CompletedLevels);
    var nodes = campaignCatalog.Nodes.Select(n => new
    {
        n.Id, n.Label, n.Biome, n.X, n.Y,
        Unlocked  = campaignCatalog.IsUnlocked(n, completed),
        Completed = completed.Contains(n.Id),
    });
    return Results.Json(new { nodes }, jsonOpts);
});

// POST /complete?level=<id> → grant completion reward, save, return {profile, reward}
app.MapPost("/complete", (HttpContext ctx) =>
{
    var levelId = ctx.Request.Query["level"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(levelId))
        return Results.BadRequest("level query parameter required");

    var profile = profileStore.Load();
    var reward  = Rewards.GrantLevelCompletion(profile, levelId, progressionConfig);
    profileStore.Save(profile);
    return Results.Json(new { profile, reward }, jsonOpts);
});

// POST /upgrade?spell=<id> → upgrade spell, save, return {ok, profile}
app.MapPost("/upgrade", (HttpContext ctx) =>
{
    var spellId = ctx.Request.Query["spell"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(spellId))
        return Results.BadRequest("spell query parameter required");

    var profile = profileStore.Load();
    var ok      = Upgrades.TryUpgradeSpell(profile, spellId, progressionConfig);
    if (ok) profileStore.Save(profile);
    return Results.Json(new { ok, profile }, jsonOpts);
});

// POST /reset → reset to new default, save, return profile
app.MapPost("/reset", () =>
{
    var profile = Profile.NewDefault();
    profileStore.Save(profile);
    return Results.Json(profile, jsonOpts);
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var levelId = context.Request.Query["level"].FirstOrDefault() ?? "hell-1";
    var seed = int.TryParse(context.Request.Query["seed"].FirstOrDefault(), out var s) ? s : 1;
    var runId = context.Request.Query["run"].FirstOrDefault() ?? $"sess-{DateTime.UtcNow:HHmmss-fff}";
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new GameSession(socket, configRoot);
    await session.RunAsync(levelId, seed, runId, context.RequestAborted);
});

app.Run("http://localhost:5080");
