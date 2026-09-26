using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Items;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Plants;

namespace Aurvangar.Sim.Save;

/// <summary>SAV-01 entity sections: plants, buildings, storage contents, item piles, designations, agents (alive
/// only, SAV-06), job board. Every section is written in ascending id / cell index order.</summary>
public static partial class SaveGame
{
    private static void WritePlants(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Plants);
        w.WriteCount(sim.Plants.Count);
        foreach (var p in sim.Plants.All)
        {
            w.Write(p.Id.Value); w.Write((byte)p.Kind); w.Write(p.Base); w.Write(p.Height);
            w.Write(p.MarkedForChop); w.Write(p.ChopUnreachable); w.Write(p.Berries); w.Write(p.RegrowTicks);
        }
    }

    private static void ReadPlants(BinaryReader r, Simulation sim)
    {
        r.ExpectSection(SaveSection.Plants);
        int n = r.ReadCount(sim.World.CellCount, "plant");
        for (int k = 0; k < n; k++)
        {
            var p = new Plant
            {
                Id = new PlantId(r.ReadInt32()), Kind = r.ReadEnum<PlantKind>("plant kind"), Base = r.ReadInt3(),
                Height = r.ReadInt32(), MarkedForChop = r.ReadBoolean(), ChopUnreachable = r.ReadBoolean(),
                Berries = r.ReadInt32(), RegrowTicks = r.ReadInt32(),
            };
            sim.Plants.Restore(p);
        }
    }

    private static void WriteBuildings(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Buildings);
        var all = sim.Buildings.All.ToList();
        w.WriteCount(all.Count);
        foreach (var b in all)
        {
            w.Write(b.Id.Value); w.Write(b.Def.Id); w.Write(b.Origin); w.Write(b.Rotation);
            w.Write((byte)b.State); w.Write(b.Progress); w.Write(b.NoWater);
            WriteItemCounts(w, b.Delivered);
        }

        w.Section(SaveSection.Storage);
        foreach (var b in all) WriteItemCounts(w, b.Stored);
    }

    private static void ReadBuildings(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.Buildings);
        int n = r.ReadCount(sim.World.CellCount, "building");
        var all = new List<Building>(n);
        for (int k = 0; k < n; k++)
        {
            var b = new Building
            {
                Id = new BuildingId(r.ReadInt32()), Def = content.Building(r.ReadString()), Origin = r.ReadInt3(),
                Rotation = r.ReadInt32(), State = r.ReadEnum<BuildingState>("building state"), Progress = r.ReadInt32(),
                NoWater = r.ReadBoolean(),
            };
            ReadItemCounts(r, b.Delivered, content, "delivered item");
            sim.Buildings.Restore(b);
            all.Add(b);
        }

        r.ExpectSection(SaveSection.Storage);
        foreach (var b in all) ReadItemCounts(r, b.Stored, content, "stored item");
    }

    private static void WriteItemCounts(BinaryWriter w, SortedDictionary<int, int> counts)
    {
        w.WriteCount(counts.Count);
        foreach (var (item, n) in counts) { w.Write(item); w.Write(n); }
    }

    private static void ReadItemCounts(BinaryReader r, SortedDictionary<int, int> into, ContentDb content, string what)
    {
        int n = r.ReadCount(content.Items.Count, what);
        for (int k = 0; k < n; k++) into[ReadItem(r, content, what)] = r.ReadInt32();
    }

    /// <summary>A real item id (1..Items.Count-1; 0 is "none").</summary>
    private static int ReadItem(BinaryReader r, ContentDb content, string what)
    {
        int id = r.ReadIndex(content.Items.Count, what);
        if (id == 0) throw new InvalidDataException($"Save file is corrupt: {what} has no item.");
        return id;
    }

    private static void WritePiles(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Piles);
        w.WriteCount(sim.Piles.Count);
        foreach (var (cell, s) in sim.Piles.All) { w.Write(sim.World.Index(cell)); w.Write(s.Item.Value); w.Write(s.Count); }
    }

    private static void ReadPiles(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.Piles);
        int cells = sim.World.CellCount;
        int n = r.ReadCount(cells, "item pile");
        for (int k = 0; k < n; k++)
        {
            int i = r.ReadIndex(cells, "item pile cell");
            var s = new ItemStack(new ItemId(ReadItem(r, content, "pile item")), r.ReadInt32());
            sim.Piles.Restore(i, s);
        }
    }

    private static void WriteDesignations(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Designations);
        w.WriteCount(sim.Designations.Count);
        foreach (var (cell, mark) in sim.Designations.All) { w.Write(sim.World.Index(cell)); w.Write((byte)mark); }
    }

    private static void ReadDesignations(BinaryReader r, Simulation sim)
    {
        r.ExpectSection(SaveSection.Designations);
        int cells = sim.World.CellCount;
        int n = r.ReadCount(cells, "designation");
        for (int k = 0; k < n; k++)
        {
            var cell = sim.World.CellOf(r.ReadIndex(cells, "designation cell"));
            sim.Designations.Set(cell, r.ReadEnum<DesignationMark>("designation mark"));
        }
    }

    private static void WriteAgents(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Agents);
        var alive = sim.Agents.All.Where(a => a.IsAlive).ToList();   // SAV-06
        w.WriteCount(alive.Count);
        foreach (var a in alive)
        {
            w.Write(a.Id.Value); w.Write(a.Name);
            w.Write(a.Cell); w.Write(a.NextCell); w.Write(a.MoveProgress); w.Write(a.MoveTotal);
            w.Write(a.Hunger); w.Write(a.Thirst); w.Write(a.Health);
            w.Write(a.Carried.Item.Value); w.Write(a.Carried.Count);
            w.Write((byte)a.State); w.Write((byte)a.Death);
            w.Write(a.CurrentJob.Value); w.Write(a.StepIndex); w.Write(a.StepProgress);
            w.WriteCount(a.Path.Length); foreach (var c in a.Path) w.Write(c);
            w.Write(a.PathPos); w.Write((byte)a.Move); w.Write(a.Repathed);
            w.Write(a.NextJobSearchTick);
        }
    }

    private static void ReadAgents(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.Agents);
        int n = r.ReadCount(sim.World.CellCount, "agent");
        for (int k = 0; k < n; k++)
        {
            var a = new Agent { Id = new AgentId(r.ReadInt32()), Name = r.ReadString() };
            a.Cell = r.ReadInt3(); a.NextCell = r.ReadInt3(); a.MoveProgress = r.ReadInt32(); a.MoveTotal = r.ReadInt32();
            a.Hunger = r.ReadInt32(); a.Thirst = r.ReadInt32(); a.Health = r.ReadInt32();
            int item = r.ReadIndex(content.Items.Count, "carried item");
            a.Carried = new ItemStack(new ItemId(item), r.ReadInt32());
            a.State = r.ReadEnum<AgentState>("agent state"); a.Death = r.ReadEnum<DeathCause>("death cause");
            a.CurrentJob = new JobId(r.ReadInt32()); a.StepIndex = r.ReadInt32(); a.StepProgress = r.ReadInt32();
            var path = new Int3[r.ReadCount(sim.World.CellCount, "agent path")];
            for (int i = 0; i < path.Length; i++) path[i] = r.ReadInt3();
            a.Path = path;
            a.PathPos = r.ReadInt32(); a.Move = r.ReadEnum<MoveStatus>("move status"); a.Repathed = r.ReadBoolean();
            a.NextJobSearchTick = r.ReadInt64();
            sim.Agents.Restore(a);
        }
    }

    private static void WriteJobs(BinaryWriter w, Simulation sim)
    {
        w.Section(SaveSection.Jobs);
        w.WriteCount(sim.Jobs.Count);
        foreach (var j in sim.Jobs.All)
        {
            w.Write(j.Id.Value); w.Write((byte)j.Kind); w.Write(j.Priority); w.Write(j.Target);
            w.WriteCount(j.Steps.Count);
            foreach (var s in j.Steps)
            {
                w.Write((byte)s.Kind); w.Write(s.Cell); w.Write(s.Target); w.Write(s.Item.Value); w.Write(s.Count);
                w.Write(s.Ticks); w.Write((byte)s.Goal);
            }
            w.WriteCount(j.Reservations.Count);
            foreach (var res in j.Reservations)
            {
                w.Write((byte)res.Kind); w.Write(res.Cell); w.Write(res.Building.Value); w.Write(res.Item.Value); w.Write(res.Count);
            }
            w.Write(j.ClaimedBy.Value); w.Write(j.Failures); w.Write(j.RetryAfterTick);
        }
    }

    private static void ReadJobs(BinaryReader r, Simulation sim, ContentDb content)
    {
        r.ExpectSection(SaveSection.Jobs);
        int items = content.Items.Count;
        int n = r.ReadCount(int.MaxValue, "job");
        for (int k = 0; k < n; k++)
        {
            var job = new Job
            {
                Id = new JobId(r.ReadInt32()), Kind = r.ReadEnum<JobKind>("job kind"), Priority = r.ReadInt32(),
                Target = r.ReadInt3(),
            };
            int steps = r.ReadCount(int.MaxValue, "job step");
            for (int i = 0; i < steps; i++)
                job.Steps.Add(new JobStep(r.ReadEnum<StepKind>("step kind"), r.ReadInt3(), r.ReadInt32(),
                    new ItemId(r.ReadIndex(items, "step item")), r.ReadInt32(), r.ReadInt32(), r.ReadEnum<GoalMode>("goal mode")));
            int reservations = r.ReadCount(int.MaxValue, "reservation");
            for (int i = 0; i < reservations; i++)
                job.Reservations.Add(new Reservation(r.ReadEnum<ReservationKind>("reservation kind"), r.ReadInt3(),
                    new BuildingId(r.ReadInt32()), new ItemId(r.ReadIndex(items, "reservation item")), r.ReadInt32()));
            job.ClaimedBy = new AgentId(r.ReadInt32()); job.Failures = r.ReadInt32(); job.RetryAfterTick = r.ReadInt64();
            sim.Jobs.Restore(job);
        }
    }
}
