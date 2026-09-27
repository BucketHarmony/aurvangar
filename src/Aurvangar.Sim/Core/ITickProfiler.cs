namespace Aurvangar.Sim.Core;

/// <summary>Tick phases that <see cref="ITickProfiler"/> brackets (debug overlay, VIEW-17).</summary>
public enum TickPhase : byte
{
    Water,
    Regions,
    /// <summary>ARCH-01 step 8's block plan upkeep and Build job posting (CON-12; M8-T6 perf breakdown).</summary>
    BlockBuild,
}

/// <summary>Optional diagnostics hook called around tick phases. The sim never reads time itself; the view supplies an
/// implementation that does (ADR-019). Implementations must not touch sim state.</summary>
public interface ITickProfiler
{
    void Begin(TickPhase phase);
    void End(TickPhase phase);
}
