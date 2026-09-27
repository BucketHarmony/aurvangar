using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>A mesh of translucent, unshaded vertex-coloured boxes (the block tool's ghost, VIEW-21; plan ghosts,
/// VIEW-22). <see cref="SetData"/> replaces the mesh; an empty one hides it.</summary>
public partial class TranslucentMesh : MeshInstance3D
{
    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    public void SetData(MeshData? data)
    {
        Mesh = data == null ? null : MeshConvert.ToArrayMesh(data);
        Visible = Mesh != null;
    }
}
