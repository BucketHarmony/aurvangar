using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Screenshots;

/// <summary>Optional command scripts for the screenshot harness (<c>--script</c>, VIEW-20), so shots can show
/// colonists at work. Commands are computed from the world, like the presets (ADR-020).
/// <list type="bullet">
/// <item><c>none</c>: no commands (the default).</item>
/// <item><c>digchop</c>: dig a <see cref="PitDepth"/>-deep pit of <see cref="PitLength"/>Ã—<see cref="PitWidth"/> cells starting
/// <see cref="PitGap"/> cells east of the hub, and chop every tree within <see cref="ChopRadius"/> cells of the hub
/// center (Chebyshev on X/Z).</item>
/// <item><c>build</c> (M5-T6): the <c>digchop</c> chop order plus a warehouse, a pump and a
/// line of <see cref="LeveeCount"/> levees, each at the nearest valid site at least <see cref="BuildGap"/> cells
/// from the hub (<see cref="FindSite"/>). The pump needs no water to be placed (ADR-040); on seed 1 the nearest site
/// is a dry terrace step, so the shots also show its no-water icon. With <c>--ticks 500</c> the shots show buildings in several states; by
/// tick 1600 all of them are complete (1200 before the M6-T3 berry picking, ADR-048).</item>
/// <item><c>farm</c> (M6-T5): a <see cref="FarmSize"/>×<see cref="FarmSize"/> field (<see cref="FindFarm"/>) on the
/// nearest moist ground to the hub. <c>--ticks 4000</c> shows growing crops; by 9000 (before the day-5 drought) the
/// first are mature and harvested. The <c>farm</c> camera preset looks at it.</item>
/// </list></summary>
public static class ScreenshotScripts
{
    public static readonly IReadOnlyList<string> Names = new[] { "none", "digchop", "build", "farm" };

    public const int BuildGap = 4;
    public const int LeveeCount = 3;
    public const int SiteSearchRadius = 64;

    public const int FarmSize = 5;

    public const int PitGap = 3;
    public const int PitLength = 14;
    public const int PitWidth = 9;
    public const int PitDepth = 2;
    public const int ChopRadius = 24;

    public static IReadOnlyList<ICommand> For(string name, Simulation sim)
    {
        switch (name)
        {
            case "none":
                return Array.Empty<ICommand>();
            case "digchop":
            {
                var hub = sim.Buildings.All.FirstOrDefault()
                    ?? throw new InvalidOperationException("digchop script needs the pre-placed hub");
                int maxX = int.MinValue;
                foreach (var c in hub.FootprintCells()) maxX = Math.Max(maxX, c.X);
                var focus = ScreenshotPresets.HubFocus(sim);
                int cx = (int)focus.X, cz = (int)focus.Z, floor = hub.Origin.Y - 1;
                int x0 = maxX + 1 + PitGap, half = PitWidth / 2;
                return new ICommand[]
                {
                    new DesignateDig(new Int3(x0, floor - PitDepth + 1, cz - half), new Int3(x0 + PitLength - 1, floor, cz + half)),
                    ChopNearHub(sim),
                };
            }
            case "build":
                return BuildScript(sim);
            case "farm":
                return FindFarm(sim) is { } farm ? new ICommand[] { farm } : Array.Empty<ICommand>();
            default:
                throw new ArgumentException($"unknown screenshot script '{name}' (known: {string.Join(",", Names)})");
        }
    }

    /// <summary>A fixed pick for the harness to show the build tool's ghost with (picking is off in shots): for
    /// <c>build</c>, the middle of the Great Hall's roof, where a warehouse ghost is red ("Needs solid ground under
    /// it"). Null for the other scripts.</summary>
    public static PickHit? GhostPick(string name, Simulation sim)
    {
        if (name != "build" || sim.Buildings.All.FirstOrDefault() is not { } hub) return null;
        var focus = ScreenshotPresets.HubFocus(sim);
        int top = hub.FootprintCells().Max(c => c.Y);
        return new PickHit(new Int3((int)focus.X, top, (int)focus.Z), Int3.Up);
    }

    /// <summary>The nearest <see cref="FarmSize"/>-square field to the hub (ring by ring on its min corner, like
    /// <see cref="FindSite"/>) whose every column would become a farm tile (ECO-11: top block Grass or Dirt with
    /// standable air above, no building) and is moist. Moisture is not computed before the first tick, so it is
    /// evaluated on a private <see cref="MoistureMap"/> over the sim's world and water (read only). Null if none.</summary>
    public static DesignateFarm? FindFarm(Simulation sim)
    {
        var moisture = new MoistureMap(sim.World, sim.Water);
        moisture.Recompute();
        var focus = ScreenshotPresets.HubFocus(sim);
        int cx = (int)focus.X, cz = (int)focus.Z;
        for (int r = BuildGap + 2; r <= SiteSearchRadius; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    int x0 = cx + dx, z0 = cz + dz;
                    if (FieldOk(sim, moisture, x0, z0))
                        return new DesignateFarm(x0, z0, x0 + FarmSize - 1, z0 + FarmSize - 1);
                }
        return null;
    }

