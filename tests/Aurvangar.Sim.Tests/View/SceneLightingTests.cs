using System.Globalization;
using System.Text.RegularExpressions;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>
/// M3-T9: Main.tscn carries a WorldEnvironment with a flat ambient color so faces turned away from the sun and
/// cast shadows are darker than lit faces but never black (ADR-022). The sun itself stays as it was.
/// </summary>
[Trait("Category", "Unit")]
public class SceneLightingTests
{
    private static string MainScene =>
        File.ReadAllText(Path.Combine(TestContent.RepoRoot, "src", "Aurvangar.Godot", "scenes", "Main.tscn"));

    private static float Prop(string scene, string name)
    {
        var m = Regex.Match(scene, $@"^{Regex.Escape(name)} = ([0-9.]+)", RegexOptions.Multiline);
        Assert.True(m.Success, $"Main.tscn has no '{name}'");
        return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void MainScene_HasWorldEnvironment_WithColorAmbient()
    {
        var scene = MainScene;
        Assert.Contains("type=\"WorldEnvironment\"", scene);
        Assert.Contains("[sub_resource type=\"Environment\"", scene);
        Assert.Equal(2f, Prop(scene, "ambient_light_source")); // 2 = AmbientSource.Color
        float energy = Prop(scene, "ambient_light_energy");
        Assert.InRange(energy, 0.2f, 1.0f); // visible fill, well below the sun (energy 1)
    }

    [Fact]
    public void MainScene_SunUnchanged()
    {
        var scene = MainScene;
        Assert.Contains(
            "transform = Transform3D(0.866, -0.354, 0.354, 0, 0.707, 0.707, -0.5, -0.612, 0.612, 64, 80, 64)", scene);
        Assert.DoesNotContain("light_energy", scene.Replace("ambient_light_energy", ""));
    }
}
