using Aurvangar.Sim;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Placeholder trees and bushes (M3-T6): one MeshInstance3D for all plants from <see cref="PlantMesher"/>
/// (trunk boxes, canopy and bush cones, berries on ripe bushes). Rebuilt when the slice level or
/// <see cref="PlantMesher.Signature"/> (plants and ripe bushes) changes.</summary>
public partial class PlantRenderer : MeshInstance3D
{
    private Simulation _sim = null!;
    private PlantColors _colors = null!;
    private int _builtSlice = int.MinValue;
    private ulong _builtSignature;

    public void Init(Simulation sim, PlantColors colors)
    {
        _sim = sim;
        _colors = colors;
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
    }

    /// <summary>Rebuilds if the slice or the plant signature changed since the last build (or when forced).</summary>
    public void Refresh(int sliceY, bool force = false)
    {
        ulong signature = PlantMesher.Signature(_sim.Plants);
        if (!force && sliceY == _builtSlice && signature == _builtSignature && _builtSlice != int.MinValue) return;
        _builtSlice = sliceY;
        _builtSignature = signature;
        Mesh = MeshConvert.ToArrayMesh(PlantMesher.Build(_sim.Plants, sliceY, _colors));
    }
}
