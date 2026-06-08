namespace Arkanoid.Core.Sim;

/// <summary>Host-agnostic structured log sink. Core never touches Console/files directly.</summary>
public interface ISimLog
{
    bool Verbose { get; }
    void Log(long tick, string cat, string msg, string data = "");
}

/// <summary>Default no-op sink (unit tests run silent unless they inject a capture).</summary>
public sealed class NullSimLog : ISimLog
{
    public static readonly NullSimLog Instance = new();
    public bool Verbose => false;
    public void Log(long tick, string cat, string msg, string data = "") { }
}
