using Aurvangar.Sim;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>The farm tool's mouse label (M6-T5): crops grow only on moist tiles (ECO-12, ECO-15), so the tool says
/// whether the hovered column is moist, and during a drag how many columns of the rectangle are. The drag itself is
/// <see cref="ToolController"/> (<see cref="ToolKind.Farm"/> sends <c>DesignateFarm</c>). Pure read of
/// <see cref="Simulation.Moisture"/>, which reflects the last recompute (every 50 ticks).</summary>
public static class FarmTool
{
    public const string MoistText = "Moist: crops grow here";
    public const string DryText = "Dry: crops will not grow here";

    /// <summary>Label for a drag <paramref name="box"/> (inclusive cells, only X/Z matter) or else for the hovered
    /// column; null when there is neither.</summary>
    public static string? Tooltip(Simulation sim, PickHit? hover, (Int3 Min, Int3 Max)? box)
    {
        if (box is var (min, max))
        {
            int total = 0, moist = 0;
            for (int z = min.Z; z <= max.Z; z++)
                for (int x = min.X; x <= max.X; x++)
                {
                    if (x < 0 || z < 0 || x >= sim.World.SizeX || z >= sim.World.SizeZ) continue;
                    total++;
                    if (sim.Moisture.IsMoist(x, z)) moist++;
                }
            return $"{moist} of {total} columns moist";
        }
        if (hover is not { } h) return null;
        return sim.Moisture.IsMoist(h.Cell.X, h.Cell.Z) ? MoistText : DryText;
    }
}
