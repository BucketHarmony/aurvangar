using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Tests.Support;

/// <summary>CON-09 invariant check for tests, written independently of <c>Blocks.Support</c>: support spreads from
/// the built blocks that rest on ground (a solid non-construction block below or beside them) to built blocks above
/// or beside a supported one. Whatever is left is floating.</summary>
public static class Grounding
{
    private static readonly Int3[] UpAndSides = { Int3.Up, new(-1, 0, 0), new(1, 0, 0), new(0, 0, -1), new(0, 0, 1) };
    private static readonly Int3[] DownAndSides = { Int3.Down, new(-1, 0, 0), new(1, 0, 0), new(0, 0, -1), new(0, 0, 1) };

    /// <summary>Every built block in the world that is not grounded, in index order.</summary>
    public static List<Int3> Floating(Simulation sim)
    {
        var world = sim.World;
        var built = new List<Int3>();
        for (int y = 0; y < world.SizeY; y++)
            for (int z = 0; z < world.SizeZ; z++)
                for (int x = 0; x < world.SizeX; x++)
                    if (sim.Content.IsConstruction(world.GetBlock(x, y, z))) built.Add(new Int3(x, y, z));

        bool IsBuilt(Int3 c) => world.InBounds(c) && sim.Content.IsConstruction(world.GetBlock(c));
        bool IsGround(Int3 c) => world.InBounds(c) && world.IsSolid(c) && !sim.Content.IsConstruction(world.GetBlock(c));

        var grounded = new HashSet<Int3>();
        var queue = new Queue<Int3>();
        foreach (var c in built)
            if (DownAndSides.Any(d => IsGround(c + d)) && grounded.Add(c)) queue.Enqueue(c);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in UpAndSides)
                if (IsBuilt(c + d) && grounded.Add(c + d)) queue.Enqueue(c + d);
        }
        return built.Where(c => !grounded.Contains(c)).ToList();
    }
}
