using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Commands;

/// <summary>CON-07 (M8-T2): paint construction blocks. The cells are <see cref="BuildShapes.Cells"/>; valid ones
/// (CON-08) get a plan entry, <c>Planned</c> when <paramref name="Plan"/> is true, else <c>Released</c>. Every entry
/// takes <paramref name="Form"/> (CON-19, M11-T10: the fine block shape and its rotation; Full by default).</summary>
public sealed record DesignateBuild(BuildShape Shape, Int3 A, Int3 B, int Height, BlockId Block, bool Plan,
    BlockForm Form = default) : ICommand
{
    public string Tag => "DesignateBuild";

    public void Apply(Simulation sim) => BlockBuildSystem.Designate(sim, this);
}

/// <summary>CON-07 (M8-T4): every <c>Planned</c> entry in the box (corners in any order) becomes <c>Released</c>.
/// Rejected with <c>NothingToRelease</c> when the box holds no Planned entry.</summary>
public sealed record ReleasePlan(Int3 A, Int3 B) : ICommand
{
    public string Tag => "ReleasePlan";

    public void Apply(Simulation sim)
    {
        if (sim.Plans.Release(A, B) == 0) sim.Events.Emit(new Events.CommandRejected(Tag, "NothingToRelease"));
    }
}
