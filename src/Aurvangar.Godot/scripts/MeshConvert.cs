using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Copies engine-neutral <see cref="MeshData"/> into Godot types. Winding is flipped by
/// <see cref="MeshWinding"/> (MeshData is counter-clockwise, Godot front faces are clockwise).</summary>
public static class MeshConvert
{
    /// <summary>One-surface ArrayMesh with vertex colors, or null when the mesh is empty.</summary>
    public static ArrayMesh? ToArrayMesh(MeshData data)
    {
        if (data.IsEmpty) return null;
        int n = data.Positions.Count;
        var positions = new Vector3[n];
        var normals = new Vector3[n];
        var colors = new Color[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = ToGodot(data.Positions[i]);
            normals[i] = ToGodot(data.Normals[i]);
            var c = data.Colors[i];
            colors[i] = new Color(c.X, c.Y, c.Z, c.W);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = MeshWinding.ClockwiseIndices(data);

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Triangle soup for a ConcavePolygonShape3D (VIEW-03 picking collision).</summary>
    public static Vector3[] ToCollisionFaces(MeshData data)
    {
        var tris = MeshWinding.ClockwiseTriangles(data);
        var result = new Vector3[tris.Length];
        for (int i = 0; i < tris.Length; i++) result[i] = ToGodot(tris[i]);
        return result;
    }

    public static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    public static Color ToColor(System.Numerics.Vector4 c) => new(c.X, c.Y, c.Z, c.W);
}
