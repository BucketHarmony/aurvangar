using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

/// <summary>CON-07 (M8-T2): build shapes and DesignateBuild rejections.</summary>
[Trait("Category", "Unit")]
public class BuildShapeTests
{
    private static List<Int3> Cells(BuildShape s, Int3 a, Int3 b, int h = 1)
    {
        var cells = BuildShapes.Cells(s, a, b, h);
        Assert.Equal(BuildShapes.Count(s, a, b, h), cells.Count);
        for (int i = 1; i < cells.Count; i++)
        {
            var p = cells[i - 1];
            var q = cells[i];
            bool ordered = p.Y < q.Y || (p.Y == q.Y && (p.Z < q.Z || (p.Z == q.Z && p.X < q.X)));
            Assert.True(ordered, $"{p} before {q}: not in (y, index) order");
        }
        return cells;
    }

    [Fact]
    public void Shapes_ExpandToExpectedCells()
    {
        Assert.Equal(new[] { new Int3(3, 4, 5) }, Cells(BuildShape.Single, new(3, 4, 5), new(9, 9, 9), 7));

        // Line along the longer axis; B.Y is ignored; ties go to X.
        Assert.Equal(Enumerable.Range(2, 5).Select(x => new Int3(x, 5, 3)), Cells(BuildShape.Line, new(2, 5, 3), new(6, 9, 4)));
        Assert.Equal(Enumerable.Range(2, 6).Select(z => new Int3(2, 1, z)), Cells(BuildShape.Line, new(2, 1, 2), new(3, 1, 7)));
        Assert.Equal(Enumerable.Range(0, 4).Select(x => new Int3(x, 1, 0)), Cells(BuildShape.Line, new(0, 1, 0), new(3, 1, 3)));

        var wall = Cells(BuildShape.Wall, new(6, 2, 1), new(2, 2, 1), 3);
        Assert.Equal(15, wall.Count);
        Assert.All(wall, c => Assert.True(c.X is >= 2 and <= 6 && c.Y is >= 2 and <= 4 && c.Z == 1));

        var floor = Cells(BuildShape.Floor, new(5, 3, 4), new(2, 8, 2), 9);
        Assert.Equal(12, floor.Count);
        Assert.All(floor, c => Assert.Equal(3, c.Y));

        var box = Cells(BuildShape.HollowBox, new(1, 1, 1), new(5, 1, 4), 2);
        Assert.Equal(28, box.Count);
        Assert.DoesNotContain(new Int3(3, 1, 2), box);
        Assert.Contains(new Int3(1, 2, 4), box);
        Assert.Equal(Cells(BuildShape.Wall, new(1, 1, 1), new(1, 1, 5), 2), Cells(BuildShape.HollowBox, new(1, 1, 1), new(1, 1, 5), 2));

        Assert.Equal(new Int3[] { new(2, 1, 2), new(3, 2, 2), new(4, 3, 2), new(5, 4, 2) },
            Cells(BuildShape.Stair, new(2, 1, 2), new(5, 1, 2), 9));
        Assert.Equal(new Int3[] { new(5, 1, 2), new(4, 2, 2), new(3, 3, 2), new(2, 4, 2) },
            Cells(BuildShape.Stair, new(5, 1, 2), new(2, 1, 2)));
        Assert.Equal(new Int3[] { new(1, 1, 6), new(1, 2, 5), new(1, 3, 4) }, Cells(BuildShape.Stair, new(1, 1, 6), new(1, 1, 4)));

        Assert.True(BuildShapes.Count(BuildShape.Floor, new(0, 0, 0), new(int.MaxValue, 0, int.MaxValue), 1) > BuildShapes.MaxCells);
    }

    [Fact]
    public void DesignateBuild_Rejections_InOrder()
    {
        var sim = new ScenarioBuilder().Ground(8).Build();
        string? Run(ICommand c)
        {
            sim.Events.Drain();
            sim.Enqueue(c);
            sim.Tick();
            var rejected = sim.Events.Drain().OfType<CommandRejected>().Where(r => r.Command == "DesignateBuild").ToList();
            Assert.True(rejected.Count <= 1);
            return rejected.Count == 0 ? null : rejected[0].Reason;
        }
        var a = new Int3(5, 9, 5);
        // BadHeight wins over every later check, even a bad block.
        Assert.Equal("BadHeight", Run(new DesignateBuild(BuildShape.Wall, a, new(5, 9, 9), 0, BlockId.Stone, false)));
        Assert.Equal("BadHeight", Run(new DesignateBuild(BuildShape.Single, a, a, 33, BlockId.Masonry, false)));
        Assert.Equal("NotBuildable", Run(new DesignateBuild(BuildShape.Floor, a, new(9999, 9, 9999), 1, BlockId.Stone, false)));
        Assert.Equal("NotBuildable", Run(new DesignateBuild(BuildShape.Single, a, a, 1, BlockId.BuildingSolid, false)));
        Assert.Equal("TooLarge", Run(new DesignateBuild(BuildShape.Floor, new(-500, 9, -500), new(500, 9, 500), 1, BlockId.Masonry, false)));
        Assert.Equal("TooLarge", Run(new DesignateBuild(BuildShape.Wall, new(0, 9, 5), new(128, 9, 5), 32, BlockId.Masonry, false)));
        Assert.Equal("OutOfWorld", Run(new DesignateBuild(BuildShape.Line, new(40, 9, 5), new(45, 9, 5), 1, BlockId.Masonry, false)));
        Assert.Equal("NothingToBuild", Run(new DesignateBuild(BuildShape.Floor, new(2, 8, 2), new(6, 8, 6), 1, BlockId.Masonry, false)));
        Assert.Equal(0, sim.Plans.Count);

        Assert.Null(Run(new DesignateBuild(BuildShape.Single, a, a, 1, BlockId.Masonry, false)));
        Assert.Equal(new PlanEntry(BlockId.Masonry, PlanState.Released), sim.Plans.Get(a));
        Assert.Null(Run(new DesignateBuild(BuildShape.Single, a + Int3.Up, a + Int3.Up, 1, BlockId.Planks, true)));
        Assert.Equal(new PlanEntry(BlockId.Planks, PlanState.Planned), sim.Plans.Get(a + Int3.Up));
    }
}
