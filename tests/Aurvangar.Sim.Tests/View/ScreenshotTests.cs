using System.Numerics;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Screenshots;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>M3-T6: screenshot harness command line (VIEW-20).</summary>
public class ScreenshotArgsTests
{
    [Fact]
    public void Defaults_WhenNoArgs()
    {
        var a = ScreenshotArgs.Parse(Array.Empty<string>());
        Assert.Equal(1UL, a.Seed);
        Assert.Equal(1200, a.Ticks);
        Assert.Equal(new[] { "overview", "river", "hub", "slice" }, a.Shots);
        Assert.Equal("artifacts/screens", a.OutDir);
    }

    [Fact]
    public void ParsesSpecExample()
    {
        var a = ScreenshotArgs.Parse(new[]
            { "--seed", "7", "--ticks", "300", "--shots", "overview,river,hub", "--out", "E:/x/screens" });
        Assert.Equal(7UL, a.Seed);
        Assert.Equal(300, a.Ticks);
        Assert.Equal(new[] { "overview", "river", "hub" }, a.Shots);
        Assert.Equal("E:/x/screens", a.OutDir);
        Assert.Equal(Path.Combine("E:/x/screens", "river.png"), a.OutputPath("river"));
    }

    [Theory]
    [InlineData("--shots", "overview,moon")]
    [InlineData("--ticks", "-5")]
    [InlineData("--seed", "abc")]
    [InlineData("--bogus", "1")]
    [InlineData("--shots", "")]
    public void InvalidArgs_Throw(string key, string value) =>
        Assert.Throws<ArgumentException>(() => ScreenshotArgs.Parse(new[] { key, value }));

    [Fact]
    public void MissingValue_Throws() =>
        Assert.Throws<ArgumentException>(() => ScreenshotArgs.Parse(new[] { "--seed" }));
}

/// <summary>M3-T6: the four camera presets from docs/testing.md, computed from the seed-1 world.</summary>
public class ScreenshotPresetTests
{
    private static readonly Lazy<Simulation> Seed1 = new(() => WorldFactory.Create(1, TestContent.Db));

    private static int SurfaceY(VoxelWorld w, int x, int z)
    {
        for (int y = w.SizeY - 1; y >= 0; y--) if (w.IsSolid(x, y, z)) return y;
        return -1;
    }

    [Fact]
    public void AllPresetNamesResolve_AndFocusIsInsideWorld_AndNotAboveSlice()
    {
        var sim = Seed1.Value;
        foreach (var name in ScreenshotPresets.Names)
        {
            var shot = ScreenshotPresets.For(name, sim);
            Assert.Equal(name, shot.Name);
            Assert.InRange(shot.Focus.X, 0, sim.World.SizeX);
            Assert.InRange(shot.Focus.Z, 0, sim.World.SizeZ);
            Assert.InRange(shot.Pitch, OrbitRig.MinPitch, OrbitRig.MaxPitch);
            Assert.InRange(shot.Distance, OrbitRig.MinDistance, OrbitRig.MaxDistance);
            Assert.InRange(shot.SliceY, 0, sim.World.SizeY - 1);
            Assert.True(shot.Focus.Y <= shot.SliceY + 1, $"{name}: focus must not float above the slice");
        }
        Assert.Throws<ArgumentException>(() => ScreenshotPresets.For("moon", sim));
    }

    [Fact]
    public void Overview_WholeMapAt45Degrees_NoSlice()
    {
        var sim = Seed1.Value;
        var shot = ScreenshotPresets.For("overview", sim);
        Assert.Equal(45f, shot.Pitch);
        Assert.Equal(sim.World.SizeY - 1, shot.SliceY);
        Assert.Equal(sim.World.SizeX / 2f, shot.Focus.X, 1);
        Assert.Equal(sim.World.SizeZ / 2f, shot.Focus.Z, 1);
        Assert.Equal(OrbitRig.MaxDistance, shot.Distance);
    }

    [Fact]
    public void Hub_FocusesFootprintCenter_CloseUp()
    {
        var sim = Seed1.Value;
        var hub = sim.Buildings.All.First();
        var cells = hub.FootprintCells().ToList();
        var shot = ScreenshotPresets.For("hub", sim);
        Assert.Equal((float)cells.Average(c => c.X) + 0.5f, shot.Focus.X, 2);
        Assert.Equal((float)cells.Average(c => c.Z) + 0.5f, shot.Focus.Z, 2);
        Assert.Equal(hub.Origin.Y, shot.Focus.Y, 2);
        Assert.True(shot.Distance <= 40f);
        Assert.Equal(sim.World.SizeY - 1, shot.SliceY);
    }

