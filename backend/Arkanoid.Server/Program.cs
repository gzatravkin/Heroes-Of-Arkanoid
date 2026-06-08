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
var dungeonCatalog    = DungeonCatalog.FromFile(Path.Combine(configRoot, "dungeons.json"));
var progressionConfig = ProgressionConfig.Default;
var profileStore      = new ProfileStore();
var dungeonStore      = new DungeonStore();

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

// ── Dungeon endpoints ──────────────────────────────────────────────────────

// GET /dungeons → full catalog
app.MapGet("/dungeons", () =>
    Results.Json(new { dungeons = dungeonCatalog.All }, jsonOpts));

// POST /dungeon/start?id=<id> → start a new run
app.MapPost("/dungeon/start", (HttpContext ctx) =>
{
    var id = ctx.Request.Query["id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest("id query parameter required");

    DungeonDef def;
    try { def = dungeonCatalog.Get(id); }
    catch (KeyNotFoundException) { return Results.NotFound($"Dungeon '{id}' not found"); }

    var seed = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7fffffff);
    var run  = DungeonService.StartRun(def, seed);
    dungeonStore.Save(run);
    return Results.Json(run, jsonOpts);
});

// GET /dungeon/state → current run (or {active:false})
app.MapGet("/dungeon/state", () =>
{
    var run = dungeonStore.Load();
    return run is null
        ? Results.Json(new { active = false }, jsonOpts)
        : Results.Json(run, jsonOpts);
});

// POST /dungeon/floor-cleared → advance floor; grants permanent reward on final floor
app.MapPost("/dungeon/floor-cleared", () =>
{
    var run = dungeonStore.Load();
    if (run is null || !run.Active)
        return Results.BadRequest("No active dungeon run");

    // Need the def for reward data (final floor only)
    DungeonDef def;
    try { def = dungeonCatalog.Get(run.DungeonId); }
    catch { return Results.BadRequest($"Unknown dungeon id '{run.DungeonId}'"); }

    var isLastFloor = DungeonService.OnFloorCleared(run, progressionConfig);
    dungeonStore.Save(run);

    Profile? updatedProfile = null;
    if (isLastFloor)
    {
        // Grant permanent reward to profile
        var profile = profileStore.Load();
        if (!profile.UnlockedRelics.Contains(def.RewardRelic))
            profile.UnlockedRelics.Add(def.RewardRelic);
        profile.Crystals += def.RewardCrystals;
        profileStore.Save(profile);
        updatedProfile = profile;
    }

    return Results.Json(new { run, isLastFloor, profile = updatedProfile }, jsonOpts);
});

// POST /dungeon/pick?choice=<id> → player picks one of the 3 pending choices
app.MapPost("/dungeon/pick", (HttpContext ctx) =>
{
    var choice = ctx.Request.Query["choice"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(choice))
        return Results.BadRequest("choice query parameter required");

    var run = dungeonStore.Load();
    if (run is null || !run.Active)
        return Results.BadRequest("No active dungeon run");

    DungeonService.PickChoice(run, choice);
    dungeonStore.Save(run);
    return Results.Json(run, jsonOpts);
});

// POST /dungeon/fail → permadeath — ends the run
app.MapPost("/dungeon/fail", () =>
{
    var run = dungeonStore.Load();
    if (run is null) return Results.BadRequest("No active dungeon run");

    DungeonService.Fail(run);
    dungeonStore.Save(run);
    return Results.Json(run, jsonOpts);
});

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
