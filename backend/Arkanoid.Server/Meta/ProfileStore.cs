using Arkanoid.Core.Meta;
namespace Arkanoid.Server.Meta;

/// <summary>Loads and saves the player Profile JSON. Path injected via DI so it works under dotnet publish.</summary>
public sealed class ProfileStore : IProfileStore
{
    private readonly JsonStore<Profile> _store;

    public ProfileStore(string savesDir)
        => _store = new JsonStore<Profile>(savesDir, "profile.json", "profile");

    internal static string Sanitize(string s) => JsonStore<Profile>.Sanitize(s);

    public Profile Load(string pid = "default")
    {
        var p = _store.Load(pid, Profile.NewDefault) ?? Profile.NewDefault();
        // One-time fold of the pre-rework currencies into Sparks/Souls/Insight (economy rework).
        if (!p.CurrencyMigrated)
        {
            p.MigrateCurrencies();
            _store.Save(p, pid);
        }
        return p;
    }

    public void Save(Profile profile, string pid = "default") =>
        _store.Save(profile, pid);
}
