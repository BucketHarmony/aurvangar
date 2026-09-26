using Aurvangar.ViewCore.Picking;
using Godot;

namespace Aurvangar.Client;

/// <summary>Translucent box around the picked cell (VIEW-05 feedback). Hidden when nothing is picked.</summary>
public partial class HoverMarker : MeshInstance3D
{
    public override void _Ready()
    {
        Mesh = new BoxMesh { Size = new Vector3(1.04f, 1.04f, 1.04f) };
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.9f, 0.2f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
    }

    public void SetHit(PickHit? hit)
    {
        Visible = hit.HasValue;
        if (hit is { } h) Position = new Vector3(h.Cell.X + 0.5f, h.Cell.Y + 0.5f, h.Cell.Z + 0.5f);
    }
}
