using Aurvangar.Sim.Content;

namespace Aurvangar.Sim.World;

/// <summary>Builds the standard game world: 128x64x128, terrain, hub, plants, colonists, pre-settled river.</summary>
public static class WorldFactory
{
    public const int SizeX = 128, SizeY = 64, SizeZ = 128;

    public static Simulation Create(ulong seed, ContentDb content)
    {
        var sim = new Simulation(content, SizeX, SizeY, SizeZ, seed);
        var terrain = TerrainGenerator.Generate(sim.World, seed);   // M1-T3
        var hub = content.Building("hub");
        sim.Buildings.PlacePrebuilt(hub, terrain.HubOrigin(hub.Footprint), 0);
        foreach (var t in terrain.TreeBases) sim.Plants.AddTree(t);
        foreach (var b in terrain.BushBases) sim.Plants.AddBush(b);
        // M2-T5: register sources/drains, fill InitialWater, run 600 water ticks, reset the clock to 0.
        // M4-T4: spawn 5 colonists near the hub entrance. M5-T5: starting stock (40 berries, 30 water, 30 logs).
        sim.World.ClearChangeLog();
        sim.World.MarkAllDirty();
        return sim;
    }
}
