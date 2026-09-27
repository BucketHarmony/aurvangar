using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;
using static Aurvangar.Sim.Tests.Scenarios.ConstructionScenarioTests;

namespace Aurvangar.Sim.Tests.Scenarios;

/// <summary>M7-T2 (G3 answer 3b, ADR-055): on seed 1 the river banks step up one level per cell, so a pump whose
/// intake is over the water has its entrance inside the next bank step. The pump may then use the stand cell one
/// level up (on top of that step), so wet pump sites exist on the generated map without a hand-dug notch, and the
/// colonists build and work such a pump from the raised cell.</summary>
[Trait("Category", "Scenario")]
public class PumpSiteScenarioTests
{
    private const ulong Seed = 1;

    private readonly record struct Site(Int3 Origin, int Rotation, Int3 Stand);

    /// <summary>Every valid pump site (any column, any air cell on solid ground, four rotations) whose intake holds at
    /// least <c>minIntakeLevel</c> and whose stand cell is in the Great Hall's region. No blocks are changed.</summary>
    private static List<Site> WetReachableSites(Simulation sim)
    {
        var pump = sim.Content.Building("pump");
        int min = pump.Producer!.MinIntakeLevel;
        sim.Regions.RebuildIfDirty();
        int hall = sim.Regions.RegionOf(Hub(sim).EntranceCell);
        Assert.NotEqual(Paths.Regions.None, hall);
        var world = sim.World;
        var sites = new List<Site>();
        for (int z = 0; z < world.SizeZ; z++)
            for (int x = 0; x < world.SizeX; x++)
                for (int y = 1; y < world.SizeY; y++)
                {
                    var o = new Int3(x, y, z);
                    if (world.IsSolid(o) || !world.IsSolid(o + Int3.Down)) continue;
                    for (int rot = 0; rot < 360; rot += 90)
                    {
                        if (sim.Buildings.CanPlace(pump, o, rot) != PlacementResult.Ok) continue;
                        if (sim.Water.GetLevel(BuildingShape.Intake(pump, o, rot)) < min) continue;
                        var stand = sim.Buildings.PlannedStandCell(pump, o, rot);
                        if (sim.Regions.RegionOf(stand) != hall) continue;
                        sites.Add(new Site(o, rot, stand));
                    }
                }
        return sites;
    }

    [Fact]
    public void Seed1_WetPumpSitesExist_WithoutANotch()
    {
        var sim = WorldFactory.Create(Seed, TestContent.Db);
        var sites = WetReachableSites(sim);
        Assert.True(sites.Count > 0, "no wet, reachable pump site on seed 1 without digging");
        // They are the bank sites the raised stand cell opens up: the entrance is inside the next bank step.
        Assert.Contains(sites, s => s.Stand == BuildingShape.Entrance(sim.Content.Building("pump"), s.Origin, s.Rotation) + Int3.Up
            && sim.World.IsSolid(s.Stand + Int3.Down));
    }

    /// <summary>The colonists build a pump at the wet site nearest the hall, with its entrance cell solid, and work it
    /// from the stand cell one level up: water is pumped out of the river and hauled to storage.</summary>
    [Fact]
    public void Seed1_RaisedPump_IsBuiltAndWorkedFromTheCellAbove()
    {
        var sim = WorldFactory.Create(Seed, TestContent.Db);
        var hubEntrance = Hub(sim).EntranceCell;
        var pumpDef = sim.Content.Building("pump");
        var site = WetReachableSites(sim)
            .Where(s => s.Stand == BuildingShape.Entrance(pumpDef, s.Origin, s.Rotation) + Int3.Up)
            .OrderBy(s => Manhattan(s.Stand, hubEntrance)).ThenBy(s => sim.World.Index(s.Origin)).ThenBy(s => s.Rotation)
            .First();
        var entrance = BuildingShape.Entrance(pumpDef, site.Origin, site.Rotation);
        Assert.True(sim.World.IsSolid(entrance), "the entrance cell should be the next bank step");

        sim.Enqueue(new PlaceBuilding("pump", site.Origin, site.Rotation));
        sim.Tick();
        var pump = Site(sim, "pump");
        RunUntil(sim, () => pump.State == BuildingState.Complete, 4000);
        Assert.Equal(site.Stand, Construction.StandCell(sim, pump));

        bool workedFromStand = false, hauled = false;
        RunUntil(sim, () => sim.Water.Stats.Pumped >= 5 * 64 && hauled, 4000, () =>
        {
            foreach (var j in sim.Jobs.All)
            {
                if (j.Kind == JobKind.OperatePump && j.IsClaimed && sim.Agents.Get(j.ClaimedBy) is { } a
                    && a.CurrentJob == j.Id && a.StepIndex == 1 && a.Cell == site.Stand)
                    workedFromStand = true;
                hauled |= j.Kind == JobKind.Haul && j.IsClaimed
                    && j.Steps.Any(s => s.Kind == StepKind.PickUpFromStorage && s.Target == pump.Id.Value);
            }
        });
        Assert.True(workedFromStand, "no dwarf worked the pump from the raised stand cell");
        Assert.False(pump.NoWater);
        Assert.Equal(0, sim.Counters.JobsFailed);
    }

    private static int Manhattan(Int3 a, Int3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
}
