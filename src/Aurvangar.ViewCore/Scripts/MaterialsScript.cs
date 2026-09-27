using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Screenshots;

namespace Aurvangar.ViewCore.Scripts;

/// <summary>The screenshot harness's <c>materials</c> script (M9-T4, VIEW-20): one sample of every construction block
/// (CON-01), in id order, on the <see cref="BlocksScript.Find"/> site (x0, y, z0; ground level y). Each sample is a
/// released wall <see cref="SampleW"/> wide and <see cref="SampleH"/> high at z0 + <see cref="RowDz"/>, starting at
/// x0 + <see cref="SampleW"/> * i, with a planned (never released) course of the same block on top, so the shot shows
/// each material built and as a plan ghost. The pit, dug <see cref="QuarryDepth"/> layers deeper than the <c>digchop</c> pit,
/// brings in the stone (ADR-070). A Water Pump at the nearest wet site (M10-T5, ADR-075) keeps the colony alive through
/// the day-5 drought to day 10. The <c>materials</c> camera preset looks at the row.</summary>
public static class MaterialsScript
{
    public const int SampleW = 2, SampleH = 2, RowDz = 3;

    /// <summary>Layers dug below the <c>digchop</c> pit, down into the stone the samples need.</summary>
    public const int QuarryDepth = 5;

    /// <summary>The construction blocks in id order (the order of the Blocks menu).</summary>
    public static IReadOnlyList<BlockId> Blocks(Simulation sim) =>
        Enum.GetValues<BlockId>().Where(sim.Content.IsConstruction).ToList();

    /// <summary>The site: recovered from the planned top course once the script ran, else <see cref="BlocksScript.Find"/>.</summary>
    public static Int3? Site(Simulation sim)
    {
        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        foreach (var (c, e) in sim.Plans.All)
        {
            if (e.State != PlanState.Planned) continue;
            minX = Math.Min(minX, c.X); minY = Math.Min(minY, c.Y); minZ = Math.Min(minZ, c.Z);
        }
        if (minX != int.MaxValue) return new Int3(minX, minY - SampleH - 1, minZ - RowDz);
        return BlocksScript.Find(sim);
    }

    public static IReadOnlyList<ICommand> Commands(Simulation sim)
    {
        var (pitMin, pitMax) = ScreenshotScripts.PitBox(sim);
        var list = new List<ICommand>
        {
            new DesignateDig(new Int3(pitMin.X, pitMin.Y - QuarryDepth, pitMin.Z), pitMax),
        };
        // M10-T5: a Water Pump at the nearest wet site, as the build script picks it (on seed 1 MonumentScript.PumpOrigin).
        var pump = sim.Content.Building("pump");
        if (ScreenshotScripts.FindSite(sim, pump, new HashSet<Int3>(), wet: true) is { } ps) list.Add(ps);
        if (BlocksScript.Find(sim) is not { } s) return list;
        var blocks = Blocks(sim);
        int y = s.Y + 1, z = s.Z + RowDz;
        for (int i = 0; i < blocks.Count; i++)
        {
            int x = s.X + SampleW * i;
            list.Add(new DesignateBuild(BuildShape.Wall, new Int3(x, y, z), new Int3(x + SampleW - 1, y, z), SampleH, blocks[i], false));
        }
        for (int i = 0; i < blocks.Count; i++)
        {
            int x = s.X + SampleW * i;
            list.Add(new DesignateBuild(BuildShape.Line, new Int3(x, y + SampleH, z), new Int3(x + SampleW - 1, y + SampleH, z), 1, blocks[i], true));
        }
        return list;
    }
}