    [Fact]
    public void River_FocusBetweenHubAndNearestRiverWater()
    {
        var sim = Seed1.Value;
        var hub = ScreenshotPresets.For("hub", sim).Focus;
        var shot = ScreenshotPresets.For("river", sim);
        var water = ScreenshotPresets.NearestWaterAlongZ(sim, (int)hub.X, (int)hub.Z);
        Assert.NotNull(water);
        Assert.True(sim.Water.GetLevel(water!.Value) > 0);
        Assert.Equal((hub.Z + water.Value.Z + 0.5f) / 2f, shot.Focus.Z, 1);
        Assert.Equal(hub.X, shot.Focus.X, 1);
        // Both ends fit: the camera is at least as far away as the hub-to-river span.
        Assert.True(shot.Distance >= MathF.Abs(hub.Z - water.Value.Z));
        Assert.Equal(sim.World.SizeY - 1, shot.SliceY);
    }

    [Fact]
    public void Slice_AtY20_OverTheHillPeak()
    {
        var sim = Seed1.Value;
        var w = sim.World;
        var shot = ScreenshotPresets.For("slice", sim);
        Assert.Equal(20, shot.SliceY);
        int peak = 0;
        for (int z = 0; z < w.SizeZ; z++)
            for (int x = 0; x < w.SizeX; x++) peak = Math.Max(peak, SurfaceY(w, x, z));
        Assert.Equal(peak, SurfaceY(w, (int)shot.Focus.X, (int)shot.Focus.Z));
        Assert.True(peak > 20, "the hill must be cut by the slice");
        // GEN-02 hill center is (90, 40); the peak is on the hill.
        Assert.InRange(shot.Focus.X, 90 - 26, 90 + 26);
        Assert.InRange(shot.Focus.Z, 40 - 26, 40 + 26);
    }

    [Fact]
    public void ApplyToRig_SetsView_AndUpdateKeepsIt()
    {
        var sim = Seed1.Value;
        var shot = ScreenshotPresets.For("slice", sim);
        var rig = new OrbitRig(sim.World.SizeX, sim.World.SizeZ, new Vector3(1, 1, 1), 1);
        shot.ApplyTo(rig);
        rig.Update(1f, shot.SliceY);
        Assert.Equal(shot.Focus.X, rig.Focus.X, 3);
        Assert.Equal(shot.Focus.Y, rig.Focus.Y, 3);
        Assert.Equal(shot.Focus.Z, rig.Focus.Z, 3);
        Assert.Equal(shot.Yaw, rig.Yaw, 3);
        Assert.Equal(shot.Pitch, rig.Pitch, 3);
        Assert.Equal(shot.Distance, rig.Distance, 3);
    }
}

/// <summary>M3-T6: placeholder plant meshes (trunk box + canopy cone for trees, small cone for bushes).</summary>
public class PlantMesherTests
{
    private static PlantColors Colors => new(TestContent.Db);

    private static PlantSystem Plants(out VoxelWorld world)
    {
        world = new VoxelWorld(32, 32, 32, TestContent.Db.SolidTable);
        return new PlantSystem(world);
    }

    [Fact]
    public void Tree_TrunkBoxPlusCanopyCone()
    {
        var plants = Plants(out _);
        plants.AddTree(new Int3(5, 3, 5));
        var mesh = PlantMesher.Build(plants, sliceY: 31, Colors);
        Assert.Equal(PlantMesher.TrunkQuads + PlantMesher.ConeQuads, mesh.QuadCount);
        float minY = mesh.Positions.Min(p => p.Y), maxY = mesh.Positions.Max(p => p.Y);
        Assert.Equal(3f, minY, 3);
        Assert.True(maxY > 3 + PlantSystem.TreeHeight, "the canopy tops the 4-cell trunk");
        Assert.All(mesh.Positions, p => Assert.InRange(p.X, 4f, 7f));
        Assert.Contains(mesh.Colors, c => c == Colors.Trunk);
        Assert.Contains(mesh.Colors, c => c == Colors.Canopy);
    }

