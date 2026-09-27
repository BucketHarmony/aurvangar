using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.Water;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Scripts;
using Xunit;
using Xunit.Abstractions;

namespace Aurvangar.Sim.Tests;

/// <summary>M4-T10: binary save/load (SAV-01..06).</summary>
public class SaveLoadTests
{
    private readonly ITestOutputHelper _out;

    public SaveLoadTests(ITestOutputHelper output) => _out = output;

    private static byte[] SaveBytes(Simulation sim)
    {
        using var ms = new MemoryStream();
        SaveGame.Save(sim, ms);
        return ms.ToArray();
    }

    private static Simulation LoadBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return SaveGame.Load(ms, TestContent.Db);
    }

    private static Simulation RoundTrip(Simulation sim) => LoadBytes(SaveBytes(sim));

    /// <summary>Seed 1 with a two-layer dig box east of the hub entrance, so jobs, reservations, carried stacks,
    /// piles and hub stock all exist after a while.</summary>
    private static Simulation Seed1WithDig(out Int3 entrance)
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        entrance = sim.Buildings.All.First().EntranceCell;
        sim.Enqueue(new DesignateDig(entrance + new Int3(2, -2, -6), entrance + new Int3(12, -1, 6)));
        return sim;
    }

    [Fact]
    public void RoundTrip_HashEqual()
    {
        var sim = Seed1WithDig(out _);
        sim.RunTicks(1000);
        Assert.True(sim.Designations.Count > 0, "dig designation should still be active at tick 1000");
        Assert.Contains(sim.Jobs.All, j => j.IsClaimed);
        Assert.True(sim.Counters.JobsCompleted > 0);

        var bytes = SaveBytes(sim);
        var loaded = LoadBytes(bytes);

        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Seed, loaded.Seed);
        Assert.Equal(sim.Clock.Tick, loaded.Clock.Tick);
        Assert.Equal(sim.Agents.All.Select(a => a.Name), loaded.Agents.All.Select(a => a.Name));
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Equal(bytes, SaveBytes(loaded));   // saving the loaded game gives the same file
    }

    [Fact]
    public void RoundTrip_FutureEqual()
    {
        var sim = Seed1WithDig(out var entrance);
        sim.RunTicks(1000);
        var loaded = RoundTrip(sim);

        for (int t = 1; t <= 1000; t++)
        {
            if (t == 250)
            {
                // Same command on both sides after the load.
                var chop = new DesignateChop(entrance.X - 20, entrance.Z - 20, entrance.X + 20, entrance.Z + 20);
                sim.Enqueue(chop);
                loaded.Enqueue(chop);
            }
            sim.Tick();
            loaded.Tick();
            if (t % 100 == 0) Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        Assert.True(sim.Counters.JobsCompleted > 0);
    }

    [Fact]
    public void WrongVersion_FailsClearly()
    {
        var bytes = SaveBytes(new ScenarioBuilder().Ground(3).Agent(new Int3(5, 4, 5)).Build());
        var wrong = (byte[])bytes.Clone();
        BitConverter.GetBytes(SaveGame.FormatVersion + 1).CopyTo(wrong, 4);

        var ex = Assert.Throws<InvalidDataException>(() => LoadBytes(wrong));
        Assert.Contains($"version {SaveGame.FormatVersion + 1}", ex.Message);
        Assert.Contains($"{SaveGame.FormatVersion}", ex.Message);
    }

    [Fact]
    public void BadMagic_And_Truncated_FailClearly()
    {
        var bytes = SaveBytes(new ScenarioBuilder().Ground(3).Build());
        var magic = (byte[])bytes.Clone();
        magic[0] = (byte)'X';
        Assert.Contains("CSAV", Assert.Throws<InvalidDataException>(() => LoadBytes(magic)).Message);

        var cut = bytes[..(bytes.Length / 2)];
        Assert.Throws<InvalidDataException>(() => LoadBytes(cut));
    }

    /// <summary>SAV-06: dead agents are dropped on save; the id allocator is kept so ids are never reused.</summary>
    [Fact]
    public void DeadAgents_DroppedOnSave()
    {
        var sim = new ScenarioBuilder().Ground(3).Agent(new Int3(5, 4, 5)).Agent(new Int3(8, 4, 8)).Build();
        sim.RunTicks(3);
        sim.Agents.Kill(sim, sim.Agents.Get(new AgentId(1))!, DeathCause.Drowned);
        sim.RunTicks(2);

        var loaded = RoundTrip(sim);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(new[] { 2 }, loaded.Agents.All.Select(a => a.Id.Value));
        Assert.Equal(sim.Agents.Ids.Next, loaded.Agents.Ids.Next);
    }

    /// <summary>A save taken while an agent swims along its flee path resumes the same swim (WAT-14).</summary>
    [Fact]
    public void MidFlee_RoundTrip_ContinuesIdentically()
    {
        var b = new ScenarioBuilder().Ground(3);
        b.Layer(new Int3(4, 4, 4), "SSSSSSS", "S.....S", "S.....S", "S.....S", "S.....S", "S.....S", "SSSSSSS");
        var sim = b.Agent(new Int3(7, 4, 7)).Build();
        sim.Tick();
        for (int z = 5; z <= 9; z++)
            for (int x = 5; x <= 9; x++)
                sim.Water.SetLevel(new Int3(x, 4, z), WaterGrid.Full);
        var a = sim.Agents.All.Single();
        for (int i = 0; i < 20 && !(a.CurrentJob.IsValid && a.Move == MoveStatus.Moving); i++) sim.Tick();
        Assert.Equal(JobKind.Flee, sim.Jobs.Get(a.CurrentJob)!.Kind);
        Assert.Equal(MoveStatus.Moving, a.Move);

        var loaded = RoundTrip(sim);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        for (int t = 0; t < 60; t++)
        {
            sim.Tick();
            loaded.Tick();
            Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t + 1} ticks after load");
        }
        Assert.False(sim.Water.IsDeep(a.Cell));
    }

    /// <summary>Piles, storage stock, designations, the command log and still-queued commands survive a round trip.</summary>
    [Fact]
    public void PilesStorageAndCommands_RoundTrip()
    {
        var sim = new ScenarioBuilder().Ground(3)
            .Hub(new Int3(12, 4, 12)).Stock("log", 7)
            .Pile(new Int3(4, 4, 4), "stone", 13)
            .Agent(new Int3(6, 4, 6)).Agent(new Int3(7, 4, 6))
            .Build();
        sim.Enqueue(new DesignateDig(new Int3(2, 3, 20), new Int3(5, 3, 22)));
        sim.RunTicks(30);
        sim.Enqueue(new CancelDesignation(new Int3(2, 3, 20), new Int3(2, 3, 22)));   // queued, not yet applied

        var loaded = RoundTrip(sim);
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Piles.All, loaded.Piles.All);
        Assert.Equal(sim.Designations.All, loaded.Designations.All);
        Assert.Equal(sim.Buildings.All.Single().Stored, loaded.Buildings.All.Single().Stored);
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
        Assert.Equal(1, loaded.Commands.PendingCount);

        for (int t = 0; t < 300; t++) { sim.Tick(); loaded.Tick(); }
        Assert.Equal(sim.StateHash(), loaded.StateHash());
        Assert.Equal(sim.Commands.Log, loaded.Commands.Log);
    }

    /// <summary>Derived data the save does not store is rebuilt on load: reservation tables (JOB-04, BLD-10) and
    /// regions (PTH-13, read by job selection during the first tick after the load).</summary>
    [Fact]
    public void DerivedData_RebuiltOnLoad()
    {
        var sim = new ScenarioBuilder().Ground(3)
            .Hub(new Int3(12, 4, 12))
            .Pile(new Int3(4, 4, 4), "stone", 13)
            .Agent(new Int3(20, 4, 20))
            .Build();
        var a = sim.Agents.All.Single();
        for (int i = 0; i < 50 && !a.CurrentJob.IsValid; i++) sim.Tick();
        var job = sim.Jobs.Get(a.CurrentJob)!;
        Assert.Equal(JobKind.Haul, job.Kind);
        var hub = sim.Buildings.All.Single();
        var stone = TestContent.Db.Item("stone");
        Assert.True(sim.Jobs.ReservedFromPile(new Int3(4, 4, 4)) > 0);

        var loaded = RoundTrip(sim);
        Assert.Equal(sim.Jobs.ReservedFromPile(new Int3(4, 4, 4)), loaded.Jobs.ReservedFromPile(new Int3(4, 4, 4)));
        Assert.Equal(sim.Jobs.ReservedIn(hub.Id, stone), loaded.Jobs.ReservedIn(hub.Id, stone));
        Assert.Equal(sim.Jobs.ReservedInTotal(hub.Id), loaded.Jobs.ReservedInTotal(hub.Id));
        Assert.False(loaded.Regions.IsDirty);
        Assert.Equal(sim.Regions.Count, loaded.Regions.Count);
        Assert.NotEqual(Aurvangar.Sim.Paths.Regions.None, loaded.Regions.RegionOf(a.Cell));
        Assert.Equal(sim.Regions.RegionOf(a.Cell), loaded.Regions.RegionOf(a.Cell));
    }

    /// <summary>SAV-03 before the first tick: a fresh world has no regions built yet, and neither does its load, so
    /// both pick jobs the same way on tick 1 (ADR-032).</summary>
    [Fact]
    public void SaveBeforeFirstTick_ContinuesIdentically()
    {
        var sim = new ScenarioBuilder().Ground(3)
            .Hub(new Int3(12, 4, 12))
            .Agent(new Int3(6, 4, 6)).Agent(new Int3(7, 4, 6))
            .Build();
        sim.Enqueue(new DesignateDig(new Int3(2, 3, 20), new Int3(5, 3, 22)));
        Assert.True(sim.Regions.IsDirty);

        var loaded = RoundTrip(sim);
        Assert.True(loaded.Regions.IsDirty);
        for (int t = 1; t <= 200; t++)
        {
            sim.Tick();
            loaded.Tick();
            Assert.True(sim.StateHash() == loaded.StateHash(), $"hash differs {t} ticks after load");
        }
        Assert.True(sim.Counters.JobsCompleted > 0);
    }

    private sealed class UnknownCommand : ICommand
    {
        public string Tag => "TestOnlyUnknown";
        public void Apply(Simulation sim) { }
    }

    [Fact]
    public void UnknownCommandTag_SaveFailsClearly()
    {
        var sim = new ScenarioBuilder().Ground(3).Build();
        sim.Enqueue(new UnknownCommand());
        sim.Tick();
        var ex = Assert.Throws<NotSupportedException>(() => SaveBytes(sim));
        Assert.Contains("TestOnlyUnknown", ex.Message);
    }

    /// <summary>SAV-05: seed 1 with the full <see cref="SurvivalScript"/> at day 5 (tick 12,000) saves to at most
    /// 3 MB, and that save loads back to the same hash.</summary>
    [Fact]
    public void SaveSize_Day5_Under3MB()
    {
        var sim = WorldFactory.Create(SurvivalScript.Seed, TestContent.Db);
        SurvivalScript.Run(sim, 12_000);
        sim.Events.Drain();
        var bytes = SaveBytes(sim);
        _out.WriteLine($"SAV-05: day-5 save {bytes.Length:N0} bytes ({bytes.Length / 1048576.0:F2} MB)");
        Assert.True(bytes.Length <= 3 * 1024 * 1024, $"day-5 save is {bytes.Length:N0} bytes");
        Assert.Equal(sim.StateHash(), LoadBytes(bytes).StateHash());
    }
}
