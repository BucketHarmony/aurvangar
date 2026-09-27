using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Commands;

/// <summary>CON-07 (M8-T2): paint construction blocks. The cells are <see cref="BuildShapes.Cells"/>; valid ones
/// (CON-08) get a plan entry, <c>Planned</c> when <paramref name="Plan"/> is true, else <c>Released</c>.</summary>
public sealed record DesignateBuild(BuildShape Shape, Int3 A, Int3 B, int Height, BlockId Block, bool Plan) : ICommand
{
    public string Tag => "DesignateBuild";

    public void Apply(Simulation sim) => BlockBuildSystem.Designate(sim, this);
}
