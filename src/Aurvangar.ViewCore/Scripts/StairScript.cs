using Aurvangar.Sim;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The <c>stairs</c> screenshot script (M11-T8, VIEW-24/25): a stair-down dig on the flat ground north-east of
/// the Great Hall, heading north (-z) one step past the first stone step, with a 3 x <see cref="RoomLength"/> quarry room
/// two high at its foot, and west of it a straight-sided <see cref="PitSize"/>-cube pit whose deeper cells wait ("would
/// trap a dwarf"). <c>StairDigScenarioTests</c> proves the same tool east of the hall, down a slope. Computed from the
/// world (ADR-020).</summary>
public static class StairScript
{
    public const int HallGap = 3;
    public const int MaxSteps = 16;
    public const int RoomLength = 3;
    public const int PitSize = 3;
    /// <summary>The pit's east column, this many cells west of the stair's line.</summary>
    public const int PitDx = 2;

    /// <summary>The stair heads north.</summary>
    public static readonly Int3 Heading = new(0, 0, -1);

    public sealed record Plan(PickHit First, PickHit Second, int BottomY, Int3 RoomMin, Int3 RoomMax, Int3 PitMin, Int3 PitMax)
    {
        public int Steps => First.Cell.Y - BottomY;
    }

    /// <summary>The stair's first cell from the hall alone (x beside the hall, z just north of it; y the ground just
    /// south of the stair, which the dig leaves alone), so the camera preset does not move while the stair is dug.
    /// Null without a hall.</summary>
    public static Int3? Anchor(Simulation sim)
    {
        var hall = sim.Buildings.All.FirstOrDefault(b => b.Def.Id == Aurvangar.Sim.Jobs.DigStrand.HallDefId);
        if (hall is null) return null;
        int x0 = hall.FootprintCells().Max(c => c.X) + 1 + HallGap, z0 = hall.FootprintCells().Min(c => c.Z) - 2;
        return new Int3(x0, SurfaceY(sim.World, x0, z0 + 1), z0);
    }

    /// <summary>The stair, room and pit (computed before anything is dug), or null when the hall is missing or there is
    /// no stone within <see cref="MaxSteps"/> steps.</summary>
    public static Plan? Site(Simulation sim)
    {
        if (Anchor(sim) is not { } anchor) return null;
        int x0 = anchor.X, z0 = anchor.Z;
        int top = SurfaceY(sim.World, x0, z0);
        int n = 0;
        for (int k = 1; k < MaxSteps; k++)
            if (sim.World.GetBlock(new Int3(x0, top - k, z0 - k)) == BlockId.Stone) { n = k + 1; break; }
        if (n == 0) return null;
        int bottom = top - n;
        var first = new PickHit(new Int3(x0, top, z0), Int3.Up);
        var second = new PickHit(new Int3(x0, SurfaceY(sim.World, x0, z0 - n), z0 - n), Int3.Up);
        var roomMin = new Int3(x0 - 1, bottom, z0 - n - RoomLength);
        var roomMax = new Int3(x0 + 1, bottom + 1, z0 - n - 1);
        int px = x0 - PitDx - PitSize + 1, pitTop = SurfaceY(sim.World, px + 1, z0 - 1);
        var pitMin = new Int3(px, pitTop - PitSize + 1, z0 - PitSize);
        var pitMax = new Int3(px + PitSize - 1, pitTop, z0 - 1);
        return new Plan(first, second, bottom, roomMin, roomMax, pitMin, pitMax);
    }

    /// <summary>The stair columns (as the dig tool in stair mode sends them with the view level at the bottom step),
    /// the room, the pit, and a chop order over them.</summary>
    public static IReadOnlyList<ICommand> Commands(Simulation sim)
    {
        if (Site(sim) is not { } p) return Array.Empty<ICommand>();
        var list = new List<ICommand>(StairDig.Commands(p.First, p.Second, p.BottomY))
        {
            new DesignateDig(p.RoomMin, p.RoomMax),
            new DesignateDig(p.PitMin, p.PitMax),
            // Trees on the way would hold their floors (DSG-03): chop them.
            new DesignateChop(p.PitMin.X - 1, p.RoomMin.Z - 1, p.RoomMax.X + 1, p.First.Cell.Z + 1),
        };
        return list;
    }

    /// <summary>A pick for the mouse label: the first pit cell that would trap a dwarf, else the first marked cell
    /// that waits for another, else the first marked cell; null without marks.</summary>
    public static PickHit? TooltipPick(Simulation sim)
    {
        Int3? waiting = null, any = null;
        foreach (var (c, mark) in sim.Designations.All)
        {
            if (mark == DesignationMark.None) continue;
            var wait = DigStatus.Of(sim, c);
            if (wait == DigWait.WouldTrap) return new PickHit(c, Int3.Up);
            if (wait is DigWait.CellAbove or DigWait.Neighbour) waiting ??= c;
            any ??= c;
        }
        return (waiting ?? any) is { } cell ? new PickHit(cell, Int3.Up) : null;
    }

    private static int SurfaceY(VoxelWorld world, int x, int z)
    {
        for (int y = world.SizeY - 1; y > 0; y--)
            if (world.IsSolid(new Int3(x, y, z))) return y;
        return 0;
    }
}
