using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Entities;

/// <summary>How an agent is drawn (VIEW-08): idle grey, working white, dead red; trapped in deep water (WAT-14)
/// has its own warning color.</summary>
public enum AgentLook : byte { Idle, Working, Trapped, Dead }

/// <summary>One agent as the view draws it. <see cref="Position"/> is the feet point (bottom center of the cell the
/// agent stands in, lerped toward the next cell). <see cref="Carried"/> is invalid when the hands are empty.</summary>
public readonly record struct AgentVisual(AgentId Id, Vector3 Position, AgentLook Look, Vector4 Color, ItemId Carried,
    Vector4 CarriedColor, bool Visible);

/// <summary>Agent render data (VIEW-08). Pure read of sim state; the Godot AgentRenderer only places meshes.</summary>
public static class AgentVisuals
{
    public static AgentLook LookOf(Agent a) => a.State switch
    {
        AgentState.Dead => AgentLook.Dead,
        AgentState.Trapped => AgentLook.Trapped,
        AgentState.Working => AgentLook.Working,
        _ => AgentLook.Idle,
    };

    /// <summary>Feet position: <c>Cell</c> lerped toward <c>NextCell</c> by <c>MoveProgress / MoveTotal</c>.
    /// <paramref name="tickFraction"/> (0..1, the part of the next tick that has already elapsed in wall time)
    /// smooths the motion between ticks; it is only applied while a step is in progress and never passes the
    /// next cell.</summary>
    public static Vector3 Position(Agent a, float tickFraction = 0f)
    {
        var from = Feet(a.Cell);
        if (a.MoveTotal <= 0 || a.NextCell == a.Cell) return from;
        float t = Math.Clamp((a.MoveProgress + Math.Clamp(tickFraction, 0f, 1f)) / a.MoveTotal, 0f, 1f);
        return Vector3.Lerp(from, Feet(a.NextCell), t);
    }

    /// <summary>VIEW-04: an agent is hidden when the cell it stands in is above the slice level (the same rule as
    /// plants: its floor is cut away, so the agent would float).</summary>
    public static bool VisibleAt(Agent a, int sliceY) => a.Cell.Y <= sliceY;

    /// <summary>Every agent in ascending id order, dead ones included (they lie where they fell until a save/load
    /// drops them, SAV-06).</summary>
    public static List<AgentVisual> Build(Simulation sim, int sliceY, EntityColors colors, float tickFraction = 0f)
    {
        var list = new List<AgentVisual>(sim.Agents.Count);
        foreach (var a in sim.Agents.All)
        {
            var look = LookOf(a);
            var carried = a.Carried.IsEmpty ? default : a.Carried.Item;
            list.Add(new AgentVisual(a.Id, Position(a, tickFraction), look, colors.Agent(look), carried,
                colors.Item(carried), VisibleAt(a, sliceY)));
        }
        return list;
    }

    private static Vector3 Feet(Int3 c) => new(c.X + 0.5f, c.Y, c.Z + 0.5f);
}
