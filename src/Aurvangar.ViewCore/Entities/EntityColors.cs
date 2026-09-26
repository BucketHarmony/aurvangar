using System.Numerics;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Meshing;

namespace Aurvangar.ViewCore.Entities;

/// <summary>Colors for agents, item piles, designations and buildings, from palette.json (`agents.*`, `items.*`,
/// `designations.*`, `buildings.*`). A missing key shows magenta so it is obvious on screen.</summary>
public sealed class EntityColors
{
    /// <summary>Alpha of the translucent dig-mark cubes (VIEW-11).</summary>
    public const float DigAlpha = 0.4f;

    private static readonly Vector4 Missing = new(1, 0, 1, 1);
    private readonly Vector4[] _items;
    private readonly Dictionary<string, string> _buildings;

    public Vector4 Idle { get; }
    public Vector4 Working { get; }
    public Vector4 Dead { get; }
    /// <summary>Trapped in deep water (WAT-14): drowning unless the water drops.</summary>
    public Vector4 Trapped { get; }
    public Vector4 Dig { get; }
    public Vector4 Chop { get; }
    /// <summary>Farm tile furrows (VIEW-11, `designations.farm`).</summary>
    public Vector4 Farm { get; }
    public Vector4 Unreachable { get; }
    /// <summary>Blueprint color of any building (VIEW-09, `buildings.blueprint`).</summary>
    public Vector4 Blueprint { get; }

    public EntityColors(ContentDb content)
    {
        var p = content.Palette;
        Idle = Get(p.Agents, "idle");
        Working = Get(p.Agents, "working");
        Dead = Get(p.Agents, "dead");
        Trapped = Get(p.Agents, "trapped");
        Dig = Get(p.Designations, "dig");
        Chop = Get(p.Designations, "chop");
        Farm = Get(p.Designations, "farm");
        Unreachable = Get(p.Designations, "unreachable");
        _buildings = p.Buildings;
        Blueprint = Get(p.Buildings, "blueprint");
        _items = new Vector4[content.Items.Count];
        _items[0] = Missing;
        for (int i = 1; i < content.Items.Count; i++) _items[i] = Get(p.Items, content.Items[i].Id);
    }

    public Vector4 Agent(AgentLook look) => look switch
    {
        AgentLook.Working => Working,
        AgentLook.Trapped => Trapped,
        AgentLook.Dead => Dead,
        _ => Idle,
    };

    /// <summary>Solid color of a building definition (VIEW-09, `buildings.&lt;id&gt;`).</summary>
    public Vector4 Building(string defId) => Get(_buildings, defId);

    public Vector4 Item(ItemId id) => id.Value > 0 && id.Value < _items.Length ? _items[id.Value] : Missing;

    private static Vector4 Get(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var hex) ? BlockColors.ParseHex(hex) : Missing;
}
