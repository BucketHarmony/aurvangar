using Aurvangar.Sim;
using Aurvangar.ViewCore.Entities;
using Godot;

namespace Aurvangar.Client;

/// <summary>Item piles (VIEW-10): one mesh for all pile markers from <see cref="PileMesher"/>, plus a pooled count
/// label per pile (a fixed-screen-size billboard). GameRoot rebuilds it when an <c>ItemPileChanged</c> event arrives
/// or the slice level changes, and shows or hides the labels by camera distance
/// (<see cref="PileMesher.LabelsVisible"/>).</summary>
public partial class PileRenderer : MeshInstance3D
{
    /// <summary>Label3D in fixed-size mode: about 20 px tall text on a 900 px high view.</summary>
    public const int FontSize = 36;
    public const float LabelPixelSize = 0.0009f;

    private readonly List<Label3D> _labels = new();
    private Simulation _sim = null!;
    private EntityColors _colors = null!;
    private int _used;
    private bool _labelsOn = true;

    public void Init(Simulation sim, EntityColors colors)
    {
        _sim = sim;
        _colors = colors;
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
    }

    public void Rebuild(int sliceY)
    {
        var markers = PileMesher.Markers(_sim, sliceY, _colors);
        Mesh = MeshConvert.ToArrayMesh(PileMesher.Build(markers));
        _used = markers.Count;
        while (_labels.Count < _used) _labels.Add(CreateLabel());
        for (int i = 0; i < _labels.Count; i++)
        {
            var label = _labels[i];
            if (i >= _used) { label.Visible = false; continue; }
            var m = markers[i];
            label.Position = MeshConvert.ToGodot(m.LabelAnchor);
            if (label.Text != m.Text) label.Text = m.Text;
            label.Visible = _labelsOn;
        }
    }

    /// <summary>Called every frame with the camera distance rule.</summary>
    public void SetLabelsVisible(bool on)
    {
        if (on == _labelsOn) return;
        _labelsOn = on;
        for (int i = 0; i < _used; i++) _labels[i].Visible = on;
    }

    private Label3D CreateLabel()
    {
        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = LabelPixelSize,
            FontSize = FontSize,
            OutlineSize = 10,
            Modulate = Colors.White,
            OutlineModulate = new Color(0, 0, 0, 0.85f),
            Shaded = false,
            DoubleSided = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visible = false,
        };
        AddChild(label);
        return label;
    }
}
