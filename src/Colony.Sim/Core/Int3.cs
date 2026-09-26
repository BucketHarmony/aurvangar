namespace Colony.Sim.Core;

/// <summary>Integer cell coordinate. X east, Y up, Z south (see docs/00-overview.md).</summary>
public readonly record struct Int3(int X, int Y, int Z)
{
    public static readonly Int3 Zero = new(0, 0, 0);
    public static readonly Int3 Up = new(0, 1, 0);
    public static readonly Int3 Down = new(0, -1, 0);
    public static readonly Int3 North = new(0, 0, -1);
    public static readonly Int3 South = new(0, 0, 1);
    public static readonly Int3 East = new(1, 0, 0);
    public static readonly Int3 West = new(-1, 0, 0);

    /// <summary>The 4 horizontal neighbors in fixed order N, E, S, W.</summary>
    public static readonly Int3[] Horizontal4 = { North, East, South, West };

    /// <summary>The 8 horizontal neighbors in fixed order starting N, clockwise.</summary>
    public static readonly Int3[] Horizontal8 =
    {
        new(0, 0, -1), new(1, 0, -1), new(1, 0, 0), new(1, 0, 1),
        new(0, 0, 1), new(-1, 0, 1), new(-1, 0, 0), new(-1, 0, -1),
    };

    /// <summary>The 6 face neighbors in fixed order: -Y, +Y, N, E, S, W.</summary>
    public static readonly Int3[] Neighbors6 = { Down, Up, North, East, South, West };

    public static Int3 operator +(Int3 a, Int3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Int3 operator -(Int3 a, Int3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Int3 operator *(Int3 a, int k) => new(a.X * k, a.Y * k, a.Z * k);

    public int Manhattan(Int3 o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y) + Math.Abs(Z - o.Z);
    public int ChebyshevXZ(Int3 o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Z - o.Z));

    /// <summary>Rotate around the Y axis by a multiple of 90 degrees (clockwise seen from above).</summary>
    public Int3 RotateY(int degrees) => (((degrees % 360) + 360) % 360) switch
    {
        0 => this,
        90 => new Int3(-Z, Y, X),
        180 => new Int3(-X, Y, -Z),
        270 => new Int3(Z, Y, -X),
        _ => throw new ArgumentOutOfRangeException(nameof(degrees), "Rotation must be a multiple of 90."),
    };

    public override string ToString() => $"({X},{Y},{Z})";
}
