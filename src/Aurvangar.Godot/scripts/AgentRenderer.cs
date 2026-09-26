using Aurvangar.ViewCore.Entities;
using Godot;

namespace Aurvangar.Client;

/// <summary>Colonists (VIEW-08): one capsule per agent in its look color (idle grey, working white, trapped yellow,
/// dead red, lying down), with a small cube above the head in the carried item's color. All placement data comes
/// from <see cref="AgentVisuals"/> (ViewCore); this node only moves meshes. Nodes are created on first sight of an
/// agent id and never read sim state themselves.</summary>
public partial class AgentRenderer : Node3D
{
    public const float Radius = 0.22f;
    public const float BodyHeight = 0.85f;
    public const float CarrySize = 0.26f;

    private readonly Dictionary<int, (Node3D Root, MeshInstance3D Body, MeshInstance3D Cargo)> _nodes = new();
    private readonly Dictionary<Color, StandardMaterial3D> _materials = new();
    private readonly CapsuleMesh _capsule = new() { Radius = Radius, Height = BodyHeight };
    private readonly BoxMesh _cube = new() { Size = new Vector3(CarrySize, CarrySize, CarrySize) };

    public void Refresh(IReadOnlyList<AgentVisual> agents)
    {
        foreach (var v in agents)
        {
            if (!_nodes.TryGetValue(v.Id.Value, out var n)) n = Create(v.Id.Value);
            n.Root.Visible = v.Visible;
            if (!v.Visible) continue;
            n.Root.Position = MeshConvert.ToGodot(v.Position);
            bool dead = v.Look == AgentLook.Dead;
            // Standing: capsule center half a body above the feet. Dead: lying on its side on the floor.
            n.Body.Position = dead ? new Vector3(0, Radius, 0) : new Vector3(0, BodyHeight / 2f, 0);
            n.Body.RotationDegrees = dead ? new Vector3(0, 0, 90) : Vector3.Zero;
            n.Body.MaterialOverride = Material(v.Color);
            n.Cargo.Visible = v.Carried.IsValid && !dead;
            if (n.Cargo.Visible) n.Cargo.MaterialOverride = Material(v.CarriedColor);
        }
    }

    private (Node3D, MeshInstance3D, MeshInstance3D) Create(int id)
    {
        var root = new Node3D { Name = $"Agent{id}" };
        var body = new MeshInstance3D { Name = "Body", Mesh = _capsule };
        var cargo = new MeshInstance3D
        {
            Name = "Cargo", Mesh = _cube, Position = new Vector3(0, BodyHeight + CarrySize / 2f + 0.05f, 0),
        };
        root.AddChild(body);
        root.AddChild(cargo);
        AddChild(root);
        var n = (root, body, cargo);
        _nodes[id] = n;
        return n;
    }

    private StandardMaterial3D Material(System.Numerics.Vector4 c)
    {
        var color = new Color(c.X, c.Y, c.Z, c.W);
        if (!_materials.TryGetValue(color, out var m))
        {
            m = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.8f };
            _materials[color] = m;
        }
        return m;
    }
}
