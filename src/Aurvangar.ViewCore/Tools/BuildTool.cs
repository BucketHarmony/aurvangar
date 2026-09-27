using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Tools;

/// <summary>The build ghost under the cursor (VIEW-14): the footprint box (inclusive cells, min first), the entrance
/// cell (where the builder and worker will stand: <see cref="BuildingSystem.PlannedStandCell"/>, one level up for a
/// pump on a stepped bank, ADR-055; null for a building with no entrance, the levee, ADR-076), and the sim's placement
/// verdict. Green when <see cref="Ok"/>, red otherwise with <see cref="Reason"/>.</summary>
public readonly record struct BuildGhost(BuildingDef Def, Int3 Origin, int Rotation, Int3 Min, Int3 Max, Int3? Entrance,
    PlacementResult Result)
{
    public bool Ok => Result == PlacementResult.Ok;

    /// <summary>Player-facing reason for a red ghost, or null when it is green.</summary>
    public string? Reason => BuildTool.ReasonText(Result);
}

/// <summary>What a build-tool click did: the command to enqueue (null when nothing is sent), whether the tool stays
/// active, and a message for the player (the reason a red ghost was not placed).</summary>
public readonly record struct BuildClick(ICommand? Command, bool KeepTool, string? Message);

/// <summary>Build tool state (VIEW-12, VIEW-14; ADR-044). Engine-neutral: the Godot layer shows <see cref="Ghost"/>
/// and enqueues the commands that <see cref="Press"/> and <see cref="Drag"/> return.
/// <list type="bullet">
/// <item>The ghost's origin is the empty cell in front of the picked face (<see cref="PickHit.Adjacent"/>), so on
/// flat ground the building stands on the picked block.</item>
/// <item>R rotates by 90 degrees; B cycles through the buildable definitions (content order, prebuilt-only ones such
/// as the Great Hall left out).</item>
/// <item>A click on a green ghost sends <c>PlaceBuilding</c>; a red ghost sends nothing and returns the reason.
/// Without Shift the tool then returns to Select; with Shift it stays, and dragging places one more building at
/// each new green origin the cursor reaches (levee lines).</item>
/// </list></summary>
public sealed class BuildTool
{
    private readonly List<BuildingDef> _defs;
    private readonly HashSet<Int3> _sentThisDrag = new();
    private int _index;

    public BuildTool(ContentDb content)
    {
        _defs = content.Buildings.Where(d => !d.PrebuiltOnly).ToList();
        if (_defs.Count == 0) throw new InvalidOperationException("BuildTool: no buildable building definitions");
    }

    /// <summary>Buildable definitions in content order (warehouse, pump, levee).</summary>
    public IReadOnlyList<BuildingDef> Buildable => _defs;

    public BuildingDef Def => _defs[_index];
    public int Rotation { get; private set; }

    /// <summary>True between a press and its release.</summary>
    public bool Held { get; private set; }

    /// <summary>Chooses the building by id (toolbar menu). Unknown ids are ignored.</summary>
    public void Select(string defId)
    {
        int i = _defs.FindIndex(d => d.Id == defId);
        if (i >= 0) _index = i;
    }

    /// <summary>B while the build tool is active: the next buildable definition, wrapping around.</summary>
    public void Cycle() => _index = (_index + 1) % _defs.Count;

    /// <summary>R: rotate by 90 degrees (BLD-01).</summary>
    public void Rotate() => Rotation = (Rotation + 90) % 360;

    /// <summary>The ghost for the current definition and rotation at the pick, or null over nothing.</summary>
    public BuildGhost? Ghost(Simulation sim, PickHit? hit) => hit is { } h ? GhostAt(sim, Def, h.Adjacent, Rotation) : null;

    /// <summary>The ghost of <paramref name="def"/> at an origin: footprint box, stand cell and
    /// <see cref="BuildingSystem.CanPlace"/> (a read-only query).</summary>
    public static BuildGhost GhostAt(Simulation sim, BuildingDef def, Int3 origin, int rotation)
    {
        var min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
        var max = new Int3(int.MinValue, int.MinValue, int.MinValue);
        foreach (var c in BuildingShape.Footprint(def, origin, rotation))
        {
            min = new Int3(Math.Min(min.X, c.X), Math.Min(min.Y, c.Y), Math.Min(min.Z, c.Z));
            max = new Int3(Math.Max(max.X, c.X), Math.Max(max.Y, c.Y), Math.Max(max.Z, c.Z));
        }
        Int3? entrance = def.HasEntrance ? sim.Buildings.PlannedStandCell(def, origin, rotation) : null;
        return new BuildGhost(def, origin, rotation, min, max, entrance, sim.Buildings.CanPlace(def, origin, rotation));
    }

    /// <summary>Left button down.</summary>
    public BuildClick Press(Simulation sim, PickHit? hit, bool shift)
    {
        Held = true;
        _sentThisDrag.Clear();
        if (Ghost(sim, hit) is not { } g) return new BuildClick(null, true, null);
        if (!g.Ok) return new BuildClick(null, true, $"Can't build {g.Def.Name}: {g.Reason}");
        _sentThisDrag.Add(g.Origin);
        return new BuildClick(Command(g), shift, null);
    }

    /// <summary>Mouse moved while the button is held: with Shift, a new green origin sends one more building.</summary>
    public ICommand? Drag(Simulation sim, PickHit? hit, bool shift)
    {
        if (!Held || !shift || Ghost(sim, hit) is not { Ok: true } g || !_sentThisDrag.Add(g.Origin)) return null;
        return Command(g);
    }

    /// <summary>Left button up (or the tool changed).</summary>
    public void Release()
    {
        Held = false;
        _sentThisDrag.Clear();
    }

    private static PlaceBuilding Command(BuildGhost g) => new(g.Def.Id, g.Origin, g.Rotation);

    /// <summary>Tooltip text: name and cost, then the reason when the ghost is red, and the controls.</summary>
    public static string Tooltip(Simulation sim, BuildGhost g)
    {
        var cost = string.Join(", ", Construction.Cost(sim, g.Def).Select(c => $"{c.Count} {sim.Content.ItemDef(c.Item).Name.ToLowerInvariant()}"));
        string head = cost.Length > 0 ? $"{g.Def.Name} ({cost})" : g.Def.Name;
        return g.Ok ? $"{head}\nR rotate, B next building, Shift keeps the tool" : $"{head}\n{g.Reason}";
    }

    /// <summary>Player-facing text for a placement result (null for Ok).</summary>
    public static string? ReasonText(PlacementResult r) => r switch
    {
        PlacementResult.Ok => null,
        PlacementResult.OutOfBounds => "Outside the map",
        PlacementResult.Overlaps => "Overlaps another building or its entrance",
        PlacementResult.NotOnGround => "Needs solid ground under it",
        PlacementResult.FootprintBlocked => "Something is in the way",
        PlacementResult.EntranceBlocked => "The entrance is blocked",
        PlacementResult.NeedsWaterEdge => "Must stand on a bank with water in front",
        PlacementResult.PrebuiltOnly => "Cannot be built",
        PlacementResult.BadRotation => "Bad rotation",
        PlacementResult.PlannedBlocks => "Blocks are planned here",
        PlacementResult.NoStandCell => "No room for a builder beside it",
        _ => r.ToString(),
    };
}
