using Aurvangar.ViewCore.Entities;
using Godot;

namespace Aurvangar.Client;

/// <summary>Buildings (VIEW-09): one footprint box per building in the color <see cref="BuildingVisuals"/> gives
/// (translucent blueprint, semi-opaque site, solid complete), a label with a progress bar above sites and teardowns,
/// and a red "NO WATER" billboard over a dry pump. Nodes are pooled by index and refreshed every frame from the
/// ViewCore data; this node never reads sim state.</summary>
public partial class BuildingRenderer : Node3D
{
    public const float BarWidth = 1.6f;
    public const float BarHeight = 0.22f;

    private sealed record Slot(Node3D Root, MeshInstance3D Box, StandardMaterial3D Material, Label3D Label,
        Node3D Bar, MeshInstance3D Fill, Label3D NoWater);

    private static readonly BoxMesh UnitBox = new() { Size = Vector3.One };
    private static readonly StandardMaterial3D BarBack = Flat(new Color(0.08f, 0.08f, 0.08f, 0.85f));
    private static readonly StandardMaterial3D BarFill = Flat(new Color(0.35f, 0.85f, 0.35f));

    private readonly List<Slot> _slots = new();

    public void Refresh(IReadOnlyList<BuildingVisual> buildings)
    {
        while (_slots.Count < buildings.Count) _slots.Add(Create(_slots.Count));
        // Progress bars face the camera like the labels.
        var facing = GetViewport().GetCamera3D()?.GlobalBasis ?? Basis.Identity;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (i >= buildings.Count) { s.Root.Visible = false; continue; }
            var v = buildings[i];
            s.Root.Visible = true;
            var size = MeshConvert.ToGodot(v.Size);
            s.Box.Scale = size;
            s.Box.Position = MeshConvert.ToGodot(v.Min) + size / 2f;
            ApplyColor(s.Material, v);

            var anchor = MeshConvert.ToGodot(v.LabelAnchor);
            s.Label.Visible = v.Label.Length > 0;
            if (s.Label.Visible)
            {
                if (s.Label.Text != v.Label) s.Label.Text = v.Label;
                s.Label.Position = anchor + new Vector3(0, BarHeight + 0.05f, 0);
            }
            s.Bar.Visible = v.Progress >= 0f;
            if (s.Bar.Visible)
            {
                s.Bar.Position = anchor;
                s.Bar.Basis = facing;
                float f = Mathf.Clamp(v.Progress, 0f, 1f);
                s.Fill.Visible = f > 0f;
                s.Fill.Scale = new Vector3(Mathf.Max(f, 0.001f) * BarWidth, BarHeight * 0.7f, 0.02f);
                s.Fill.Position = new Vector3(-BarWidth / 2f + f * BarWidth / 2f, 0, 0.02f);
            }
            s.NoWater.Visible = v.NoWater;
            if (v.NoWater) s.NoWater.Position = anchor + new Vector3(0, 0.3f, 0);
        }
    }

    private static void ApplyColor(StandardMaterial3D m, BuildingVisual v)
    {
        var c = MeshConvert.ToColor(v.Color);
        if (m.AlbedoColor == c) return;
        m.AlbedoColor = c;
        bool clear = c.A < 0.999f;
        m.Transparency = clear ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
        // Blueprints are drawn flat (a plan, not an object); sites and finished buildings are lit like terrain.
        m.ShadingMode = v.Look == BuildingLook.Blueprint ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel;
    }

    private Slot Create(int index)
    {
        var root = new Node3D { Name = $"Building{index}" };
        var material = new StandardMaterial3D { Roughness = 1f };
        var box = new MeshInstance3D { Name = "Box", Mesh = UnitBox, MaterialOverride = material };
        root.AddChild(box);

        var label = Billboard("Label", Colors.White, PileRenderer.FontSize);
        root.AddChild(label);

        var bar = new Node3D { Name = "Bar" };
        bar.AddChild(new MeshInstance3D
        {
            Name = "Back", Mesh = UnitBox, MaterialOverride = BarBack, Scale = new Vector3(BarWidth, BarHeight, 0.02f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        var fill = new MeshInstance3D
        {
            Name = "Fill", Mesh = UnitBox, MaterialOverride = BarFill, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        bar.AddChild(fill);
        root.AddChild(bar);

        var noWater = Billboard("NoWater", new Color(1f, 0.3f, 0.25f), PileRenderer.FontSize + 8);
        noWater.Text = "NO WATER";
        root.AddChild(noWater);

        AddChild(root);
        return new Slot(root, box, material, label, bar, fill, noWater);
    }

    private static Label3D Billboard(string name, Color color, int fontSize) => new()
    {
        Name = name,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        FixedSize = true,
        PixelSize = PileRenderer.LabelPixelSize,
        FontSize = fontSize,
        OutlineSize = 10,
        Modulate = color,
        OutlineModulate = new Color(0, 0, 0, 0.85f),
        Shaded = false,
        DoubleSided = true,
        NoDepthTest = true,
        VerticalAlignment = VerticalAlignment.Bottom,
        Visible = false,
    };

    private static StandardMaterial3D Flat(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = c.A < 0.999f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };
}
