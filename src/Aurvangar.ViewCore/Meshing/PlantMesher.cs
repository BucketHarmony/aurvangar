using System.Numerics;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Plants;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Plant colors from palette.json (`plants.trunk`, `plants.canopy`, `plants.bush`).</summary>
public sealed class PlantColors
{
    public Vector4 Trunk { get; }
    public Vector4 Canopy { get; }
    public Vector4 Bush { get; }

    public PlantColors(ContentDb content)
    {
        var p = content.Palette.Plants;
        Trunk = Get(p, "trunk");
        Canopy = Get(p, "canopy");
        Bush = Get(p, "bush");
    }

    private static Vector4 Get(Dictionary<string, string> p, string key) =>
        p.TryGetValue(key, out var hex) ? BlockColors.ParseHex(hex) : new Vector4(1, 0, 1, 1);
}

/// <summary>Placeholder plant meshes (M3-T6): a tree is a thin trunk box over its trunk cells plus a four-sided
/// canopy cone; a bush is a small cone inside its cell. All plants go into one mesh in world coordinates.
/// Slicing (VIEW-04): plants whose base is above the slice are hidden, trunks are cut at the top of the slice
/// layer, and a canopy is shown only when the whole trunk is at or below the slice (ADR-020).</summary>
public static class PlantMesher
{
    public const int TrunkQuads = 6;
    /// <summary>A cone is four sloped sides (triangles as quads with a repeated apex) plus a square base.</summary>
    public const int ConeQuads = 5;

    public const float TrunkHalfWidth = 0.2f;
    public const float CanopyHalfWidth = 1.2f;
    /// <summary>Canopy bottom above the tree base, in cells.</summary>
    public const float CanopyStart = 2f;
    /// <summary>Canopy apex above the top of the trunk, in cells.</summary>
    public const float CanopyOvershoot = 1.5f;
    public const float BushHalfWidth = 0.4f;
    public const float BushHeight = 0.9f;

    public static MeshData Build(PlantSystem plants, int sliceY, PlantColors colors)
    {
        var mesh = new MeshData();
        foreach (var p in plants.All)
        {
            var b = p.Base;
            if (b.Y > sliceY) continue;
            float cx = b.X + 0.5f, cz = b.Z + 0.5f;
            if (p.Kind == PlantKind.Bush)
            {
                AddCone(mesh, new Vector3(cx, b.Y, cz), BushHalfWidth, BushHeight, colors.Bush);
                continue;
            }
            int visible = Math.Min(p.Height, sliceY + 1 - b.Y);
            AddBox(mesh, new Vector3(cx - TrunkHalfWidth, b.Y, cz - TrunkHalfWidth),
                new Vector3(cx + TrunkHalfWidth, b.Y + visible, cz + TrunkHalfWidth), colors.Trunk);
            if (visible == p.Height)
            {
                float y0 = b.Y + CanopyStart;
                AddCone(mesh, new Vector3(cx, y0, cz), CanopyHalfWidth, b.Y + p.Height + CanopyOvershoot - y0,
                    colors.Canopy);
            }
        }
        return mesh;
    }

    private static void AddBox(MeshData m, Vector3 lo, Vector3 hi, Vector4 color)
    {
        Vector3 C(float x, float y, float z) => new(x, y, z);
        AddFacing(m, C(hi.X, lo.Y, lo.Z), C(hi.X, hi.Y, lo.Z), C(hi.X, hi.Y, hi.Z), C(hi.X, lo.Y, hi.Z), Vector3.UnitX, color);
        AddFacing(m, C(lo.X, lo.Y, lo.Z), C(lo.X, lo.Y, hi.Z), C(lo.X, hi.Y, hi.Z), C(lo.X, hi.Y, lo.Z), -Vector3.UnitX, color);
        AddFacing(m, C(lo.X, hi.Y, lo.Z), C(lo.X, hi.Y, hi.Z), C(hi.X, hi.Y, hi.Z), C(hi.X, hi.Y, lo.Z), Vector3.UnitY, color);
        AddFacing(m, C(lo.X, lo.Y, lo.Z), C(hi.X, lo.Y, lo.Z), C(hi.X, lo.Y, hi.Z), C(lo.X, lo.Y, hi.Z), -Vector3.UnitY, color);
        AddFacing(m, C(lo.X, lo.Y, hi.Z), C(hi.X, lo.Y, hi.Z), C(hi.X, hi.Y, hi.Z), C(lo.X, hi.Y, hi.Z), Vector3.UnitZ, color);
        AddFacing(m, C(lo.X, lo.Y, lo.Z), C(lo.X, hi.Y, lo.Z), C(hi.X, hi.Y, lo.Z), C(hi.X, lo.Y, lo.Z), -Vector3.UnitZ, color);
    }

    /// <summary>Square-based cone (pyramid): base centered at <paramref name="baseCenter"/>, apex straight above.</summary>
    private static void AddCone(MeshData m, Vector3 baseCenter, float halfWidth, float height, Vector4 color)
    {
        float h = halfWidth;
        var corners = new[]
        {
            baseCenter + new Vector3(-h, 0, -h), baseCenter + new Vector3(h, 0, -h),
            baseCenter + new Vector3(h, 0, h), baseCenter + new Vector3(-h, 0, h),
        };
        var apex = baseCenter + new Vector3(0, height, 0);
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            var mid = (a + b) / 2f - baseCenter;
            var outward = Vector3.Normalize(new Vector3(mid.X, 0, mid.Z));
            var n = Vector3.Normalize(Vector3.Cross(b - a, apex - a));
            if (Vector3.Dot(n, outward) < 0) n = -n;
            AddFacing(m, a, b, apex, apex, n, color);
        }
        AddFacing(m, corners[0], corners[1], corners[2], corners[3], -Vector3.UnitY, color);
    }

    /// <summary>Adds the quad wound counter-clockwise as seen from the normal side (the MeshData contract), flipping
    /// the corner order when needed. Handles quads with a repeated corner (triangles).</summary>
    private static void AddFacing(MeshData m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector4 color)
    {
        var cross = Vector3.Cross(b - a, c - a);
        if (cross.LengthSquared() < 1e-12f) cross = Vector3.Cross(c - a, d - a);
        if (Vector3.Dot(cross, normal) >= 0) m.AddQuad(a, b, c, d, normal, color);
        else m.AddQuad(a, d, c, b, normal, color);
    }
}
