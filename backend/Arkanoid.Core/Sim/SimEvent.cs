namespace Arkanoid.Core.Sim;

/// <summary>
/// A domain-level game event raised during simulation (block destroyed, spell cast, etc.).
/// Mapped to <see cref="Net.EventDto"/> at the session boundary in Snapshot.From().
/// </summary>
/// <param name="Kind">Compile-checked event discriminant (see <see cref="SimEventKind"/>).</param>
/// <param name="X">World X coordinate of the effect origin.</param>
/// <param name="Y">World Y coordinate of the effect origin.</param>
/// <param name="Payload">Optional integer payload (e.g. boss phase number for <see cref="SimEventKind.BossPhase"/>).</param>
public readonly record struct SimEvent(SimEventKind Kind, double X, double Y, int Payload = 0);
