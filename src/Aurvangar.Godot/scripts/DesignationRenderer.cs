using Aurvangar.Sim;
using Aurvangar.ViewCore.Entities;
using Godot;

namespace Aurvangar.Client;

/// <summary>Designation overlay (VIEW-11): translucent dig cubes and chop rings from <see cref="DesignationMesher"/>,
/// red where colonists gave up. Designations emit no events, so <see cref="Refresh"/> compares a cheap signature and
/// rebuilds only when a mark or the slice changed.</summary>
public partial class DesignationRenderer : MeshInstance3D
{
    private Simulation _sim = null!;
    private EntityColors _colors = null!;
    private ulong _builtSignature;
    private int _builtSlice = int.MinValue;

    public void Init(Simulation sim, EntityColors colors)
    {
        _sim = sim;
        _colors = colors;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    public void Refresh(int sliceY)
    {
        ulong signature = DesignationMesher.Signature(_sim);
        if (signature == _builtSignature && sliceY == _builtSlice) return;
        _builtSignature = signature;
        _builtSlice = sliceY;
        Mesh = MeshConvert.ToArrayMesh(DesignationMesher.Build(_sim, sliceY, _colors));
    }
}
