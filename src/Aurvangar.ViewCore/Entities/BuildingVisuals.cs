using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Entities;

/// <summary>How a building is drawn (VIEW-09).</summary>
public enum BuildingLook : byte { Blueprint, Site, Complete, Deconstructing }

/// <summary>One building as the view draws it: a box from <see cref="Min"/> with <see cref="Size"/> (world units,
/// already clipped to the slice level and slightly inflated so it covers the building's own solid blocks), its color
/// (alpha &lt; 1 is translucent), a label (empty for none) with a progress bar (<see cref="Progress"/> 0..1, or -1
/// for no bar) above <see cref="LabelAnchor"/>, and the pump's no-water flag.</summary>
public readonly record struct BuildingVisual(BuildingId Id, Vector3 Min, Vector3 Size, BuildingLook Look, Vector4 Color,
    string Label, float Progress, bool NoWater, Vector3 LabelAnchor);

/// <summary>Building render data (VIEW-09). Blueprint = translucent blueprint color with the materials still to come;
/// site = semi-opaque building color with the delivery or build progress; complete = solid palette color; being torn
/// down = solid with the teardown progress. A complete pump flagged <c>NoWater</c> (BLD-13) gets the no-water icon.
/// Pure read of sim state.</summary>
public static class BuildingVisuals
{
    public const float BlueprintAlpha = 0.35f;
    public const float SiteAlpha = 0.6f;

    /// <summary>Boxes grow by this much on every side, so a complete building's box hides its BuildingSolid blocks
    /// (no z-fighting) and the palette color shows.</summary>
    public const float Inflate = 0.02f;

    /// <summary>The label sits this far above the top of the box.</summary>
    public const float LabelLift = 0.35f;

    public static BuildingLook LookOf(Building b) => b.State switch
    {
        BuildingState.Blueprint => BuildingLook.Blueprint,
        BuildingState.UnderConstruction => BuildingLook.Site,
        BuildingState.Deconstructing => BuildingLook.Deconstructing,
        _ => BuildingLook.Complete,
    };

    /// <summary>Every building whose bottom is at or below the slice level, in ascending id order (VIEW-04: the box is
    /// cut at the slice level).</summary>
    public static List<BuildingVisual> Build(Simulation sim, int sliceY, EntityColors colors)
    {
        var list = new List<BuildingVisual>();
        foreach (var b in sim.Buildings.All)
        {
            var (min, max) = Bounds(b);
            if (min.Y > sliceY) continue;
            int top = Math.Min(max.Y, sliceY);
            var lo = new Vector3(min.X - Inflate, min.Y - Inflate, min.Z - Inflate);
            var size = new Vector3(max.X - min.X + 1, top - min.Y + 1, max.Z - min.Z + 1) + new Vector3(2 * Inflate);
            var look = LookOf(b);
            var anchor = new Vector3(lo.X + size.X / 2f, lo.Y + size.Y + LabelLift, lo.Z + size.Z / 2f);
            var (label, progress) = LabelOf(sim, b);
            bool noWater = b.State == BuildingState.Complete && b.Def.Producer is not null && b.NoWater;
            list.Add(new BuildingVisual(b.Id, lo, size, look, ColorOf(b, look, colors), label, progress, noWater, anchor));
        }
        return list;
    }

    /// <summary>Label and progress (0..1, -1 for no bar). A blueprint or site still waiting for materials lists them as
    /// delivered/needed with the delivered fraction; a site being built shows the build progress; a teardown shows its
    /// progress; a complete building has no label.</summary>
    public static (string Label, float Progress) LabelOf(Simulation sim, Building b)
    {
        switch (b.State)
        {
            case BuildingState.Blueprint:
            case BuildingState.UnderConstruction:
            {
                int need = 0, have = 0;
                var parts = new List<string>();
                foreach (var (item, count) in Construction.Cost(sim, b.Def))
                {
                    int got = Math.Min(b.Delivered.TryGetValue(item.Value, out var n) ? n : 0, count);
                    need += count; have += got;
                    parts.Add($"{sim.Content.ItemDef(item).Name.ToLowerInvariant()} {got}/{count}");
                }
                if (have < need) return ($"{b.Def.Name}: {string.Join(", ", parts)}", have / (float)need);
                return ($"Building {b.Def.Name} {Percent(b.Progress, b.Def.BuildTicks)}%", Fraction(b.Progress, b.Def.BuildTicks));
            }
            case BuildingState.Deconstructing:
            {
                int total = Construction.DeconstructTicks(b.Def);
                return ($"Tearing down {Percent(b.Progress, total)}%", Fraction(b.Progress, total));
            }
            default:
                return ("", -1f);
        }
    }

    public static Vector4 ColorOf(Building b, BuildingLook look, EntityColors colors) => look switch
    {
        BuildingLook.Blueprint => colors.Blueprint with { W = BlueprintAlpha },
        BuildingLook.Site => colors.Building(b.Def.Id) with { W = SiteAlpha },
        _ => colors.Building(b.Def.Id),
    };

    /// <summary>Inclusive footprint box, min first (the footprint rotates about the origin, BLD-01).</summary>
    public static (Int3 Min, Int3 Max) Bounds(Building b)
    {
        var min = new Int3(int.MaxValue, int.MaxValue, int.MaxValue);
        var max = new Int3(int.MinValue, int.MinValue, int.MinValue);
        foreach (var c in b.FootprintCells())
        {
            min = new Int3(Math.Min(min.X, c.X), Math.Min(min.Y, c.Y), Math.Min(min.Z, c.Z));
            max = new Int3(Math.Max(max.X, c.X), Math.Max(max.Y, c.Y), Math.Max(max.Z, c.Z));
        }
        return (min, max);
    }

    private static float Fraction(int value, int total) => total <= 0 ? 1f : Math.Clamp(value / (float)total, 0f, 1f);

    private static int Percent(int value, int total) => (int)(Fraction(value, total) * 100f);
}
