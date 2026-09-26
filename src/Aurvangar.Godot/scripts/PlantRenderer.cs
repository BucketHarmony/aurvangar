using Aurvangar.Sim;
using Aurvangar.ViewCore.Meshing;
using Godot;

namespace Aurvangar.Client;

/// <summary>Placeholder trees and bushes (M3-T6): one MeshInstance3D for all plants from <see cref="PlantMesher"/>
/// (trunk boxes, canopy and bush cones). Rebuilt by GameRoot when the slice level or the plant count changes.</summary>
public partial class PlantRenderer : MeshInstance3D
{
    private Simulation _sim = null!;
    private PlantColors _colors = null!;
    private int _builtSlice = int.MinValue;
    private int _builtCount = -1;

    public void Init(Simulation sim, PlantColors colors)
    {
        _sim = sim;
        _colors = colors;
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
    }

    /// <summary>Rebuilds if the slice or the number of plants changed since the last build (or when forced).</summary>
    public void Refresh(int sliceY, bool force = false)
    {
        int count = _sim.Plants.Count;
        if (!force && sliceY == _builtSlice && count == _builtCount) return;
        _builtSlice = sliceY;
        _builtCount = count;
        Mesh = MeshConvert.ToArrayMesh(PlantMesher.Build(_sim.Plants, sliceY, _colors));
    }
}
