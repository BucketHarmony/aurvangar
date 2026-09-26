using System.Numerics;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Converts <see cref="MeshData"/> (counter-clockwise front faces) to the clockwise front-face order Godot
/// expects, so the Godot layer only copies arrays.</summary>
public static class MeshWinding
{
    /// <summary>Index list with each triangle's winding reversed (a, b, c → a, c, b).</summary>
    public static int[] ClockwiseIndices(MeshData mesh)
    {
        var src = mesh.Indices;
        var result = new int[src.Count];
        for (int t = 0; t + 2 < src.Count; t += 3)
        {
            result[t] = src[t];
            result[t + 1] = src[t + 2];
            result[t + 2] = src[t + 1];
        }
        return result;
    }

    /// <summary>Triangle soup (3 positions per triangle, clockwise), e.g. for a concave collision shape (VIEW-03).</summary>
    public static Vector3[] ClockwiseTriangles(MeshData mesh)
    {
        var idx = ClockwiseIndices(mesh);
        var pos = mesh.Positions;
        var result = new Vector3[idx.Length];
        for (int i = 0; i < idx.Length; i++) result[i] = pos[idx[i]];
        return result;
    }
}
