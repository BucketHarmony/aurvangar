using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Scripts;

namespace Aurvangar.ViewCore.Screenshots;

/// <summary>Optional command scripts for the screenshot harness (<c>--script</c>, VIEW-20), so shots can show
/// colonists at work. Commands are computed from the world, like the presets (ADR-020).
/// <list type="bullet">
/// <item><c>none</c>: no commands (the default).</item>
/// <item><c>digchop</c>: dig a <see cref="PitDepth"/>-deep pit of <see cref="PitLength"/>×<see cref="PitWidth"/> cells starting
/// <see cref="PitGap"/> cells east of the hub, and chop every tree within <see cref="ChopRadius"/> cells of the hub
/// center (Chebyshev on X/Z).</item>
/// <item><c>build</c> (M5-T6): the <c>digchop</c> chop order plus a warehouse, a pump and a
/// line of <see cref="LeveeCount"/> levees, each at the nearest valid site at least <see cref="BuildGap"/> cells
/// from the hub (<see cref="FindSite"/>). The pump needs no water to be placed (ADR-040); on seed 1 the nearest site
/// is a dry terrace step, so the shots also show its no-water icon. With <c>--ticks 200</c> the shots show buildings in several states; by
/// tick 1600 all of them are complete (1200 before the M6-T3 berry picking, ADR-048; since M11-T2 the wagon's logs finish
/// them by about tick 400, so the mid-build tick moved from 500 to 200, ADR-077).</item>
/// <item><c>farm</c> (M6-T5): a <see cref="FarmSize"/>�<see cref="FarmSize"/> field (<see cref="FindFarm"/>) on the
/// nearest moist ground to the hub. <c>--ticks 4000</c> shows growing crops; by 9000 (before the day-5 drought) the
/// first are mature and harvested. The <c>farm</c> camera preset looks at it.</item>
/// <item><c>survival</c> (M7-T7): the timed <see cref="SurvivalScript"/> (seed 1); <see cref="Run"/> enqueues each
/// command at its tick. <c>--ticks 7400</c> shows the flooded hill tunnel, 9000 its breach levees, 14400 the drought
/// (the <c>tunnel</c> and <c>reservoir</c> presets).</item>
/// <item><c>blocks</c> (M8-T5): the <c>digchop</c> pit (stone) plus <see cref="BlocksScript"/>: a planks wall, a stone
/// wall and a planned polished-stone box by the hub, and a block-tool drag held over them. <c>--ticks 2500</c> shows
/// the planks wall built and the stone wall going up; the <c>blocks</c> preset looks at it.</item>
/// <item><c>monument</c> (M8-T6): the timed <see cref="MonumentScript"/> (seed 1): a hill quarry and a planned stone
/// tower with a walled courtyard, released course by course from tick 9600. <c>--ticks 11000</c> shows the lower
/// courses, 15000 the tower half built, 18600 the finished monument (the <c>monument</c> preset).</item>
/// <item><c>paint</c> (M9-T1): the timed <see cref="PaintScript"/>: single blocks painted along drags with the player's
/// block tool (a released planks L, a planned second course), and a stone drag held in progress. The <c>paint</c>
/// preset looks at it.</item>
/// <item><c>wall</c> (M10-T3): the same drags as <c>paint</c>; the harness holds a vertical Wood planks drag up a wall
/// face (<see cref="PaintScript.WallDrag"/>). The <c>wall</c> preset looks at it.</item>
/// <item><c>stairs</c> (M11-T8): <see cref="StairScript"/>, a stair-down dig to stone with a quarry room and a
/// straight-sided pit beside it. <c>--ticks 500</c> shows the stair being dug; later the pit's waiting cells say "would
/// trap a dwarf". The harness holds the dig tool in stair mode and hovers a waiting cell. The <c>stairs</c> preset
/// looks at it.</item>
/// </list></summary>
public static class ScreenshotScripts
{
    public static readonly IReadOnlyList<string> Names = new[] { "none", "digchop", "build", "farm", "survival", "blocks", "monument", "paint", "materials", "wall", "stairs" };

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
            case "survival":   // timed: its commands are enqueued at their ticks by Run
            case "monument":
            case "paint":
            case "wall":
                return Array.Empty<ICommand>();
            case "digchop":
                return new ICommand[] { PitDig(sim), ChopNearHub(sim) };
            case "blocks":
                return BlocksScript.Commands(sim);
            case "materials":
                return MaterialsScript.Commands(sim);
            case "stairs":
                return StairScript.Commands(sim);
            case "build":
                return BuildScript(sim);
            case "farm":
                return FindFarm(sim) is { } farm ? new ICommand[] { farm } : Array.Empty<ICommand>();
            default:
                throw new ArgumentException($"unknown screenshot script '{name}' (known: {string.Join(",", Names)})");
        }
    }

    /// <summary>The <c>digchop</c> pit: <see cref="PitDepth"/> deep, <see cref="PitLength"/> x <see cref="PitWidth"/>
    /// cells, starting <see cref="PitGap"/> cells east of the hub.</summary>
    public static DesignateDig PitDig(Simulation sim)
    {
        var (min, max) = PitBox(sim);
        return new DesignateDig(min, max);
    }

    public static (Int3 Min, Int3 Max) PitBox(Simulation sim)
    {
        var hub = sim.Buildings.All.FirstOrDefault()
            ?? throw new InvalidOperationException("the pit needs the pre-placed hub");
        int maxX = int.MinValue;
        foreach (var c in hub.FootprintCells()) maxX = Math.Max(maxX, c.X);
        var focus = ScreenshotPresets.HubFocus(sim);
        int cz = (int)focus.Z, floor = hub.Origin.Y - 1;
        int x0 = maxX + 1 + PitGap, half = PitWidth / 2;
        return (new Int3(x0, floor - PitDepth + 1, cz - half), new Int3(x0 + PitLength - 1, floor, cz + half));
    }

    /// <summary>True for a script whose commands have their own ticks (<c>survival</c>, <c>monument</c>, <c>paint</c>).</summary>
    public static bool IsTimed(string name) => name is "survival" or "monument" or "paint" or "wall";

    /// <summary>Enqueues the commands of timed script <paramref name="name"/> due at the sim's current tick.</summary>
    public static void EnqueueDue(string name, Simulation sim)
    {
        if (name == "monument") MonumentScript.EnqueueDue(sim);
        else if (name == "survival") SurvivalScript.EnqueueDue(sim);
        else if (name is "paint" or "wall") PaintScript.EnqueueDue(sim);
    }

    /// <summary>Runs <paramref name="ticks"/> ticks of script <paramref name="name"/> the way the harness does: an
    /// untimed script's commands (<see cref="For"/>) are enqueued before tick 1; a timed script's commands are enqueued
    /// just before the tick that applies them (<see cref="EnqueueDue"/>). <paramref name="afterTick"/>
    /// runs after every tick (the harness drains the sim's events there).</summary>
    public static void Run(string name, Simulation sim, int ticks, Action? afterTick = null)
    {
        foreach (var command in For(name, sim)) sim.Enqueue(command);
        bool timed = IsTimed(name);
        for (int i = 0; i < ticks; i++)
        {
            if (timed) EnqueueDue(name, sim);
            sim.Tick();
            afterTick?.Invoke();
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

    /// <summary>M11-T1: a pick that shows a green ghost of <paramref name="def"/> beside the existing buildings of the
    /// same type (for the <c>build</c> script's levee line: the first free cell further along +x), else beside the
    /// hub's nearest valid site (<see cref="FindSite"/>). The pick is the ground block under that origin, top face.
    /// Null when there is no valid site.</summary>
    public static PickHit? GhostPickFor(Simulation sim, BuildingDef def)
    {
        var last = sim.Buildings.All.Where(b => b.Def.Id == def.Id).OrderBy(b => b.Id.Value).LastOrDefault();
        if (last is not null)
            for (int k = 1; k <= 6; k++)
            {
                var o = last.Origin + new Int3(k, 0, 0);
                if (sim.Buildings.CanPlace(def, o, 0) == PlacementResult.Ok) return new PickHit(o + Int3.Down, Int3.Up);
            }
        return FindSite(sim, def, new HashSet<Int3>()) is { } site ? new PickHit(site.Origin + Int3.Down, Int3.Up) : null;
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
        // M11-T2: keep clear of the buildings already standing (the hall and the wagon beside it), so the new sites do
        // not wall in the wagon's entrance.
        foreach (var b in sim.Buildings.All) Take(sim, b.Def, b.Origin, b.Rotation, taken);
        var list = new List<ICommand> { ChopNearHub(sim) };
        if (FindSite(sim, content.Building("warehouse"), taken) is { } wh) list.Add(wh);
        // The pump goes to the nearest site with water at its intake (on seed 1 a bank site that uses the stand cell
        // one level up, ADR-055), else to the nearest valid one.
        var pump = content.Building("pump");
        if ((FindSite(sim, pump, taken, wet: true) ?? FindSite(sim, pump, taken)) is { } ps) list.Add(ps);
        // A levee line: the first site, then the cells beside it along +x (a levee has no entrance, ADR-076) while they
        // stay valid and clear of the earlier buildings.
        var levee = content.Building("levee");
        var earlier = new HashSet<Int3>(taken);
        if (FindSite(sim, levee, taken) is { } first)
        {
            list.Add(first);
            var step = new Int3(1, 0, 0);
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
        return !def.HasEntrance || !taken.Contains(BuildingShape.Entrance(def, origin, rot));
    }

    private static void Take(Simulation sim, BuildingDef def, Int3 origin, int rot, HashSet<Int3> taken)
    {
        var cells = BuildingShape.Footprint(def, origin, rot).ToList();
        if (def.HasEntrance) cells.Add(BuildingShape.Entrance(def, origin, rot));
        foreach (var c in cells)
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        taken.Add(c + new Int3(dx, dy, dz));
    }
}
