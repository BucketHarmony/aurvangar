using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Hud;
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
        Declutter();
    }

    private readonly List<ScreenRect> _labelRects = new();
    private readonly List<Label3D> _labelNodes = new();

    /// <summary>Lifts overlapping name/progress labels apart on screen (M7-T7, <see cref="LabelLayout.Declutter"/>),
    /// e.g. two adjacent levee sites. A lift is a Label3D pixel offset, so it holds at any zoom until the next frame.</summary>
    private void Declutter()
    {
        _labelRects.Clear();
        _labelNodes.Clear();
        if (GetViewport().GetCamera3D() is not { } camera) return;
        float scale = Scale(camera);
        foreach (var s in _slots)
        {
            s.Label.Offset = Vector2.Zero;
            if (!s.Root.Visible || LabelRect(s.Label, camera, scale) is not { } r) continue;
            _labelRects.Add(r);
            _labelNodes.Add(s.Label);
        }
        if (_labelRects.Count < 2) return;
        var lifts = LabelLayout.Declutter(_labelRects);
        for (int i = 0; i < lifts.Length; i++) _labelNodes[i].Offset = new Vector2(0, lifts[i] / scale);
    }

    private float Scale(Camera3D camera) =>
        LabelLayout.BillboardScale(PileRenderer.LabelPixelSize, GetViewport().GetVisibleRect().Size.Y, camera.Fov);

    /// <summary>Screen rectangles of the visible billboards (name/progress labels, progress bars, NO WATER icons), for
    /// the mouse label to keep clear of (M7-T7, <see cref="LabelLayout.PlaceTooltip"/>).</summary>
    public void CollectBillboardRects(Camera3D camera, List<ScreenRect> into)
    {
        float scale = Scale(camera);
        foreach (var s in _slots)
        {
            if (!s.Root.Visible) continue;
            if (LabelRect(s.Label, camera, scale) is { } label) into.Add(label);
            if (LabelRect(s.NoWater, camera, scale) is { } noWater) into.Add(noWater);
            if (s.Bar.Visible && !camera.IsPositionBehind(s.Bar.GlobalPosition))
            {
                var c = s.Bar.GlobalPosition;
                var right = camera.GlobalBasis.X * (BarWidth / 2f);
                var up = camera.GlobalBasis.Y * (BarHeight / 2f);
                var a = camera.UnprojectPosition(c - right + up);
                var b = camera.UnprojectPosition(c + right - up);
                into.Add(new ScreenRect(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y)));
            }
        }
    }

    /// <summary>A visible billboard label's screen rectangle (its pixel offset included), or null.</summary>
    private static ScreenRect? LabelRect(Label3D label, Camera3D camera, float scale)
    {
        if (!label.Visible || label.Text.Length == 0 || camera.IsPositionBehind(label.GlobalPosition)) return null;
        var font = label.Font ?? ThemeDB.FallbackFont;
        var text = font.GetMultilineStringSize(label.Text, HorizontalAlignment.Center, -1, label.FontSize)
            + new Vector2(label.OutlineSize, label.OutlineSize);
        var anchor = camera.UnprojectPosition(label.GlobalPosition);
        return LabelLayout.BillboardRect(new System.Numerics.Vector2(anchor.X, anchor.Y - label.Offset.Y * scale),
            new System.Numerics.Vector2(text.X, text.Y), scale);
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
