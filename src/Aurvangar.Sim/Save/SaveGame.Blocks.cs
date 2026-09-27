using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Save;

public static partial class SaveGame
{
    /// <summary>CON-19 (M11-T10, save v7): the cells that are not Full, ascending cell index: index, packed form.
    /// Written right after the blocks.</summary>
    private static void WriteBlockForms(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.BlockForms);
        w.WriteCount(sim.World.FormCount);
        foreach (var (i, f) in sim.World.Forms) { w.Write(i); w.Write(f.Packed); }
    }

    private static void ReadBlockForms(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.BlockForms);
        int n = r.ReadCount(sim.World.CellCount, "block form");
        int last = -1;
        for (int k = 0; k < n; k++)
        {
            int index = r.ReadIndex(sim.World.CellCount, "block form cell");
            if (index <= last) throw new InvalidDataException($"Save file is corrupt: block form cell {index} is out of order.");
            last = index;
            var form = ReadForm(r, content, "block form");
            if (form.IsFull) throw new InvalidDataException($"Save file is corrupt: block form cell {index} is Full.");
            if (!content.IsConstruction((BlockId)sim.World.Blocks[index]))
                throw new InvalidDataException($"Save file is corrupt: block form cell {index} is not a built block.");
            sim.World.RestoreForm(index, form);
        }
    }

    private static BlockForm ReadForm(BinaryReader r, ContentDb content, string what)
    {
        var form = BlockForm.FromPacked(r.ReadByte());
        if (!content.IsValidForm(form)) throw new InvalidDataException($"Save file is corrupt: {what} shape {form} is not defined.");
        return form;
    }

    /// <summary>CON-04 (M8-T2): plan entries in ascending cell index: index, block, state, packed form (CON-19).</summary>
    private static void WriteBlockPlans(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.BlockPlans);
        w.WriteCount(sim.Plans.Count);
        foreach (var (c, e) in sim.Plans.All)
        {
            w.Write(sim.World.Index(c)); w.Write((byte)e.Block); w.Write((byte)e.State); w.Write(e.Form.Packed);
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
            var form = ReadForm(r, content, "plan entry");
            sim.Plans.Restore(index, new PlanEntry(block, state, form));
        }
    }
}
