using System.Text.RegularExpressions;
using Aurvangar.Sim.Tests.Support;
using Xunit;

namespace Aurvangar.Sim.Tests.View;

/// <summary>
/// M4-T13 (G2 early answer): gate screenshots render with Forward+, the renderer the game plays in, on the default
/// rendering driver. The Compatibility (opengl3) path and the separate forward_plus output folder are gone.
/// </summary>
[Trait("Category", "Unit")]
public class ScreenshotRendererTests
{
    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContent.RepoRoot }.Concat(parts).ToArray()));

    private static string Script => Read("scripts", "screenshot.sh");

    // Only the executable lines: comments may explain the history.
    private static string ScriptCode =>
        string.Join('\n', Script.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));

    [Fact]
    public void ScreenshotScript_UsesForwardPlus_OnDefaultDriver()
    {
        string code = ScriptCode;
        Assert.Contains("--rendering-method forward_plus", code);
        Assert.DoesNotContain("--rendering-driver", code);
        Assert.DoesNotContain("opengl3", code);
        Assert.DoesNotContain("gl_compatibility", code);
        Assert.DoesNotContain("forward_plus/", code);
    }

    [Fact]
    public void ScreenshotScript_KeepsScriptAndTicksOptions()
    {
        string code = ScriptCode;
        Assert.Contains("--script \"${SCRIPT:-none}\"", code);
        Assert.Contains("--ticks \"${TICKS:-1200}\"", code);
        Assert.Contains("res://scenes/Screenshot.tscn", code);
    }

    [Fact]
    public void Project_DoesNotOverrideTheForwardPlusRenderer()
    {
        string project = Read("src", "Aurvangar.Godot", "project.godot");
        Assert.Contains("\"Forward Plus\"", project);
        var method = Regex.Match(project, @"^renderer/rendering_method[^=]*=\s*""([^""]+)""", RegexOptions.Multiline);
        Assert.True(!method.Success || method.Groups[1].Value == "forward_plus",
            $"project.godot sets rendering_method to '{method.Groups[1].Value}'");
    }

    [Fact]
    public void TestingDoc_SaysScreenshotsUseForwardPlus()
    {
        string doc = Read("docs", "testing.md");
        Assert.Contains("Forward+", doc);
        Assert.DoesNotContain("opengl3", doc);
        Assert.DoesNotContain("forward_plus/", doc);
    }
}
