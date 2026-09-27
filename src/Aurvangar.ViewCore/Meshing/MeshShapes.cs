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

    /// <summary>The 12 edges of the box <paramref name="lo"/>..<paramref name="hi"/> as bars <paramref name="width"/>
    /// thick, centred on the edges (an outline; M9-T3).</summary>
    public static void AddEdges(MeshData m, Vector3 lo, Vector3 hi, float width, Vector4 color)
    {
        float h = width / 2f;
        var d = new Vector3(h, h, h);
        foreach (float y in new[] { lo.Y, hi.Y })
            foreach (float z in new[] { lo.Z, hi.Z })
                AddBox(m, new Vector3(lo.X - h, y - h, z - h), new Vector3(hi.X + h, y + h, z + h), color);
        foreach (float x in new[] { lo.X, hi.X })
            foreach (float z in new[] { lo.Z, hi.Z })
                AddBox(m, new Vector3(x, lo.Y, z) - d, new Vector3(x, hi.Y, z) + d, color);
        foreach (float x in new[] { lo.X, hi.X })
            foreach (float y in new[] { lo.Y, hi.Y })
                AddBox(m, new Vector3(x - h, y - h, lo.Z - h), new Vector3(x + h, y + h, hi.Z + h), color);
    }
}
