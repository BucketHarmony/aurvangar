using Aurvangar.Sim;
using Aurvangar.ViewCore.Entities;
using Godot;

namespace Aurvangar.Client;

/// <summary>Item piles (VIEW-10): one mesh for all piles from <see cref="PileMesher"/>. GameRoot rebuilds it when an
/// <c>ItemPileChanged</c> event arrives or the slice level changes.</summary>
public partial class PileRenderer : MeshInstance3D
{
    private Simulation _sim = null!;
    private EntityColors _colors = null!;

    public void Init(Simulation sim, EntityColors colors)
    {
        _sim = sim;
        _colors = colors;
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
    }

    public void Rebuild(int sliceY) => Mesh = MeshConvert.ToArrayMesh(PileMesher.Build(_sim.Piles, sliceY, _colors));
}
