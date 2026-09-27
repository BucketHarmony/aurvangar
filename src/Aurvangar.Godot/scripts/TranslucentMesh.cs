using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>A mesh of translucent, unshaded vertex-coloured boxes (the block tool's ghost, VIEW-21; plan ghosts,
/// VIEW-22). <see cref="SetData"/> replaces the mesh; an empty one hides it. With <see cref="XRay"/> it draws without a
/// depth test and after the other translucent meshes (the block tool's red cells, M9-T3), so it shows through the
/// block that makes a cell invalid.</summary>
public partial class TranslucentMesh : MeshInstance3D
{
    public bool XRay { get; init; }

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = XRay,
            RenderPriority = XRay ? 1 : 0,
        };
    }

    public void SetData(MeshData? data)
    {
        Mesh = data == null || data.QuadCount == 0 ? null : MeshConvert.ToArrayMesh(data);
        Visible = Mesh != null;
    }
}
