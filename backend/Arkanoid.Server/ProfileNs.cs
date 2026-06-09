namespace Arkanoid.Server;

/// <summary>
/// Resolves the profile namespace for a request. Parallel test workers set the
/// "X-Profile-Id" header (HTTP) / "pid" query (WebSocket) so each worker reads and
/// writes its own isolated profile/dungeon save. Production sends neither → "default".
/// </summary>
public static class ProfileNs
{
    public const string Header = "X-Profile-Id";
    public const string Query  = "pid";

    public static string From(HttpContext ctx)
    {
        var v = ctx.Request.Headers[Header].FirstOrDefault()
             ?? ctx.Request.Query[Query].FirstOrDefault();
        return string.IsNullOrWhiteSpace(v) ? "default" : v;
    }
}
