using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Farming;
using Aurvangar.ViewCore.Meshing;
using Aurvangar.ViewCore.Picking;

namespace Aurvangar.ViewCore.Entities;

/// <summary>Crops on farm tiles (ECO-12, M6-T5). An Empty tile shows nothing here (its furrows are the farm overlay in
/// <see cref="DesignationMesher"/>). A Growing crop is a 2×2 cluster of leafy stalks whose height grows in
/// <see cref="Stages"/> steps from <see cref="MinHeight"/> to <see cref="MaxHeight"/>, green on a moist tile and straw
/// colored on a dry one (it makes no progress there and withers after a day, ECO-13). A Mature crop is full height with
/// a potato-colored cap on each stalk. One opaque mesh in world coordinates; tiles above the slice are hidden. There is
/// no farm event, so the renderer polls <see cref="Signature"/>.</summary>
public static class CropMesher
{
    /// <summary>Growth steps drawn between planting and maturity (the mesh changes only at these).</summary>
    public const int Stages = 8;
    public const float MinHeight = 0.12f;
    public const float MaxHeight = 0.6f;
    public const float StalkHalfWidth = 0.1f;
    /// <summary>Stalk centers sit this far from the tile center on X and Z.</summary>
    public const float StalkOffset = 0.25f;
    public const float CapHalfWidth = 0.13f;
    public const float CapHeight = 0.12f;
    /// <summary>Lift off the tile's top face so the stalk bottoms do not z-fight with it.</summary>
    public const float Lift = 0.005f;

    /// <summary>0..<see cref="Stages"/>: 0 just planted, <see cref="Stages"/> mature.</summary>
    public static int Stage(FarmTile t) => t.State switch
    {
        CropState.Mature => Stages,
        CropState.Growing => Math.Clamp(t.Progress * Stages / FarmSystem.MatureTicks, 0, Stages - 1),
        _ => 0,
    };

    public static float Height(int stage) => MinHeight + (MaxHeight - MinHeight) * stage / Stages;

    public static MeshData Build(Simulation sim, int sliceY, PlantColors colors)
    {
        var mesh = new MeshData();
        foreach (var t in sim.Farms.All)
        {
            if (t.State == CropState.Empty || t.Cell.Y > sliceY) continue;
            float h = Height(Stage(t));
            bool mature = t.State == CropState.Mature;
            var leaf = mature || sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z) ? colors.Crop : colors.CropDry;
            float y0 = t.Cell.Y + 1 + Lift;
            float cx = t.Cell.X + 0.5f, cz = t.Cell.Z + 0.5f;
            foreach (var (dx, dz) in Offsets)
            {
                float sx = cx + dx, sz = cz + dz;
                float top = mature ? y0 + h - CapHeight : y0 + h;
                MeshShapes.AddBox(mesh, new Vector3(sx - StalkHalfWidth, y0, sz - StalkHalfWidth),
                    new Vector3(sx + StalkHalfWidth, top, sz + StalkHalfWidth), leaf);
                if (mature)
                    MeshShapes.AddBox(mesh, new Vector3(sx - CapHalfWidth, top, sz - CapHalfWidth),
                        new Vector3(sx + CapHalfWidth, y0 + h, sz + CapHalfWidth), colors.Potato);
            }
        }
        return mesh;
    }

    /// <summary>Fingerprint of what <see cref="Build"/> draws besides the slice: per tile its cell, state, stage and
    /// moisture.</summary>
    public static ulong Signature(Simulation sim)
    {
        var h = StateHasher.Create();
        foreach (var t in sim.Farms.All)
        {
            h.Add(t.Cell);
            h.Add((byte)t.State);
            h.Add(Stage(t));
            h.Add(sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z));
        }
        return h.Value;
    }

    /// <summary>Hover text for a farm tile under the mouse (the Farmland block, or the air above it), or null.</summary>
    public static string? Label(Simulation sim, PickHit hit)
    {
        var t = sim.Farms.Get(hit.Cell) ?? sim.Farms.Get(hit.Cell - Int3.Up);
        if (t is null) return null;
        switch (t.State)
        {
            case CropState.Empty:
                return "Farm tile: waiting for planting";
            case CropState.Mature:
                return "Potatoes ready to harvest";
            default:
                int pct = t.Progress * 100 / FarmSystem.MatureTicks;
                return sim.Moisture.IsMoist(t.Cell.X, t.Cell.Z)
                    ? $"Potatoes {pct}% (moist)"
                    : $"Potatoes {pct}% (dry, withers in {FarmSystem.WitherTicks - t.DryTicks} ticks)";
        }
    }

    private static readonly (float Dx, float Dz)[] Offsets =
    {
        (-StalkOffset, -StalkOffset), (StalkOffset, -StalkOffset), (-StalkOffset, StalkOffset), (StalkOffset, StalkOffset),
    };
}
