using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Screenshots;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The screenshot harness's <c>blocks</c> script (M8-T5, VIEW-20): block construction in every state near
/// the hub. On a flat, dry <see cref="SiteW"/> x <see cref="SiteD"/> site (x0.., z0..) at ground level y:
/// <list type="bullet">
/// <item>a released Wood planks wall (z0+2, x0..x0+5, 2 high; 12 of the wagon's 20 planks, M11-T4), built first;</item>
/// <item>a released Stone wall (z0+4, x0..x0+5, 3 high; 18 stone), built as the digchop pit's stone comes in;</item>
/// <item>a planned Polished stone box (x0+8..x0+12, z0+2..z0+6, 4 high), never released: translucent ghosts and the
/// "Planned" material in the top bar.</item>
/// </list>
/// The harness also holds a Stone wall drag of the block tool across the planks wall (<see cref="GhostDrag"/>), so the
/// tool ghost shows red cells with a reason. The <c>blocks</c> camera preset looks at the site.</summary>
public static class BlocksScript
{
    public const int SiteW = 13, SiteD = 8;
    public const int BoxDx = 8, BoxDz = 2;

    /// <summary>The site (x0, y, z0), y the ground level: recovered from the planned box once the script ran, else
    /// searched in the world (<see cref="Find"/>). Null when there is none.</summary>
    public static Int3? Site(Simulation sim)
    {
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        foreach (var (c, e) in sim.Plans.All)
        {
            if (e.State != PlanState.Planned || e.Block != BlockId.PolishedStone) continue;
            minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); minZ = Math.Min(minZ, c.Z);
        }
        if (minX != int.MaxValue) return new Int3(minX - BoxDx, minY - 1, minZ - BoxDz);
        return Find(sim);
    }

    public static IReadOnlyList<ICommand> Commands(Simulation sim)
    {
        var list = new List<ICommand> { ScreenshotScripts.PitDig(sim) };
        if (Find(sim) is not { } s) return list;
        int y = s.Y + 1;
        list.Add(new DesignateBuild(BuildShape.Wall, new Int3(s.X, y, s.Z + 2), new Int3(s.X + 5, y, s.Z + 2), 2, BlockId.Planks, false));
        list.Add(new DesignateBuild(BuildShape.Wall, new Int3(s.X, y, s.Z + 4), new Int3(s.X + 5, y, s.Z + 4), 3, BlockId.Masonry, false));
        list.Add(new DesignateBuild(BuildShape.HollowBox, new Int3(s.X + BoxDx, y, s.Z + BoxDz),
            new Int3(s.X + BoxDx + 4, y, s.Z + BoxDz + 4), 4, BlockId.PolishedStone, true));
        return list;
    }

    /// <summary>The block tool drag the harness shows: a Stone wall across the planks wall (its cells there are red,
    /// "Something solid is there", once the planks are built).</summary>
    public static (PickHit From, PickHit To)? GhostDrag(Simulation sim)
    {
        if (Site(sim) is not { } s) return null;
        return (new PickHit(new Int3(s.X + 2, s.Y, s.Z), Int3.Up), new PickHit(new Int3(s.X + 2, s.Y, s.Z + 3), Int3.Up));
    }

    /// <summary>The nearest site to the hub (ring by ring on its corner) whose columns share one ground level, are dry
    /// and plannable one above (CON-08), and stay two cells clear of the pit. Deterministic.</summary>
    public static Int3? Find(Simulation sim)
    {
        var focus = ScreenshotPresets.HubFocus(sim);
        int cx = (int)focus.X, cz = (int)focus.Z;
        var (pitMin, pitMax) = ScreenshotScripts.PitBox(sim);
        for (int r = ScreenshotScripts.BuildGap + 2; r <= ScreenshotScripts.SiteSearchRadius; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    int x0 = cx + dx, z0 = cz + dz;
                    if (x0 + SiteW - 1 >= pitMin.X - 2 && x0 <= pitMax.X + 2 && z0 + SiteD - 1 >= pitMin.Z - 2 && z0 <= pitMax.Z + 2)
                        continue;
                    if (SiteOk(sim, x0, z0) is { } y) return new Int3(x0, y, z0);
                }
        return null;
    }

    private static int? SiteOk(Simulation sim, int x0, int z0)
    {
        var w = sim.World;
        if (x0 < 1 || z0 < 1 || x0 + SiteW >= w.SizeX || z0 + SiteD >= w.SizeZ) return null;
        int y = ScreenshotPresets.SurfaceY(sim, x0, z0);
        if (y < 1 || y + 6 >= w.SizeY) return null;
        var cells = new List<Int3>(SiteW * SiteD);
        for (int z = z0; z < z0 + SiteD; z++)
            for (int x = x0; x < x0 + SiteW; x++)
            {
                if (ScreenshotPresets.SurfaceY(sim, x, z) != y || sim.Water.GetLevel(new Int3(x, y + 1, z)) > 0) return null;
                cells.Add(new Int3(x, y + 1, z));
            }
        foreach (var r in BlockPlans.CanPlanAll(sim, cells))
            if (r != PlanResult.Ok) return null;
        return y;
    }
}
