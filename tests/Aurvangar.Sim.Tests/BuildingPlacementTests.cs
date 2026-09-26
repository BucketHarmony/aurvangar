using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>M5-T1: rotation and placement validation, BLD-01..04 (buildings.md acceptance scenario 1).
/// Worlds are flat stone to y = 4, so the ground surface (first air cell) is y = 5.</summary>
[Trait("Category", "Unit")]
public class BuildingPlacementTests
{
    private const int G = 5;   // first air layer above the ground

    private static BuildingDef Def(string id) => TestContent.Db.Building(id);

    private static Simulation Flat(Action<ScenarioBuilder>? more = null)
    {
        var b = new ScenarioBuilder(32, 32, 32).Ground(G - 1);
        more?.Invoke(b);
        return b.Build();
    }

    private static PlacementResult Blueprint(Simulation sim, string def, Int3 origin, int rotation = 0) =>
        sim.Buildings.TryPlaceBlueprint(Def(def), origin, rotation, out _);

    [Fact]
    public void Overlap_Rejected()
    {
        var sim = Flat(b => b.Hub(new Int3(20, G, 20)));
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "warehouse", new Int3(10, G, 10)));

        // Overlapping another blueprint, fully or by one cell, is rejected; next to it is fine.
        Assert.Equal(PlacementResult.Overlaps, sim.Buildings.CanPlace(Def("warehouse"), new Int3(10, G, 10), 0));
        Assert.Equal(PlacementResult.Overlaps, sim.Buildings.CanPlace(Def("warehouse"), new Int3(11, G, 11), 0));
        Assert.Equal(PlacementResult.Overlaps, sim.Buildings.CanPlace(Def("levee"), new Int3(11, G + 1, 11), 0));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(Def("warehouse"), new Int3(12, G, 10), 0));

        // Overlapping a complete building (the hub), and covering another building's entrance, are rejected.
        Assert.Equal(PlacementResult.Overlaps, sim.Buildings.CanPlace(Def("warehouse"), new Int3(21, G, 21), 0));
        var hubEntrance = sim.Buildings.All.Single(x => x.Def.Id == "hub").EntranceCell;
        Assert.Equal(new Int3(21, G, 19), hubEntrance);
        Assert.Equal(PlacementResult.Overlaps, sim.Buildings.CanPlace(Def("levee"), hubEntrance, 0));

        // A rejected blueprint is not created.
        int before = sim.Buildings.All.Count();
        Assert.Equal(PlacementResult.Overlaps, Blueprint(sim, "warehouse", new Int3(11, G, 10)));
        Assert.Equal(before, sim.Buildings.All.Count());
        var bp = sim.Buildings.All.Single(x => x.Def.Id == "warehouse");
        Assert.Equal(BuildingState.Blueprint, bp.State);
        Assert.Equal(0, bp.Progress);
        Assert.Empty(bp.Delivered);
    }

    [Fact]
    public void Floating_Rejected()
    {
        // One missing ground cell under a 2x2 footprint is enough to reject it.
        var sim = Flat(b => b.FillBox(new Int3(11, G - 1, 11), new Int3(11, G - 1, 11), BlockId.Air));
        Assert.Equal(PlacementResult.NotOnGround, sim.Buildings.CanPlace(Def("warehouse"), new Int3(10, G, 10), 0));
        Assert.Equal(PlacementResult.NotOnGround, sim.Buildings.CanPlace(Def("warehouse"), new Int3(10, G + 3, 14), 0));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(Def("warehouse"), new Int3(14, G, 10), 0));

        // A complete warehouse roof is not ground (only natural blocks and levees are).
        sim.Buildings.PlacePrebuilt(Def("warehouse"), new Int3(20, G, 20), 0);
        Assert.Equal(PlacementResult.NotOnGround, sim.Buildings.CanPlace(Def("levee"), new Int3(20, G + 2, 20), 0));
    }

    [Fact]
    public void FootprintBlocked_BySolidOrPlant_WaterAllowed()
    {
        var sim = Flat(b => b
            .FillBox(new Int3(4, G, 4), new Int3(4, G, 4), BlockId.Dirt)
            .Layer(new Int3(10, G, 4), "T")
            .Water(new Int3(20, G, 20), WaterGrid.Full)
            .Water(new Int3(21, G, 21), WaterGrid.Full / 2));
        Assert.Equal(PlacementResult.FootprintBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(3, G, 3), 0));
        Assert.Equal(PlacementResult.FootprintBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(9, G, 3), 0));
        // A levee on the tree's base cell.
        Assert.Equal(PlacementResult.FootprintBlocked, sim.Buildings.CanPlace(Def("levee"), new Int3(10, G, 4), 0));
        // WAT-12: water in footprint cells is allowed at blueprint time.
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(Def("warehouse"), new Int3(20, G, 20), 0));
    }

    [Fact]
    public void EntranceBlocked_Rejected()
    {
        var sim = Flat(b => b
            .FillBox(new Int3(10, G, 9), new Int3(10, G, 9), BlockId.Stone)        // block on the entrance cell
            .FillBox(new Int3(14, G - 1, 9), new Int3(14, G - 1, 9), BlockId.Air)  // hole under the entrance
            .FillBox(new Int3(18, G + 1, 9), new Int3(18, G + 1, 9), BlockId.Stone)); // no headroom at the entrance
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(10, G, 10), 0));
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(14, G, 10), 0));
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(18, G, 10), 0));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(Def("warehouse"), new Int3(22, G, 10), 0));

        // The entrance may not lie in another building's footprint (a blueprint too).
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "levee", new Int3(22, G, 20)));
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(Def("warehouse"), new Int3(22, G, 21), 0));

        // The entrance must be inside the world.
        Assert.Equal(PlacementResult.OutOfBounds, sim.Buildings.CanPlace(Def("warehouse"), new Int3(5, G, 0), 0));
        Assert.Equal(PlacementResult.OutOfBounds, sim.Buildings.CanPlace(Def("warehouse"), new Int3(31, G, 5), 0));
    }

    /// <summary>A river trench along z = 14..16, two cells deep (y = 3..4) and full of water.</summary>
    private static Simulation Bank() => Flat(b =>
    {
        b.FillBox(new Int3(0, G - 2, 14), new Int3(31, G - 1, 16), BlockId.Air);
        for (int x = 0; x < 32; x++)
            for (int z = 14; z <= 16; z++)
                for (int y = G - 2; y <= G - 1; y++) b.Water(new Int3(x, y, z), WaterGrid.Full);
    });

    [Fact]
    public void PumpAwayFromWater_Rejected()
    {
        var sim = Bank();
        var pump = Def("pump");
        // Flat ground far from the trench: the cell in front of the intake has solid ground below it.
        Assert.Equal(PlacementResult.NeedsWaterEdge, sim.Buildings.CanPlace(pump, new Int3(10, G, 5), 0));
        // On the bank with the intake side (opposite the entrance) over the trench.
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(pump, new Int3(10, G, 13), 0));
        Assert.Equal(new Int3(10, G - 1, 14), BuildingShape.Intake(pump, new Int3(10, G, 13), 0));
        // Turned around on the same bank, the entrance would be over the water.
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(pump, new Int3(11, G, 13), 180));
        // Turned sideways (90) at the edge, the intake faces along the bank (solid ground).
        Assert.Equal(PlacementResult.NeedsWaterEdge, sim.Buildings.CanPlace(pump, new Int3(10, G, 12), 90));
        // Other bank, rotated 180: entrance north (z + 1), intake south over the trench.
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(pump, new Int3(11, G, 17), 180));
        Assert.Equal(new Int3(11, G - 1, 16), BuildingShape.Intake(pump, new Int3(11, G, 17), 180));
        // One cell back from the edge: the intake cell is solid ground.
        Assert.Equal(PlacementResult.NeedsWaterEdge, sim.Buildings.CanPlace(pump, new Int3(10, G, 12), 0));
    }

    [Fact]
    public void Rotation_RotatesFootprintAndEntrance()
    {
        var sim = Flat();
        var wh = Def("warehouse");
        var o = new Int3(15, G, 15);
        var expected = new (int Rot, int MinX, int MinZ, Int3 Entrance)[]
        {
            (0, 15, 15, new Int3(15, G, 14)),
            (90, 14, 15, new Int3(16, G, 15)),
            (180, 14, 14, new Int3(15, G, 16)),
            (270, 15, 14, new Int3(14, G, 15)),
        };
        foreach (var (rot, minX, minZ, entrance) in expected)
        {
            var cells = BuildingShape.Footprint(wh, o, rot).ToList();
            var want = new List<Int3>();
            for (int y = G; y < G + 2; y++)
                for (int z = minZ; z < minZ + 2; z++)
                    for (int x = minX; x < minX + 2; x++) want.Add(new Int3(x, y, z));
            Assert.Equal(want.OrderBy(c => (c.Y, c.Z, c.X)), cells.OrderBy(c => (c.Y, c.Z, c.X)));
            Assert.Contains(o, cells);   // rotation is about the origin cell
            Assert.Equal(entrance, BuildingShape.Entrance(wh, o, rot));
            Assert.DoesNotContain(entrance, cells);
            Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(wh, o, rot));

            // A placed building reports the same cells.
            var b = new Building { Def = wh, Origin = o, Rotation = rot };
            Assert.Equal(cells, b.FootprintCells().ToList());
            Assert.Equal(entrance, b.EntranceCell);
        }
        Assert.Equal(PlacementResult.BadRotation, sim.Buildings.CanPlace(wh, o, 45));
        Assert.Equal(PlacementResult.BadRotation, sim.Buildings.CanPlace(wh, o, 360));
        Assert.Equal(PlacementResult.BadRotation, sim.Buildings.CanPlace(wh, o, -90));
    }

    [Fact]
    public void Levee_StacksOnLevee()
    {
        var sim = Flat();
        var levee = Def("levee");
        // On a blueprinted levee (BLD-04). Its entrance is in the air, so the builder stands one level down.
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "levee", new Int3(10, G, 10)));
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "levee", new Int3(10, G + 1, 10)));
        // A third level has no standable cell in reach of the builder.
        Assert.Equal(PlacementResult.EntranceBlocked, sim.Buildings.CanPlace(levee, new Int3(10, G + 2, 10), 0));
        // Other buildings do not stand on a blueprinted levee.
        Blueprint(sim, "levee", new Int3(15, G, 10));
        Blueprint(sim, "levee", new Int3(16, G, 10));
        Blueprint(sim, "levee", new Int3(15, G, 11));
        Blueprint(sim, "levee", new Int3(16, G, 11));
        Assert.Equal(PlacementResult.NotOnGround, sim.Buildings.CanPlace(Def("warehouse"), new Int3(15, G + 1, 10), 0));

        // On a complete levee; a complete levee is also ground for other buildings.
        for (int z = 19; z <= 21; z++)
            for (int x = 20; x <= 21; x++) sim.Buildings.PlacePrebuilt(levee, new Int3(x, G, z), 0);
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(levee, new Int3(20, G + 1, 21), 0));
        Assert.Equal(PlacementResult.Ok, sim.Buildings.CanPlace(Def("warehouse"), new Int3(20, G + 1, 20), 0));
    }

    [Fact]
    public void Blueprints_SurviveSaveLoad_AndStillReserveTheirFootprint()
    {
        var sim = Flat();
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "warehouse", new Int3(10, G, 10), 90));
        Assert.Equal(PlacementResult.Ok, Blueprint(sim, "levee", new Int3(20, G, 20)));
        using var ms = new MemoryStream();
        Save.SaveGame.Save(sim, ms);
        ms.Position = 0;
        var loaded = Save.SaveGame.Load(ms, TestContent.Db);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        var bp = loaded.Buildings.All.Single(x => x.Def.Id == "warehouse");
        Assert.Equal((BuildingState.Blueprint, 90), (bp.State, bp.Rotation));
        Assert.Equal(PlacementResult.Overlaps, loaded.Buildings.CanPlace(Def("warehouse"), new Int3(10, G, 11), 0));
        Assert.Equal(PlacementResult.Ok, loaded.Buildings.CanPlace(Def("levee"), new Int3(20, G + 1, 20), 0));
    }

    [Fact]
    public void Hub_NotPlaceableByPlayer()
    {
        var sim = Flat();
        Assert.Equal(PlacementResult.PrebuiltOnly, sim.Buildings.CanPlace(Def("hub"), new Int3(10, G, 10), 0));
        Assert.Equal(PlacementResult.PrebuiltOnly, Blueprint(sim, "hub", new Int3(10, G, 10)));
        Assert.Empty(sim.Buildings.All);
    }
}
