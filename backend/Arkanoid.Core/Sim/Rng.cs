namespace Arkanoid.Core.Sim;

/// <summary>Deterministic, seed-reproducible RNG (no UnityEngine.Random).</summary>
public sealed class Rng
{
    private readonly System.Random _r;
    public int Seed { get; }
    public Rng(int seed) { Seed = seed; _r = new System.Random(seed); }
    public double NextDouble() => _r.NextDouble();
    public double Range(double min, double max) => min + (max - min) * _r.NextDouble();
}
