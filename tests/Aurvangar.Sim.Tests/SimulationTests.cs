using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class SimulationTests
{
    private sealed class SetBlockCommand : ICommand
    {
        public required Int3 Cell { get; init; }
        public required BlockId Block { get; init; }
        public string Tag => "test.setBlock";
        public void Apply(Simulation sim) => sim.World.SetBlock(Cell, Block);
    }

    [Fact]
    public void Tick_AdvancesClock()
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.RunTicks(10);
        Assert.Equal(10, sim.Clock.Tick);
    }

    [Fact]
    public void Commands_AppliedInOrderAndLogged() // ARCH-02
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        var c = new Int3(3, 5, 3);
        sim.Enqueue(new SetBlockCommand { Cell = c, Block = BlockId.Stone });
        sim.Enqueue(new SetBlockCommand { Cell = c, Block = BlockId.Dirt });
        sim.Tick();
        Assert.Equal(BlockId.Dirt, sim.World.GetBlock(c));
        Assert.Equal(2, sim.Commands.Log.Count);
        Assert.Equal(0, sim.Commands.Log[0].Tick);
    }

    [Fact]
    public void Tick_EmitsChunkDirtyForChangedChunks() // ARCH-03
    {
        var sim = new ScenarioBuilder().Ground(4).Build();
        sim.Enqueue(new SetBlockCommand { Cell = new Int3(3, 5, 3), Block = BlockId.Stone });
        sim.Tick();
        var events = sim.Events.Drain();
        Assert.Contains(events, e => e is ChunkDirty);
        Assert.Empty(sim.World.ChangedCells);
    }

    [Fact]
    public void StateHash_EqualForIdenticalSims_DiffersAfterChange() // ARCH-06
    {
        var a = new ScenarioBuilder().Ground(4).Build();
        var b = new ScenarioBuilder().Ground(4).Build();
        a.RunTicks(5); b.RunTicks(5);
        Assert.Equal(a.StateHash(), b.StateHash());
        b.World.SetBlock(new Int3(1, 5, 1), BlockId.Stone);
        Assert.NotEqual(a.StateHash(), b.StateHash());
    }

    public static IEnumerable<object[]> StateMutations() => new[]
    {
        new object[] { "water level", (Action<Simulation>)(s => s.Water.SetLevel(new Int3(2, 5, 2), 100)) },
        new object[] { "water source", (Action<Simulation>)(s => s.Water.AddSource(new Int3(2, 5, 2))) },
        new object[] { "water drain", (Action<Simulation>)(s => s.Water.AddDrain(new Int3(2, 5, 2))) },
        new object[] { "water stats", (Action<Simulation>)(s => s.Water.Stats.Evaporated += 1) },
        new object[] { "source strength", (Action<Simulation>)(s => s.Water.SourceStrength = 50) },
        new object[] { "plant", (Action<Simulation>)(s => s.Plants.AddBush(new Int3(2, 5, 2))) },
        new object[] { "plant id allocator", (Action<Simulation>)(s => s.Plants.Ids.Allocate()) },
        new object[] { "building id allocator", (Action<Simulation>)(s => s.Buildings.Ids.Allocate()) },
        new object[] { "agent", (Action<Simulation>)(s => s.Agents.Spawn(new Int3(2, 5, 2), "Urist")) },
        new object[] { "agent id allocator", (Action<Simulation>)(s => s.Agents.Ids.Allocate()) },
        new object[] { "rng", (Action<Simulation>)(s => s.Rng.NextU64()) },
    };

    [Theory]
    [MemberData(nameof(StateMutations))]
    public void StateHash_CoversAllExistingState(string what, Action<Simulation> mutate) // ARCH-06
    {
        var a = new ScenarioBuilder().Ground(4).Build();
        var b = new ScenarioBuilder().Ground(4).Build();
        mutate(b);
        Assert.True(a.StateHash() != b.StateHash(), $"StateHash does not cover: {what}");
    }

    [Fact]
    public void StateHash_CoversAgentPathCells() // ARCH-06
    {
        var a = new ScenarioBuilder().Ground(4).Build();
        var b = new ScenarioBuilder().Ground(4).Build();
        a.Agents.Spawn(new Int3(2, 5, 2), "Urist").Path = new[] { new Int3(3, 5, 2) };
        b.Agents.Spawn(new Int3(2, 5, 2), "Urist").Path = new[] { new Int3(2, 5, 3) };
        Assert.NotEqual(a.StateHash(), b.StateHash());
    }

    [Fact]
    public void ScenarioBuilder_RejectsUnknownChar()
    {
        Assert.Throws<ArgumentException>(() => new ScenarioBuilder().Layer(5, "S?S"));
    }
}
