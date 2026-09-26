using Aurvangar.Sim.Core;
using Aurvangar.Sim.Plants;
using Aurvangar.Sim.Tests.Support;
using Aurvangar.Sim.World;
using Xunit;

namespace Aurvangar.Sim.Tests;

public class GoldenHashTests
{
    private static readonly long[] Checkpoints = { 0, 1200, 3000, 6000 };

    private static ulong[] RunSeed1()
    {
        var sim = WorldFactory.Create(1, TestContent.Db);
        // M5-T7 / M6-T6: apply Scripts.SurvivalScript here once it exists.
        var hashes = new List<ulong>();
        long last = 0;
        foreach (var cp in Checkpoints)
        {
            sim.RunTicks((int)(cp - last));
            last = cp;
            hashes.Add(sim.StateHash());
        }
        return hashes.ToArray();
    }

    [Fact(Skip = "M1-T4")]
    [Trait("Category", "Golden")]
    public void TwoRunsMatch()
    {
        Assert.Equal(RunSeed1(), RunSeed1());
    }

    [Fact(Skip = "M1-T4")]
    [Trait("Category", "Golden")]
    public void MatchesGoldenFile()
    {
        var path = Path.Combine(TestContent.RepoRoot, "tests", "golden", "seed1.txt");
        var actual = RunSeed1().Select((h, i) => $"{Checkpoints[i]} {h:x16}").ToArray();
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllLines(path, actual);
            return;
        }
        Assert.True(File.Exists(path), $"Golden file missing: {path}. Run with UPDATE_GOLDEN=1 once, and log it in PROGRESS.md.");
        Assert.Equal(File.ReadAllLines(path), actual);
    }
}