    private static bool FieldOk(Simulation sim, MoistureMap moisture, int x0, int z0)
    {
        for (int z = z0; z < z0 + FarmSize; z++)
            for (int x = x0; x < x0 + FarmSize; x++)
            {
                if (x < 0 || z < 0 || x >= sim.World.SizeX || z >= sim.World.SizeZ || !moisture.IsMoist(x, z)) return false;
                int y = moisture.SurfaceY(x, z);
                if (y < 0) return false;
                var c = new Int3(x, y, z);
                if (sim.World.GetBlock(c) is not (BlockId.Grass or BlockId.Dirt)) return false;
                if (!sim.PathGrid.IsStandable(c + Int3.Up)) return false;
                if (sim.Buildings.BuildingAt(c) is not null || sim.Buildings.BuildingAt(c + Int3.Up) is not null) return false;
            }
        return true;
    }

    private static DesignateChop ChopNearHub(Simulation sim)
    {
        var focus = ScreenshotPresets.HubFocus(sim);
        int cx = (int)focus.X, cz = (int)focus.Z;
        return new DesignateChop(cx - ChopRadius, cz - ChopRadius, cx + ChopRadius, cz + ChopRadius);
    }

    private static IReadOnlyList<ICommand> BuildScript(Simulation sim)
    {
        var content = sim.Content;
        var taken = new HashSet<Int3>();
        var list = new List<ICommand> { ChopNearHub(sim) };
        if (FindSite(sim, content.Building("warehouse"), taken) is { } wh) list.Add(wh);
        // The pump goes to the nearest site with water at its intake (on seed 1 a bank site that uses the stand cell
        // one level up, ADR-055), else to the nearest valid one.
        var pump = content.Building("pump");
        if ((FindSite(sim, pump, taken, wet: true) ?? FindSite(sim, pump, taken)) is { } ps) list.Add(ps);
        // A levee line: the first site, then the cells beside it (across the entrance direction, so no levee covers
        // another's entrance) while they stay valid and clear of the earlier buildings.
        var levee = content.Building("levee");
        var earlier = new HashSet<Int3>(taken);
        if (FindSite(sim, levee, taken) is { } first)
        {
            list.Add(first);
            var e = BuildingShape.Entrance(levee, first.Origin, first.Rotation) - first.Origin;
            var step = e.Z != 0 ? new Int3(1, 0, 0) : new Int3(0, 0, 1);
            for (int i = 1; i < LeveeCount; i++)
            {
                var o = first.Origin + new Int3(step.X * i, 0, step.Z * i);
                if (sim.Buildings.CanPlace(levee, o, first.Rotation) != PlacementResult.Ok || !Free(sim, levee, o, first.Rotation, earlier)) break;
                Take(sim, levee, o, first.Rotation, taken);
                list.Add(new PlaceBuilding(levee.Id, o, first.Rotation));
            }
        }
        return list;
    }

    /// <summary>The nearest valid site (ring by ring around the hub center, Chebyshev distance from
    /// <see cref="BuildGap"/> + hub half-size, rotations 0/90/180/270) that does not touch the cells already
    /// <paramref name="taken"/> (footprints and entrances of earlier picks, with a one-cell margin). With
    /// <paramref name="wet"/>, only sites whose producer intake holds at least its <c>minIntakeLevel</c>. Deterministic.</summary>
    public static PlaceBuilding? FindSite(Simulation sim, BuildingDef def, HashSet<Int3> taken, bool wet = false)
    {
        var focus = ScreenshotPresets.HubFocus(sim);
        int cx = (int)focus.X, cz = (int)focus.Z;
        for (int r = BuildGap + 2; r <= SiteSearchRadius; r++)
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= sim.World.SizeX || z >= sim.World.SizeZ) continue;
                    var origin = new Int3(x, ScreenshotPresets.SurfaceY(sim, x, z) + 1, z);
                    for (int rot = 0; rot < 360; rot += 90)
                    {
                        if (sim.Buildings.CanPlace(def, origin, rot) != PlacementResult.Ok || !Free(sim, def, origin, rot, taken)) continue;
                        if (wet && (def.Producer is not { } p
                            || sim.Water.GetLevel(BuildingShape.Intake(def, origin, rot)) < p.MinIntakeLevel)) continue;
                        Take(sim, def, origin, rot, taken);
                        return new PlaceBuilding(def.Id, origin, rot);
                    }
                }
        return null;
    }

    private static bool Free(Simulation sim, BuildingDef def, Int3 origin, int rot, HashSet<Int3> taken)
    {
        foreach (var c in BuildingShape.Footprint(def, origin, rot)) if (taken.Contains(c)) return false;
        return !taken.Contains(BuildingShape.Entrance(def, origin, rot));
    }

    private static void Take(Simulation sim, BuildingDef def, Int3 origin, int rot, HashSet<Int3> taken)
    {
        var cells = BuildingShape.Footprint(def, origin, rot).Append(BuildingShape.Entrance(def, origin, rot)).ToList();
        foreach (var c in cells)
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        taken.Add(c + new Int3(dx, dy, dz));
    }
}
