using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Save;

public static partial class SaveGame
{
    /// <summary>CON-04 (M8-T2): plan entries in ascending cell index: index, block, state.</summary>
    private static void WriteBlockPlans(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.BlockPlans);
        w.WriteCount(sim.Plans.Count);
        foreach (var (c, e) in sim.Plans.All)
        {
            w.Write(sim.World.Index(c)); w.Write((byte)e.Block); w.Write((byte)e.State);
        }
    }

    private static void ReadBlockPlans(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.BlockPlans);
        int n = r.ReadCount(sim.World.CellCount, "plan entry");
        int last = -1;
        for (int k = 0; k < n; k++)
        {
            int index = r.ReadIndex(sim.World.CellCount, "plan entry cell");
            if (index <= last) throw new InvalidDataException($"Save file is corrupt: plan entry cell {index} is out of order.");
            last = index;
            var block = (BlockId)r.ReadByte();
            if (!content.IsConstruction(block)) throw new InvalidDataException($"Save file is corrupt: plan entry block {(byte)block} is not a construction block.");
            var state = r.ReadEnum<PlanState>("plan entry state");
            sim.Plans.Restore(index, new PlanEntry(block, state));
        }
    }
}
