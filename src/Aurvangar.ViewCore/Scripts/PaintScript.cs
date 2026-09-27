using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The screenshot harness's <c>paint</c> script (M9-T1, VIEW-21, VIEW-20): single blocks painted with the
/// player's <see cref="BlockTool"/>, the way a player drags them. It uses the <see cref="BlocksScript.Find"/> site
/// (x0, y, z0; ground level y, courses from y + 1). Timed: the commands of each drag are enqueued at their tick.
/// <list type="bullet">
/// <item>Tick 0: course 1, Wood planks, released: a drag from (x0+1, z0+2) east to (x0+7, z0+2), then south to
/// (x0+7, z0+5): an L of 10 blocks.</item>
/// <item>Tick 1: course 2, Wood planks, in plan mode: a drag over the top of the L's long arm, (x0+1..x0+7, z0+2),
/// 7 planned blocks, supported by course 1's entries.</item>
/// </list>
/// The harness then holds a Stone wall drag in progress (<see cref="LiveDrag"/>), a diagonal from (x0+11, z0+1)
/// toward (x0+4, z0+4) on the ground layer: a face-connected staircase of cells, red where it crosses the built L and
/// amber elsewhere while no stone is stored (M10-T2).
/// The <c>paint</c> camera preset looks at the site.
/// <para>The <c>wall</c> script (M10-T3) runs the same drags, and the harness holds a vertical Wood planks drag instead
/// (<see cref="WallDrag"/>): from the east face of the L's short-arm end, a zigzag up and down that paints a wall face
/// <see cref="WallWidth"/> wide and <see cref="WallHeight"/> high beside the short arm. It costs more logs than are
/// free, so its top cells are amber. The <c>wall</c> camera preset looks at it from the east.</para></summary>
public static class PaintScript
{
    public const int Course1Blocks = 10, Course2Blocks = 7;

    /// <summary>The held vertical drag of the <c>wall</c> script: <see cref="WallWidth"/> columns of
    /// <see cref="WallHeight"/> cells.</summary>
    public const int WallWidth = 4, WallHeight = 6, WallBlocks = WallWidth * WallHeight;

    /// <summary>The wall's plane (dx) and its columns (dz from <see cref="WallZ0"/> down to WallZ0 - WallWidth + 1).</summary>
    public const int WallDx = 8, WallZ0 = 5;

    /// <summary>The cursor path of a drag: the first cell (its pick is the top face of the cell below it), then the
    /// cursor positions. Cells are relative to the site corner (dx, dy, dz), dy = 1 for the first course.</summary>
    private static readonly Int3[] Course1 = { new(1, 1, 2), new(7, 1, 2), new(7, 1, 5) };
    private static readonly Int3[] Course2 = { new(1, 2, 2), new(7, 2, 2) };
    private static readonly Int3[] Live = { new(11, 1, 1), new(4, 1, 4) };

    /// <summary>The site: recovered from the planned course 2 once the script ran (or from course 1's entries between
    /// ticks 0 and 1), else <see cref="BlocksScript.Find"/>.</summary>
    public static Int3? Site(Simulation sim) =>
        FromEntries(sim, PlanState.Planned, Course2[0]) ?? FromEntries(sim, PlanState.Released, Course1[0]) ?? BlocksScript.Find(sim);

    private static Int3? FromEntries(Simulation sim, PlanState state, Int3 offset)
    {
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        foreach (var (c, e) in sim.Plans.All)
        {
            if (e.State != state || e.Block != BlockId.Planks) continue;
            minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); minZ = Math.Min(minZ, c.Z);
        }
        return minX == int.MaxValue ? null : new Int3(minX - offset.X, minY - offset.Y, minZ - offset.Z);
    }

    /// <summary>Enqueues the drag due at the sim's current tick (0 and 1), painted with a <see cref="BlockTool"/>.</summary>
    public static void EnqueueDue(Simulation sim)
    {
        long tick = sim.Clock.Tick;
        if (tick > 1 || Site(sim) is not { } origin) return;
        var tool = new BlockTool(sim.Content);
        tool.Select(BlockId.Planks);
        if (tick == 1) tool.TogglePlan();
        Paint(tool, origin, tick == 0 ? Course1 : Course2);
        foreach (var c in tool.Release(sim).Commands) sim.Enqueue(c);
    }

    /// <summary>The harness's drag in progress: the pick it starts on and the cursor cells after it (null without a
    /// site).</summary>
    public static (PickHit Start, IReadOnlyList<Int3> Path)? LiveDrag(Simulation sim)
    {
        if (Site(sim) is not { } s) return null;
        var first = s + Live[0];
        return (new PickHit(first + Int3.Down, Int3.Up), Live.Skip(1).Select(p => s + p).ToList());
    }

    /// <summary>The <c>wall</c> script's held vertical drag: the east face of the L's short-arm end (dx 7, dz 5), then
    /// the cursor zigzags up and down the columns of the plane dx = <see cref="WallDx"/> (null without a site).</summary>
    public static (PickHit Start, IReadOnlyList<Int3> Path)? WallDrag(Simulation sim)
    {
        if (Site(sim) is not { } s) return null;
        var path = new List<Int3>();
        for (int k = 0; k < WallWidth; k++)
        {
            int z = WallZ0 - k;
            bool up = k % 2 == 0;
            if (k > 0) path.Add(s + new Int3(WallDx, up ? 1 : WallHeight, z));
            path.Add(s + new Int3(WallDx, up ? WallHeight : 1, z));
        }
        return (new PickHit(s + new Int3(WallDx - 1, 1, WallZ0), Int3.East), path);
    }

    private static void Paint(BlockTool tool, Int3 site, IReadOnlyList<Int3> path)
    {
        var first = site + path[0];
        tool.Press(new PickHit(first + Int3.Down, Int3.Up));
        for (int k = 1; k < path.Count; k++) tool.DragTo(site + path[k]);
    }
}
