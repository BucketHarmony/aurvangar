using System.Numerics;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Engine-neutral triangle mesh. The Godot layer copies these into an ArrayMesh.</summary>
public sealed class MeshData
{
    public List<Vector3> Positions { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<Vector4> Colors { get; } = new();
    public List<int> Indices { get; } = new();

    /// <summary>Number of quads emitted (each quad = 4 vertices, 6 indices).</summary>
    public int QuadCount { get; private set; }

    /// <summary>Quads that are slice cut faces (VIEW-04). Subset of QuadCount.</summary>
    public int CutQuadCount { get; private set; }

    public bool IsEmpty => QuadCount == 0;

    /// <summary>Append a quad. Corners must be in counter-clockwise order seen from the normal side.</summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector4 color, bool isCut = false)
    {
        int i = Positions.Count;
        Positions.Add(a); Positions.Add(b); Positions.Add(c); Positions.Add(d);
        for (int k = 0; k < 4; k++) { Normals.Add(normal); Colors.Add(color); }
        Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2);
        Indices.Add(i); Indices.Add(i + 2); Indices.Add(i + 3);
        QuadCount++;
        if (isCut) CutQuadCount++;
    }
}
