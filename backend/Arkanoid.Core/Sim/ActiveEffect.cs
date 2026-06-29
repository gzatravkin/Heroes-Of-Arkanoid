namespace Arkanoid.Core.Sim;

public sealed class ActiveEffect
{
    public string Id           { get; init; } = "";
    public double Remaining    { get; set;  }
    public double TickInterval { get; init; }
    public double Accum        { get; set;  }
}
