using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Designations;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Save;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Hud;
using Aurvangar.ViewCore.Persistence;
using Aurvangar.ViewCore.Picking;
using Aurvangar.ViewCore.Tools;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M4-T11: dig/chop/cancel tools (VIEW-12, VIEW-13).</summary>
[Trait("Category", "Unit")]
public class ToolControllerTests
{
    private static PickHit Top(int x, int y, int z) => new(new Int3(x, y, z), Int3.Up);

    [Theory]
    [InlineData('G', ToolKind.Dig)]
    [InlineData('C', ToolKind.Chop)]
    [InlineData('Z', ToolKind.Cancel)]
    public void Hotkeys_SelectTools(char key, ToolKind tool) => Assert.Equal(tool, ToolController.ForHotkey(key));

    [Fact]
    public void UnknownHotkey_IsNull() => Assert.Null(ToolController.ForHotkey('Q'));

    [Fact]
    public void SelectTool_NeverDrags()
    {
        var t = new ToolController();
        Assert.Equal(ToolKind.Select, t.Tool);
        t.Press(Top(1, 4, 1));
        Assert.False(t.Dragging);
        Assert.Null(t.Release(Top(3, 4, 3), 63));
    }

    [Fact]
    public void DigDrag_BoxFromFirstCellToSecond_OnTheSecondsLayer()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Dig);
        t.Press(Top(5, 20, 5));
        Assert.True(t.Dragging);
        t.Move(null);                 // mouse over nothing: keeps the last end
        t.Move(Top(9, 18, 7));
        Assert.Equal((new Int3(5, 18, 5), new Int3(9, 20, 7)), t.PreviewBox(63));
        var cmd = Assert.IsType<DesignateDig>(t.Release(null, 63));
        Assert.Equal(new DesignateDig(new Int3(5, 20, 5), new Int3(9, 18, 7)), cmd);
        Assert.False(t.Dragging);
        Assert.Equal(ToolKind.Dig, t.Tool);   // the tool stays active
    }

    [Fact]
    public void DigClick_OneCell_AndSecondPickClampedToSlice()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Dig);
        t.Press(Top(5, 20, 5));
        Assert.Equal(new DesignateDig(new Int3(5, 20, 5), new Int3(5, 20, 5)), t.Release(Top(5, 20, 5), 63));
        Assert.Equal(new DesignateDig(new Int3(5, 20, 5), new Int3(6, 15, 6)),
            ToolController.CommandFor(ToolKind.Dig, Top(5, 20, 5), Top(6, 17, 6), sliceY: 15));
    }

    [Fact]
    public void ChopDrag_IsAnXZRectangle()
    {
        Assert.Equal(new DesignateChop(3, 4, 10, 12),
            ToolController.CommandFor(ToolKind.Chop, Top(3, 20, 4), Top(10, 22, 12), 63));
    }

    [Fact]
    public void CancelDrag_CoversMarkedBlocksAndTreeBasesOnThem()
    {
        var cmd = ToolController.CommandFor(ToolKind.Cancel, Top(3, 20, 4), Top(6, 19, 2), 63);
        Assert.Equal(new CancelDesignation(new Int3(3, 19, 2), new Int3(6, 21, 4)), cmd);

        // End to end: a chop mark on a tree standing on the dragged ground is cleared.
        var sim = new ScenarioBuilder().Ground(4).Layer(5, "..........", "..T.......").Build();
        sim.Enqueue(new DesignateChop(0, 0, 9, 9));
        sim.Enqueue(new DesignateDig(new Int3(5, 4, 5), new Int3(5, 4, 5)));
        sim.Tick();
        sim.Enqueue(ToolController.CommandFor(ToolKind.Cancel, Top(0, 4, 0), Top(9, 4, 9), 15)!);
        sim.Tick();
        Assert.False(sim.Plants.All.First().MarkedForChop);
        Assert.Equal(DesignationMark.None, sim.Designations.Get(new Int3(5, 4, 5)));
    }

    [Fact]
    public void Escape_AbortsDrag_AndReturnsToSelect()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Chop);
        t.Press(Top(1, 4, 1));
        t.SetTool(ToolKind.Select);
        Assert.False(t.Dragging);
        Assert.Null(t.PreviewBox(63));
        Assert.Null(t.Release(Top(2, 4, 2), 63));
    }

    [Fact]
    public void PressOnNothing_DoesNotStartDrag()
    {
        var t = new ToolController();
        t.SetTool(ToolKind.Dig);
        t.Press(null);
        Assert.False(t.Dragging);
    }
}

