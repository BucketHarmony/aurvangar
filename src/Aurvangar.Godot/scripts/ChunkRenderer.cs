using Aurvangar.Sim;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Terrain view (VIEW-03). One MeshInstance3D per chunk from <see cref="ChunkMesher"/>, plus a
/// StaticBody3D with a ConcavePolygonShape3D of the same quads, used only for mouse picking (VIEW-05, M3-T5).
/// Meshes are in world coordinates, so every node sits at the origin. Remeshing is driven by GameRoot's budgeted
/// queue (VIEW-02); this node never reads events itself.</summary>
public partial class ChunkRenderer : Node3D
{
    /// <summary>Physics layer of terrain collision (picking rays test this layer).</summary>
    public const uint PickLayer = 1;

    private Simulation _sim = null!;
    private BlockColors _colors = null!;
    private MeshInstance3D?[] _meshes = System.Array.Empty<MeshInstance3D?>();
    private StaticBody3D?[] _bodies = System.Array.Empty<StaticBody3D?>();
    private StandardMaterial3D _material = null!;

    public void Init(Simulation sim, BlockColors colors)
    {
        _sim = sim;
        _colors = colors;
        _meshes = new MeshInstance3D?[sim.World.ChunkCount];
        _bodies = new StaticBody3D?[sim.World.ChunkCount];
        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1f,
        };
    }

    /// <summary>Rebuilds the mesh and collision of one chunk at the given slice level (VIEW-04).</summary>
    public void Remesh(int chunkIndex, int sliceY)
    {
        var (cx, cy, cz) = _sim.World.ChunkCoords(chunkIndex);
        var data = ChunkMesher.Build(_sim.World, cx, cy, cz, sliceY, _colors);
        var mesh = MeshConvert.ToArrayMesh(data);

        var mi = _meshes[chunkIndex];
        var body = _bodies[chunkIndex];
        if (mesh == null)
        {
            if (mi != null) mi.Visible = false;
            if (body != null) body.GetNode<CollisionShape3D>("Shape").Shape = null;
            return;
        }

        if (mi == null)
        {
            mi = new MeshInstance3D { Name = $"Chunk{chunkIndex}", MaterialOverride = _material };
            AddChild(mi);
            _meshes[chunkIndex] = mi;
        }
        mi.Mesh = mesh;
        mi.Visible = true;

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(MeshConvert.ToCollisionFaces(data));
        if (body == null)
        {
            body = new StaticBody3D { Name = $"ChunkBody{chunkIndex}", CollisionLayer = PickLayer, CollisionMask = 0 };
            body.SetMeta("chunk", chunkIndex);
            body.AddChild(new CollisionShape3D { Name = "Shape", Shape = shape });
            AddChild(body);
            _bodies[chunkIndex] = body;
        }
        else
        {
            body.GetNode<CollisionShape3D>("Shape").Shape = shape;
        }
    }
}
