using System.Net.WebSockets;
using Arkanoid.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// config dir is ../../config relative to the server project at runtime
var configRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config"));
if (!Directory.Exists(configRoot))
    configRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "config"));

app.MapGet("/", () => "Arkanoid server up");

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