/// <summary>M4-T11: colonist panel rows (VIEW-16).</summary>
[Trait("Category", "Unit")]
public class ColonistPanelTests
{
    [Fact]
    public void Rows_NameBarsAndActivity_InIdOrder()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Agent(new Int3(4, 5, 2)).Build();
        var a = sim.Agents.All.ToArray();
        a[0].Hunger = Agent.NeedMax / 2;
        a[0].Thirst = Agent.NeedMax / 4;
        a[0].Health = Agent.HealthMax;
        var rows = ColonistPanelModel.Build(sim);
        Assert.Equal(new[] { "Agent1", "Agent2" }, rows.Select(r => r.Name));
        Assert.Equal(0.5f, rows[0].Hunger, 3);
        Assert.Equal(0.25f, rows[0].Thirst, 3);
        Assert.Equal(1f, rows[0].Health, 3);
        Assert.Equal("Idle", rows[0].Activity);
        Assert.Equal(new Int3(2, 5, 2), rows[0].Cell);
        Assert.True(rows[0].Alive);
    }

    [Fact]
    public void ClickCentersCamera_KeepsYawPitchZoom()
    {
        var rig = new Aurvangar.ViewCore.Camera.OrbitRig(128, 128, new System.Numerics.Vector3(10, 20, 10), 20);
        rig.Zoom(3);
        float yaw = rig.Yaw, pitch = rig.Pitch, dist = rig.Distance;
        rig.CenterOn(new System.Numerics.Vector3(40.5f, 18f, 200f));
        Assert.Equal(40.5f, rig.Focus.X);
        Assert.Equal(128f, rig.Focus.Z);   // clamped to the world
        Assert.Equal(18f, rig.BaseFocusY);
        Assert.Equal((yaw, pitch, dist), (rig.Yaw, rig.Pitch, rig.Distance));
        rig.Update(10f, 63);
        Assert.Equal(18f, rig.Focus.Y, 3);
    }

    [Fact]
    public void Activity_NamesTheJob()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Build();
        var a = sim.Agents.All.First();
        var stone = TestContent.Db.Item("stone");
        (JobKind Kind, JobStep[] Steps, string Label)[] cases =
        {
            (JobKind.Dig, new[] { JobStep.GoTo(new Int3(5, 4, 5)) }, "Digging"),
            (JobKind.Chop, new[] { JobStep.GoTo(new Int3(5, 4, 5)) }, "Felling a tree"),
            (JobKind.Haul, new[] { JobStep.GoTo(new Int3(5, 5, 5)), JobStep.PickUp(new Int3(5, 5, 5), stone, 3) }, "Hauling stone"),
            (JobKind.Flee, new[] { JobStep.GoTo(new Int3(5, 5, 5)) }, "Fleeing the water"),
        };
        foreach (var (kind, steps, label) in cases)
        {
            var job = sim.Jobs.Post(kind, new Int3(5, 4, 5), steps);
            a.CurrentJob = job.Id;
            a.State = AgentState.Working;
            Assert.Equal(label, ColonistPanelModel.Activity(sim, a));
        }
    }

    [Fact]
    public void Activity_TrappedAndDead()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Build();
        var a = sim.Agents.All.First();
        a.State = AgentState.Trapped;
        Assert.Equal("Trapped in deep water!", ColonistPanelModel.Activity(sim, a));
        sim.Agents.Kill(sim, a, DeathCause.Drowned);
        Assert.Equal("Drowned", ColonistPanelModel.Activity(sim, a));
        var row = ColonistPanelModel.Build(sim)[0];
        Assert.False(row.Alive);
        Assert.Equal(0f, row.Health);
    }
}

/// <summary>M4-T11: F5/F9 quick save slot (VIEW-19).</summary>
[Trait("Category", "Unit")]
public class QuickSaveTests
{
    [Fact]
    public void SaveThenLoad_ReproducesHash_AndFuture()
    {
        var sim = new ScenarioBuilder().Ground(4).Agent(new Int3(2, 5, 2)).Build();
        sim.Enqueue(new DesignateDig(new Int3(4, 4, 4), new Int3(6, 4, 6)));
        sim.RunTicks(50);
        string path = Path.Combine(Path.GetTempPath(), $"aurvangar-quick-{Guid.NewGuid():N}.save");
        try
        {
            Assert.Null(QuickSave.Save(sim, path));
            var (loaded, error) = QuickSave.Load(path, TestContent.Db);
            Assert.Null(error);
            Assert.NotNull(loaded);
            Assert.Equal(sim.StateHash(), loaded!.StateHash());
            sim.RunTicks(100);
            loaded.RunTicks(100);
            Assert.Equal(sim.StateHash(), loaded.StateHash());
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp*"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_MissingFile_ReportsNoSave()
    {
        var (sim, error) = QuickSave.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.save"), TestContent.Db);
        Assert.Null(sim);
        Assert.Equal("No quick save yet.", error);
    }

    [Fact]
    public void Load_CorruptFile_ReportsError_AndSaveOverwritesIt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aurvangar-bad-{Guid.NewGuid():N}.save");
        try
        {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            var (sim, error) = QuickSave.Load(path, TestContent.Db);
            Assert.Null(sim);
            Assert.StartsWith("Load failed: ", error);

            var good = new ScenarioBuilder().Ground(4).Build();
            Assert.Null(QuickSave.Save(good, path));
            Assert.Null(QuickSave.Load(path, TestContent.Db).Error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_UnsavableCommand_ReportsError_AndKeepsOldFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aurvangar-keep-{Guid.NewGuid():N}.save");
        try
        {
            var good = new ScenarioBuilder().Ground(4).Build();
            Assert.Null(QuickSave.Save(good, path));
            var bytes = File.ReadAllBytes(path);
            good.Enqueue(new UnsavableCommand());
            string? error = QuickSave.Save(good, path);
            Assert.StartsWith("Save failed: ", error);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class UnsavableCommand : ICommand
    {
        public string Tag => "TestOnly";
        public void Apply(Simulation sim) { }
    }
}
