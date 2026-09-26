using Aurvangar.Sim;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Water view (VIEW-07). One semi-transparent MeshInstance3D per chunk from <see cref="WaterMesher"/>,
/// vertex colors carry the depth tint and alpha (palette water.*). No collision: picks go through water to the
/// terrain. Remeshing is driven by GameRoot's budgeted queue (VIEW-02).</summary>
public partial class WaterRenderer : Node3D
{
    private Simulation _sim = null!;
    private WaterColors _colors = null!;
    private MeshInstance3D?[] _meshes = System.Array.Empty<MeshInstance3D?>();
    private StandardMaterial3D _material = null!;

    public void Init(Simulation sim, WaterColors colors)
    {
        _sim = sim;
        _colors = colors;
        _meshes = new MeshInstance3D?[sim.World.ChunkCount];
        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Seen from above and, with slicing, from the side or below: draw both faces.
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.2f,
        };
    }

    public void Remesh(int chunkIndex, int sliceY)
    {
        var (cx, cy, cz) = _sim.World.ChunkCoords(chunkIndex);
        var data = WaterMesher.Build(_sim.World, _sim.Water, cx, cy, cz, sliceY, _colors);
        var mesh = MeshConvert.ToArrayMesh(data);

        var mi = _meshes[chunkIndex];
        if (mesh == null)
        {
            if (mi != null) mi.Visible = false;
            return;
        }
        if (mi == null)
        {
            mi = new MeshInstance3D
            {
                Name = $"Water{chunkIndex}",
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mi);
            _meshes[chunkIndex] = mi;
        }
        mi.Mesh = mesh;
        mi.Visible = true;
    }
}