    [Fact]
    public void Bush_SmallConeWithinItsCell()
    {
        var plants = Plants(out _);
        plants.AddBush(new Int3(5, 3, 5));
        var mesh = PlantMesher.Build(plants, sliceY: 31, Colors);
        Assert.Equal(PlantMesher.ConeQuads, mesh.QuadCount);
        Assert.All(mesh.Positions, p =>
        {
            Assert.InRange(p.X, 5f, 6f);
            Assert.InRange(p.Y, 3f, 4f);
            Assert.InRange(p.Z, 5f, 6f);
        });
        Assert.All(mesh.Colors, c => Assert.Equal(Colors.Bush, c));
    }

    [Fact]
    public void QuadsWoundCounterClockwiseFromNormalSide()
    {
        var plants = Plants(out _);
        plants.AddTree(new Int3(5, 3, 5));
        plants.AddBush(new Int3(9, 3, 9));
        var mesh = PlantMesher.Build(plants, sliceY: 31, Colors);
        for (int q = 0; q < mesh.QuadCount; q++)
        {
            var a = mesh.Positions[q * 4];
            var b = mesh.Positions[q * 4 + 1];
            var c = mesh.Positions[q * 4 + 2];
            var d = mesh.Positions[q * 4 + 3];
            var n = mesh.Normals[q * 4];
            // Degenerate cone apex quads: use whichever triangle has area.
            var cross = Vector3.Cross(b - a, c - a);
            if (cross.LengthSquared() < 1e-8f) cross = Vector3.Cross(c - a, d - a);
            Assert.True(Vector3.Dot(cross, n) > 0, $"quad {q} is wound clockwise");
        }
    }

    [Fact]
    public void Slice_HidesPlantsAboveIt_AndTruncatesTrunk()
    {
        var plants = Plants(out _);
        plants.AddTree(new Int3(5, 3, 5));     // trunk y 3..6
        plants.AddBush(new Int3(9, 10, 9));    // above the slice
        var mesh = PlantMesher.Build(plants, sliceY: 4, Colors);
        Assert.Equal(PlantMesher.TrunkQuads, mesh.QuadCount); // canopy and bush hidden
        Assert.Equal(5f, mesh.Positions.Max(p => p.Y), 3);   // trunk cut at the top of cell y = 4

        Assert.True(PlantMesher.Build(plants, sliceY: 2, Colors).IsEmpty);
    }

    [Fact]
    public void SeedOne_AllPlantsMeshed()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        int trees = sim.Plants.All.Count(p => p.Kind == PlantKind.Tree);
        int bushes = sim.Plants.All.Count(p => p.Kind == PlantKind.Bush);
        Assert.True(trees > 0 && bushes > 0);
        var mesh = PlantMesher.Build(sim.Plants, sim.World.SizeY - 1, Colors);
        Assert.Equal(trees * (PlantMesher.TrunkQuads + PlantMesher.ConeQuads) + bushes * PlantMesher.ConeQuads,
            mesh.QuadCount);
    }
}

/// <summary>M3-T6: the Godot scenes cannot be opened in an editor in CI, so check their references statically:
/// every <c>res://</c> path exists, and each C# script defines a partial class named like its file (Godot needs it).</summary>
public class GodotSceneFileTests
{
    private static string GodotDir => Path.Combine(TestContent.RepoRoot, "src", "Aurvangar.Godot");

    [Theory]
    [InlineData("scenes/Main.tscn")]
    [InlineData("scenes/Screenshot.tscn")]
    public void SceneReferencesExist(string scene)
    {
        string text = File.ReadAllText(Path.Combine(GodotDir, scene));
        Assert.StartsWith("[gd_scene", text);
        var paths = System.Text.RegularExpressions.Regex.Matches(text, "path=\"res://([^\"]+)\"")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(paths);
        foreach (var rel in paths)
        {
            string full = Path.Combine(GodotDir, rel);
            Assert.True(File.Exists(full), $"{scene} references missing res://{rel}");
            if (rel.EndsWith(".cs"))
            {
                string cls = Path.GetFileNameWithoutExtension(rel);
                Assert.Contains($"public partial class {cls} ", File.ReadAllText(full));
            }
        }
    }

    [Fact]
    public void ScreenshotScene_InstancesMain_UnderRunnerRoot()
    {
        string text = File.ReadAllText(Path.Combine(GodotDir, "scenes/Screenshot.tscn"));
        Assert.Contains("path=\"res://scripts/ScreenshotRunner.cs\"", text);
        Assert.Contains("path=\"res://scenes/Main.tscn\"", text);
        Assert.Contains("[node name=\"Main\" parent=\".\" instance=", text);
    }
}
