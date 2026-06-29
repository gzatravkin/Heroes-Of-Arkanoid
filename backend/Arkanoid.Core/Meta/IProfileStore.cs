namespace Arkanoid.Core.Meta;

/// <summary>Seam for loading and persisting player profiles. Implement with ProfileStore for production; stub for tests.</summary>
public interface IProfileStore
{
    Profile Load(string pid = "default");
    void Save(Profile profile, string pid = "default");
}
