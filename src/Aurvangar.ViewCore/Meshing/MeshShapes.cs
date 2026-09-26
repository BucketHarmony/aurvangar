using System.Numerics;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Small shape builders shared by the entity meshers (plants, piles, designations).</summary>
public static class MeshShapes
{
    /// <summary>Axis-aligned box from <paramref name="lo"/> to <paramref name="hi"/>: 6 quads, outward normals.</summary>
    public static void AddBox(MeshData m, Vector3 lo, Vector3 hi, Vector4 color)
    {
        static Vector3 C(float x, float y, float z) => new(x, y, z);
        m.AddQuad(C(hi.X, lo.Y, lo.Z), C(hi.X, hi.Y, lo.Z), C(hi.X, hi.Y, hi.Z), C(hi.X, lo.Y, hi.Z), Vector3.UnitX, color);
        m.AddQuad(C(lo.X, lo.Y, lo.Z), C(lo.X, lo.Y, hi.Z), C(lo.X, hi.Y, hi.Z), C(lo.X, hi.Y, lo.Z), -Vector3.UnitX, color);
        m.AddQuad(C(lo.X, hi.Y, lo.Z), C(lo.X, hi.Y, hi.Z), C(hi.X, hi.Y, hi.Z), C(hi.X, hi.Y, lo.Z), Vector3.UnitY, color);
        m.AddQuad(C(lo.X, lo.Y, lo.Z), C(hi.X, lo.Y, lo.Z), C(hi.X, lo.Y, hi.Z), C(lo.X, lo.Y, hi.Z), -Vector3.UnitY, color);
        m.AddQuad(C(lo.X, lo.Y, hi.Z), C(hi.X, lo.Y, hi.Z), C(hi.X, hi.Y, hi.Z), C(lo.X, hi.Y, hi.Z), Vector3.UnitZ, color);
        m.AddQuad(C(lo.X, lo.Y, lo.Z), C(lo.X, hi.Y, lo.Z), C(hi.X, hi.Y, lo.Z), C(hi.X, lo.Y, lo.Z), -Vector3.UnitZ, color);
    }
}
