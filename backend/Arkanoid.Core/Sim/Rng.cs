namespace Arkanoid.Core.Sim;

/// <summary>Deterministic, seed-reproducible RNG (no UnityEngine.Random).</summary>
public sealed class Rng
{
    private readonly System.Random _r;
    public int Seed { get; }
    public Rng(int seed) { Seed = seed; _r = new System.Random(seed); }
    public double NextDouble() => _r.NextDouble();
    public double Range(double min, double max) => min + (max - min) * _r.NextDouble();
    /// <summary>Returns a random int in [0, count). Deterministic.</summary>
    public int Range(int count) => count <= 0 ? 0 : _r.Next(count);
}
