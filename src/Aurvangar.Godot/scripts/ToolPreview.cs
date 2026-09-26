using Aurvangar.Sim.Core;
using Godot;

namespace Aurvangar.Client;

/// <summary>Translucent box over the cells a tool drag covers (VIEW-13 feedback), and the build ghost and its
/// entrance tile (VIEW-14). Hidden when no tool shows anything.</summary>
public partial class ToolPreview : MeshInstance3D
{
    private StandardMaterial3D _material = null!;

    public override void _Ready()
    {
        Mesh = new BoxMesh { Size = Vector3.One };
        _material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
    }

    /// <summary>Shows the inclusive cell box <paramref name="min"/>..<paramref name="max"/> in the given color.</summary>
    public void Show(Int3 min, Int3 max, Color color)
    {
        var size = new Vector3(max.X - min.X + 1, max.Y - min.Y + 1, max.Z - min.Z + 1) + new Vector3(0.06f, 0.06f, 0.06f);
        Position = new Vector3(min.X, min.Y, min.Z) + (size - new Vector3(0.06f, 0.06f, 0.06f)) / 2f;
        Scale = size;
        _material.AlbedoColor = color;
        Visible = true;
    }

    /// <summary>Shows a thin tile on the floor of <paramref name="cell"/> (the build ghost's entrance).</summary>
    public void ShowTile(Int3 cell, Color color)
    {
        Scale = new Vector3(0.8f, 0.08f, 0.8f);
        Position = new Vector3(cell.X + 0.5f, cell.Y + 0.04f, cell.Z + 0.5f);
        _material.AlbedoColor = color;
        Visible = true;
    }
}
