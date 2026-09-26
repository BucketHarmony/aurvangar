using Aurvangar.Sim;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Crops on farm tiles (M6-T5) from <see cref="CropMesher"/>: one shaded, opaque mesh. Farms emit no events,
/// so <see cref="Refresh"/> compares <see cref="CropMesher.Signature"/> (tile states, growth stages, moisture) and
/// rebuilds only when it or the slice changed.</summary>
public partial class CropRenderer : MeshInstance3D
{
    private Simulation _sim = null!;
    private PlantColors _colors = null!;
    private ulong _builtSignature;
    private int _builtSlice = int.MinValue;

    public void Init(Simulation sim, PlantColors colors)
    {
        _sim = sim;
        _colors = colors;
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
    }

    public void Refresh(int sliceY)
    {
        ulong signature = CropMesher.Signature(_sim);
        if (signature == _builtSignature && sliceY == _builtSlice) return;
        _builtSignature = signature;
        _builtSlice = sliceY;
        Mesh = MeshConvert.ToArrayMesh(CropMesher.Build(_sim, sliceY, _colors));
    }
}
