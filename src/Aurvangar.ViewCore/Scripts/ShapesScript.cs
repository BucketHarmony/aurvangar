using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The <c>shapes</c> screenshot script (M11-T11, VIEW-27): a small build of fine shapes (CON-19) on the
/// <see cref="BlocksScript.Find"/> site (x0, y, z0; ground level y), all from the starting wagon's stock:
/// <list type="bullet">
/// <item>a Stone wall stair of <see cref="StairCount"/> steps climbing east (rotation 1) at z0 + 2, each step a stair on a
/// column of full blocks, up to a 3-high landing;</item>
/// <item>two Stone wall pillars <see cref="PillarHeight"/> high at x0 + 4 and x0 + 7 (z0 + 2) under a Wood planks slab
/// roof level with the landing's top;</item>
/// <item>a row of Slate tiles slabs and a row of Wood planks slabs at z0 + 5;</item>
/// <item>a planned (never released) line of Polished stone stairs climbing west at x0 + 9.. (z0 + 5): shaped plan
/// ghosts.</item>
/// </list>
/// The harness holds a Slate tiles stair drag of the block tool (<see cref="GhostDrag"/>) so the tool ghost shows the
/// shape. Everything released is built by <see cref="DoneTicks"/>. The <c>shapes</c> camera preset looks at it.</summary>
public static class ShapesScript
{
    public const int StairCount = 3;
    public const int PillarHeight = 2;
    public const int PillarCount = 2 * PillarHeight;
    public const int RoofLength = 4;
    public const int SlabRow = 4;
    public const int SlabCount = RoofLength + 2 * SlabRow;
    public const int PlannedDx = 9, PlannedDz = 5, PlannedLength = 3;

    /// <summary>Ticks by which every released entry is built on seed 1 (checked by BlockShapeToolTests).</summary>
    public const int DoneTicks = 2400;

    public static readonly BlockForm StairEast = new(BlockShape.Stair, 1);
    public static readonly BlockForm StairWest = new(BlockShape.Stair, 3);
    public static readonly BlockForm SlabForm = new(BlockShape.Slab, 0);
    public static readonly BlockForm PillarForm = new(BlockShape.Pillar, 0);
    public static readonly BlockForm StairSouth = new(BlockShape.Stair, 0);

    /// <summary>The site: recovered from the planned stair line once the script ran, else <see cref="BlocksScript.Find"/>.</summary>
    public static Int3? Site(Simulation sim)
    {
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        foreach (var (c, e) in sim.Plans.All)
        {
            if (e.State != PlanState.Planned || e.Form != StairWest) continue;
            minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); minZ = Math.Min(minZ, c.Z);
        }
        if (minX != int.MaxValue) return new Int3(minX - PlannedDx, minY - 1, minZ - PlannedDz);
        return BlocksScript.Find(sim);
    }

    public static IReadOnlyList<ICommand> Commands(Simulation sim)
    {
        if (BlocksScript.Find(sim) is not { } s) return Array.Empty<ICommand>();
        int x0 = s.X, y = s.Y + 1, z0 = s.Z;
        var list = new List<ICommand>();
        // The stair: step k is k full blocks with a stair on top; then a landing as high as the top step.
        int z = z0 + 2;
        for (int k = 0; k < StairCount; k++)
        {
            if (k > 0) list.Add(Build(BuildShape.Wall, new Int3(x0 + k, y, z), new Int3(x0 + k, y, z), k, BlockId.Masonry));
            list.Add(Build(BuildShape.Single, new Int3(x0 + k, y + k, z), new Int3(x0 + k, y + k, z), 1, BlockId.Masonry, StairEast));
        }
        list.Add(Build(BuildShape.Wall, new Int3(x0 + StairCount, y, z), new Int3(x0 + StairCount, y, z), StairCount, BlockId.Masonry));
        // Two pillars under a slab roof that starts beside the landing, so its first slab is laid from the landing's top
        // (a dwarf reaches one level up, CON-11) and the rest from the slabs already laid.
        int rx = x0 + StairCount + 1;
        foreach (int px in new[] { rx, rx + RoofLength - 1 })
            list.Add(Build(BuildShape.Wall, new Int3(px, y, z), new Int3(px, y, z), PillarHeight, BlockId.Masonry, PillarForm));
        list.Add(Build(BuildShape.Line, new Int3(rx, y + PillarHeight, z), new Int3(rx + RoofLength - 1, y + PillarHeight, z), 1,
            BlockId.Planks, SlabForm));
        // Two rows of slabs.
        list.Add(Build(BuildShape.Line, new Int3(x0, y, z0 + 5), new Int3(x0 + SlabRow - 1, y, z0 + 5), 1, BlockId.Slate, SlabForm));
        list.Add(Build(BuildShape.Line, new Int3(x0 + SlabRow, y, z0 + 5), new Int3(x0 + 2 * SlabRow - 1, y, z0 + 5), 1, BlockId.Planks, SlabForm));
        // Planned stairs: shaped plan ghosts.
        list.Add(new DesignateBuild(BuildShape.Line, new Int3(x0 + PlannedDx, y, z0 + PlannedDz),
            new Int3(x0 + PlannedDx + PlannedLength - 1, y, z0 + PlannedDz), 1, BlockId.PolishedStone, true, StairWest));
        return list;
    }

    /// <summary>The block tool drag the harness shows: Slate tiles stairs climbing south along x0 + 10..12 at z0.</summary>
    public static (PickHit Start, IReadOnlyList<Int3> Path)? GhostDrag(Simulation sim)
    {
        if (Site(sim) is not { } s) return null;
        var start = new PickHit(new Int3(s.X + 10, s.Y, s.Z), Int3.Up);
        return (start, new[] { new Int3(s.X + 11, s.Y + 1, s.Z), new Int3(s.X + 12, s.Y + 1, s.Z) });
    }

    private static DesignateBuild Build(BuildShape shape, Int3 a, Int3 b, int height, BlockId block, BlockForm form = default) =>
        new(shape, a, b, height, block, false, form);
}
