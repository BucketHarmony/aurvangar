using Colony.Sim.Commands;
using Colony.Sim.Core;
using Colony.Sim.Events;
using Colony.Sim.Tests.Support;
using Colony.Sim.World;
using Xunit;

namespace Colony.Sim.Tests;

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

    [Fact]
    public void ScenarioBuilder_RejectsUnknownChar()
    {
        Assert.Throws<ArgumentException>(() => new ScenarioBuilder().Layer(5, "S?S"));
    }
}
