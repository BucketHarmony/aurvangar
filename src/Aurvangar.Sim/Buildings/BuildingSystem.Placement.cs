using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Buildings;

/// <summary>M5-T1: placement validation (BLD-01..04, ADR-040) and blueprint creation (the state part of BLD-05).</summary>
public sealed partial class BuildingSystem
{
    /// <summary>BLD-01..04: whether <paramref name="def"/> may be placed as a blueprint at the origin and rotation.
    /// Checks run in a fixed order and the first failure is returned: rotation, prebuilt-only, bounds, overlap with
    /// any building (any state, including covering another building's entrance), footprint cells (air, no plant),
    /// ground under the bottom layer, the entrance cell, then the water edge for <c>waterEdge</c> buildings.</summary>
    public PlacementResult CanPlace(BuildingDef def, Int3 origin, int rotation)
    {
        if (!BuildingShape.IsValidRotation(rotation)) return PlacementResult.BadRotation;
        if (def.PrebuiltOnly) return PlacementResult.PrebuiltOnly;

        var footprint = BuildingShape.Footprint(def, origin, rotation).ToList();
        var entrance = BuildingShape.Entrance(def, origin, rotation);
        foreach (var c in footprint)
            if (!_world.InBounds(c)) return PlacementResult.OutOfBounds;
        if (!_world.InBounds(entrance)) return PlacementResult.OutOfBounds;

        foreach (var c in footprint)
            if (BuildingAt(c) is not null || IsAnyEntrance(c)) return PlacementResult.Overlaps;

        foreach (var c in footprint)
            if (_world.GetBlock(c) != World.BlockId.Air || _plants.IsOccupied(c)) return PlacementResult.FootprintBlocked;

        bool stacked = false;
        foreach (var c in BuildingShape.BottomLayer(def, origin, rotation))
        {
            var below = c + Int3.Down;
            var under = BuildingAt(below);
            if (under is not null && def.Stackable && under.Def.Id == def.Id
                && under.State is BuildingState.Blueprint or BuildingState.Complete)
            {
                stacked = true;   // BLD-04: on a blueprinted or complete building of the same stackable type
                continue;
            }
            bool ground = _world.IsSolid(below)
                && (under is null || (under.Def.Stackable && under.State == BuildingState.Complete));
            if (!ground) return PlacementResult.NotOnGround;
        }

        if (!EntranceOk(def, entrance, stacked)) return PlacementResult.EntranceBlocked;

        if (def.Placement == "waterEdge")
        {
            var front = BuildingShape.IntakeFront(def, origin, rotation);
            var intake = front + Int3.Down;
            if (!_world.InBounds(front) || !_world.InBounds(intake) || _world.IsSolid(front) || _world.IsSolid(intake))
                return PlacementResult.NeedsWaterEdge;
        }
        return PlacementResult.Ok;
    }

    /// <summary>BLD-05 (state only): validates with <see cref="CanPlace"/> and, if Ok, adds a building in state
    /// <c>Blueprint</c> with nothing delivered and no progress. Its footprint is then reserved: no other blueprint
    /// may overlap it. No blocks are written. The PlaceBuilding command (<see cref="Construction.Place"/>) adds the
    /// event and jobs.</summary>
    public PlacementResult TryPlaceBlueprint(BuildingDef def, Int3 origin, int rotation, out Building? building)
    {
        building = null;
        var r = CanPlace(def, origin, rotation);
        if (r != PlacementResult.Ok) return r;
        building = new Building
        {
            Id = new BuildingId(Ids.Allocate()), Def = def, Origin = origin, Rotation = rotation, State = BuildingState.Blueprint,
        };
        Add(building);
        return r;
    }

    /// <summary>The building (lowest id first, any state) whose footprint covers the cell, or null.</summary>
    public Building? BuildingAt(Int3 c)
    {
        if (_world.InBounds(c)) return _cellOwner.TryGetValue(_world.Index(c), out var id) ? _buildings[id] : null;
        foreach (var b in _buildings.Values)
            if (b.Covers(c)) return b;
        return null;
    }

    /// <summary>The cell a builder or worker would stand on for <paramref name="def"/> at this spot (the build ghost
    /// shows it): the entrance when it is free, else for a stackable building the cell below it (ADR-040), else for a
    /// <c>waterEdge</c> building the cell above it (ADR-055), else the entrance. A read-only query.</summary>
    public Int3 PlannedStandCell(BuildingDef def, Int3 origin, int rotation)
    {
        var e = BuildingShape.Entrance(def, origin, rotation);
        if (FreeStand(e)) return e;
        if (def.Stackable && FreeStand(e + Int3.Down)) return e + Int3.Down;
        if (RaisesStand(def) && FreeStand(e + Int3.Up)) return e + Int3.Up;
        return e;
    }

    /// <summary>ADR-055 (M7-T2, G3 answer 3b): a <c>waterEdge</c> building (the pump) whose entrance cell is taken by
    /// the next bank step may use the cell one level up; the footprint is still in reach from there (ARCH-07).</summary>
    internal static bool RaisesStand(BuildingDef def) => def.Placement == "waterEdge";

    private bool FreeStand(Int3 c) => BuildingAt(c) is null && _paths.IsStandable(c);

    /// <summary>A building's entrance, or for a <c>waterEdge</c> building also the cell above it (its possible raised
    /// stand cell, ADR-055): no footprint may cover either.</summary>
    private bool IsAnyEntrance(Int3 c)
    {
        foreach (var b in _buildings.Values)
        {
            var e = b.EntranceCell;
            if (e == c || (RaisesStand(b.Def) && e + Int3.Up == c)) return true;
        }
        return false;
    }

    /// <summary>BLD-02: the entrance is standable (PTH-01) and not inside any building. A stacked building
    /// (BLD-04) may instead use the cell one level below its entrance, from which the upper footprint cell is in
    /// reach (ARCH-07); ADR-040. A <c>waterEdge</c> building may use the cell one level above it (ADR-055).</summary>
    private bool EntranceOk(BuildingDef def, Int3 entrance, bool stacked)
    {
        if (FreeStand(entrance)) return true;
        if (stacked && FreeStand(entrance + Int3.Down)) return true;
        return RaisesStand(def) && FreeStand(entrance + Int3.Up);
    }
}
