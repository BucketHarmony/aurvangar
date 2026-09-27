using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Blocks;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Entities;

/// <summary>One plan entry as the view draws it: its cell, entry and CON-05 status.</summary>
public readonly record struct PlanGhost(Int3 Cell, PlanEntry Entry, BuildStatus Status);

/// <summary>Plan entries as translucent block-sized ghosts (VIEW-22, M8-T5) in the block's palette colour: Released at
/// <see cref="ReleasedAlpha"/>, Planned lighter at <see cref="PlannedAlpha"/>, and red with a dark outline when the entry is
/// stuck (<see cref="IsStuck"/>, <see cref="InvalidCellStyle"/>, M9-T3). Ghosts above the slice are hidden. Statuses come from one batched sim query
/// (<see cref="BlockPlans.Statuses"/>); the renderer refreshes them every <see cref="RefreshTicks"/> ticks or when an
/// entry changes (<see cref="Signature"/>).</summary>
public static class PlanGhostMesher
{
    public const float ReleasedAlpha = 0.45f;
    public const float PlannedAlpha = 0.25f;
    /// <summary>How far a Planned ghost's colour moves toward white.</summary>
    public const float PlannedLighten = 0.45f;
    /// <summary>Ghosts sit this far inside their cell, so they never z-fight with the terrain faces around them.</summary>
    public const float Inset = 0.04f;
    /// <summary>Statuses change with the world, not with entries; the view recomputes them this often (ticks).</summary>
    public const int RefreshTicks = 10;

    /// <summary>VIEW-22: statuses tinted red: the builders cannot get at the entry.</summary>
    public static bool IsStuck(BuildStatus s) =>
        s is BuildStatus.GivenUp or BuildStatus.NoAccess or BuildStatus.WouldStrand or BuildStatus.NoSupport;

    /// <summary>Every entry at or below <paramref name="sliceY"/> with its status (one scan).</summary>
    public static List<PlanGhost> Ghosts(Simulation sim, int sliceY)
    {
        var list = new List<PlanGhost>();
        foreach (var (c, e, s) in sim.Plans.Statuses(sim, sliceY)) list.Add(new PlanGhost(c, e, s));
        return list;
    }

    public static Vector4 ColorOf(PlanGhost g, BlockColors blocks, EntityColors entities)
    {
        var c = blocks.Get(g.Entry.Block);
        if (g.Entry.State == PlanState.Planned)
            return Vector4.Lerp(c, Vector4.One, PlannedLighten) with { W = PlannedAlpha };
        return IsStuck(g.Status) ? InvalidCellStyle.Fill(entities) : c with { W = ReleasedAlpha };
    }

    public static MeshData Build(IEnumerable<PlanGhost> ghosts, int sliceY, BlockColors blocks, EntityColors entities)
    {
        var mesh = new MeshData();
        foreach (var g in ghosts)
        {
            if (g.Cell.Y > sliceY) continue;
            var c = g.Cell;
            if (g.Entry.State == PlanState.Released && IsStuck(g.Status))
            {
                InvalidCellStyle.Add(mesh, c, -Inset, entities);
                continue;
            }
            if (!g.Entry.Form.IsFull)
            {
                // VIEW-27 (M11-T11): the entry's shape, from 0.25 m sub-cells, inset like the cube.
                ShapeMesher.EmitAlone(mesh, new Vector3(c.X + Inset, c.Y + Inset, c.Z + Inset), 1 - 2 * Inset,
                    ShapePattern.Of(g.Entry.Form), ColorOf(g, blocks, entities));
                continue;
            }
            MeshShapes.AddBox(mesh, new Vector3(c.X + Inset, c.Y + Inset, c.Z + Inset),
                new Vector3(c.X + 1 - Inset, c.Y + 1 - Inset, c.Z + 1 - Inset), ColorOf(g, blocks, entities));
        }
        return mesh;
    }

    /// <summary>Fingerprint of the entries (cells, blocks, states, forms), so the view rebuilds when one changes.</summary>
    public static ulong Signature(Simulation sim)
    {
        var h = StateHasher.Create();
        h.Add(sim.Plans.Count);
        foreach (var (c, e) in sim.Plans.All) { h.Add(c); h.Add((byte)e.Block); h.Add((byte)e.State); h.Add(e.Form.Packed); }
        return h.Value;
    }

    /// <summary>VIEW-22 hover: "Stone wall: waiting for the block below" for the entry in the cell the picked face
    /// looks into; null when there is none or it is above the slice.</summary>
    public static string? HoverText(Simulation sim, PickHit? hover, int sliceY)
    {
        if (hover is not { } h) return null;
        var cell = h.Adjacent;
        if (cell.Y > sliceY || sim.Plans.Get(cell) is not { } e || sim.Plans.StatusOf(sim, cell) is not { } s) return null;
        return $"{Tools.BlockTool.FormLabel(sim.Content, e.Block, e.Form)}: {StatusText(sim, e.Block, s)}";
    }

    /// <summary>Player text of a CON-05 status.</summary>
    public static string StatusText(Simulation sim, BlockId block, BuildStatus s) => s switch
    {
        BuildStatus.Planned => "planned, not released yet",
        BuildStatus.InJob => "being built",
        BuildStatus.GivenUp => "given up: the builders could not do it",
        BuildStatus.BelowFirst => "waiting for the block below",
        BuildStatus.CourseBelow => "waiting for the course below",
        BuildStatus.WaitSupport => "waiting for support",
        BuildStatus.NoSupport => "nothing holds it up",
        BuildStatus.Occupied => "something is in the way",
        BuildStatus.NoAccess => "no dwarf can reach it",
        BuildStatus.WouldStrand => "would wall a dwarf in",
        BuildStatus.NoMaterial => $"no {CostName(sim, block)} in storage",
        BuildStatus.Ready => "ready to build",
        _ => s.ToString(),
    };

    private static string CostName(Simulation sim, BlockId block)
    {
        var (item, cost) = sim.Content.CostOf(block);
        return cost > 0 ? sim.Content.ItemDef(item).Name.ToLowerInvariant() : "material";
    }
}
