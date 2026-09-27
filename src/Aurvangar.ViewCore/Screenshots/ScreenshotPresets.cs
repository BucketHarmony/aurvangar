using System.Numerics;
using Aurvangar.Sim;
using Aurvangar.Sim.Core;
using Aurvangar.ViewCore.Camera;
using Aurvangar.ViewCore.Scripts;

namespace Aurvangar.ViewCore.Screenshots;

/// <summary>One camera shot of the screenshot harness: an <see cref="OrbitRig"/> view plus a slice level.</summary>
public sealed record CameraShot(string Name, Vector3 Focus, float Yaw, float Pitch, float Distance, int SliceY)
{
    /// <summary>Puts the rig at this view. The focus height becomes the rig's base height, so the rig's per-frame
    /// <see cref="OrbitRig.Update"/> keeps the view (presets never focus above <c>SliceY + 1</c>).</summary>
    public void ApplyTo(OrbitRig rig) => rig.SetView(Focus, Yaw, Pitch, Distance);
}

/// <summary>Named camera presets (docs/testing.md "Screenshot presets", VIEW-20), computed from the world so they
/// follow the hub and river wherever terrain generation put them (ADR-020).
/// <list type="bullet">
/// <item><c>overview</c>: whole map, 45 degree pitch, max zoom, centered.</item>
/// <item><c>river</c>: hub-to-river close-up, focused halfway between the hub and the nearest river water along Z,
/// looking along -X so both ends sit side by side.</item>
/// <item><c>hub</c>: colony close-up on the hub footprint center.</item>
/// <item><c>slice</c>: slice at y=20 over the hill peak (the tallest column).</item>
/// <item><c>farm</c> (M6-T5): close-up on the center of the farm tiles (the hub when there are none).</item>
/// <item><c>tunnel</c> (M7-T7): the survival session's hill tunnel and breach (<see cref="SurvivalScript"/>, fixed
/// seed-1 cells), sliced at the tunnel's headroom so the hill above it is cut away.</item>
/// <item><c>reservoir</c> (M7-T7): the survival session's levee reservoir and the farm field beside it, with the river
/// past its mouth.</item>
/// <item><c>blocks</c> (M8-T5): the <see cref="BlocksScript"/> site (walls, planned box, tool ghost) and the top bar's
/// plan line; the hub when there is no site.</item>
/// <item><c>materials</c> (M9-T4): the <see cref="MaterialsScript"/> row, one sample of every construction block.</item>
/// <item><c>paint</c> (M9-T1): the <see cref="PaintScript"/> site (single blocks painted along drags, a drag held).</item>
/// <item><c>wall</c> (M10-T3): the <c>wall</c> script's vertical drag held up a wall face beside the painted L.</item>
/// <item><c>stairs</c> (M11-T8): the <see cref="StairScript"/> stair head and the pit beside it, sliced one level under
/// the stair's top cell so the upper steps show as a slot in the cut; the dig tool in stair mode hovers a waiting pit
/// cell.</item>
/// <item><c>shapes</c> (M11-T11): the <see cref="ShapesScript"/> build of fine shapes (stair, pillars, slabs) with a
/// stair drag of the block tool held.</item>
/// <item><c>monument</c> (M8-T6): the <see cref="MonumentScript"/> tower and courtyard, with the hill quarry behind.</item>
/// </list></summary>
public static class ScreenshotPresets
{
    public static readonly IReadOnlyList<string> Names =
        new[] { "overview", "river", "hub", "slice", "farm", "tunnel", "reservoir", "blocks", "monument", "paint", "materials", "wall", "stairs", "shapes", "workshop" };

    /// <summary>The shots taken when <c>--shots</c> is not given (the gate set; <c>farm</c> is opt-in, M6-T5).</summary>
    public static readonly IReadOnlyList<string> DefaultShots = new[] { "overview", "river", "hub", "slice" };

    public const int SliceLevel = 20;

    /// <summary>Camera yaws of the survival presets (M7-T7), chosen by looking at the shots.</summary>
    public const float TunnelYaw = 90f;
    public const float ReservoirYaw = 0f;
    public const float BlocksYaw = 30f;
    public const float MonumentYaw = 30f;
    public const float MaterialsYaw = 0f;
    public const float StairsYaw = 180f;
    public const float WallYaw = 130f;
    public const float ShapesYaw = 330f;
    public const float WorkshopYaw = 45f;

    public static CameraShot For(string name, Simulation sim)
    {
        var w = sim.World;
        int top = w.SizeY - 1;
        switch (name)
        {
            case "overview":
            {
                int cx = w.SizeX / 2, cz = w.SizeZ / 2;
                var focus = new Vector3(cx, SurfaceY(sim, cx, cz) + 1, cz);
                return new CameraShot(name, focus, 45f, 45f, OrbitRig.MaxDistance, top);
            }
            case "hub":
                return new CameraShot(name, HubFocus(sim), 45f, 50f, 32f, top);
            case "river":
            {
                var hub = HubFocus(sim);
                var water = NearestWaterAlongZ(sim, (int)hub.X, (int)hub.Z);
                if (water is not { } wc) return new CameraShot(name, hub, 90f, 40f, 40f, top);
                float waterZ = wc.Z + 0.5f;
                float span = MathF.Abs(hub.Z - wc.Z);
                var focus = new Vector3(hub.X, MathF.Min(hub.Y, wc.Y + 1), (hub.Z + waterZ) / 2f);
                float distance = Math.Clamp(MathF.Max(40f, span * 1.5f), OrbitRig.MinDistance, OrbitRig.MaxDistance);
                return new CameraShot(name, focus, 90f, 40f, distance, top);
            }
            case "farm":
                return new CameraShot(name, FarmFocus(sim) ?? HubFocus(sim), 45f, 50f, 14f, top);
            case "tunnel":
            {
                var a = SurvivalScript.TunnelA;
                var b = SurvivalScript.TunnelB;
                int slice = Math.Min(b.Y, top);
                var focus = new Vector3((a.X + b.X + 1) / 2f, a.Y, (a.Z + SurvivalScript.BreachA.Z + 1) / 2f);
                return new CameraShot(name, focus, TunnelYaw, 55f, 18f, slice);
            }
            case "reservoir":
            {
                var r = SurvivalScript.ReservoirA;
                var focus = new Vector3((SurvivalScript.Farm.X0 + r.X + 1) / 2f, r.Y + 1,
                    (r.Z + SurvivalScript.ReservoirMouth.Z + 1) / 2f);
                return new CameraShot(name, focus, ReservoirYaw, 60f, 20f, top);
            }
            case "blocks":
            {
                if (BlocksScript.Site(sim) is not { } s) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                var focus = new Vector3(s.X + BlocksScript.SiteW / 2f, s.Y + 1, s.Z + BlocksScript.SiteD / 2f);
                return new CameraShot(name, focus, BlocksYaw, 50f, 18f, top);
            }
            case "paint":
            {
                if (PaintScript.Site(sim) is not { } s) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                var focus = new Vector3(s.X + BlocksScript.SiteW / 2f, s.Y + 1, s.Z + BlocksScript.SiteD / 2f);
                return new CameraShot(name, focus, BlocksYaw, 50f, 12f, top);
            }
            case "wall":
            {
                if (PaintScript.Site(sim) is not { } s) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                var focus = new Vector3(s.X + PaintScript.WallDx, s.Y + PaintScript.WallHeight / 2f,
                    s.Z + PaintScript.WallZ0 - PaintScript.WallWidth / 2f + 1);
                return new CameraShot(name, focus, WallYaw, 30f, 14f, top);
            }
            case "materials":
            {
                if (MaterialsScript.Site(sim) is not { } s) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                int n = MaterialsScript.Blocks(sim).Count * MaterialsScript.SampleW;
                var focus = new Vector3(s.X + n / 2f, s.Y + 2, s.Z + MaterialsScript.RowDz + 0.5f);
                return new CameraShot(name, focus, MaterialsYaw, 35f, 12f, top);
            }
            case "monument":
            {
                var a = MonumentScript.TowerA;
                var b = MonumentScript.TowerB;
                var focus = new Vector3((a.X + b.X + 1) / 2f, a.Y + MonumentScript.TowerHeight / 2f, (a.Z + MonumentScript.CourtyardFrontZ + 1) / 2f);
                return new CameraShot(name, focus, MonumentYaw, 40f, 24f, top);
            }
            case "stairs":
            {
                // Over the stair head and the pit beside it, sliced one level under the stair's top cell (VIEW-04) so the
                // trees and ground around are cut away and the upper steps show as a slot in the cut.
                if (StairScript.Anchor(sim) is not { } a) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                int slice = Math.Min(a.Y - 1, top);
                var focus = new Vector3(a.X - 0.5f, slice - 1, a.Z - 3.5f);
                return new CameraShot(name, focus, StairsYaw, 55f, 16f, slice);
            }
            case "shapes":
            {
                if (ShapesScript.Site(sim) is not { } s) return new CameraShot(name, HubFocus(sim), 45f, 50f, 26f, top);
                var focus = new Vector3(s.X + BlocksScript.SiteW / 2f, s.Y + 1, s.Z + BlocksScript.SiteD / 2f);
                return new CameraShot(name, focus, ShapesYaw, 35f, 11f, top);
            }
            case "workshop":
            {
                // M11-T6: the hall and the two workshops of the workshop script, from the south-east.
                var hub = HubFocus(sim);
                var ws = sim.Buildings.All.Where(b => b.Def.Workshop is not null).ToList();
                if (ws.Count == 0) return new CameraShot(name, hub, 45f, 50f, 32f, top);
                float x = hub.X, z = hub.Z;
                foreach (var b in ws) { x += b.Origin.X + 1f; z += b.Origin.Z + 1f; }
                var focus = new Vector3(x / (ws.Count + 1), hub.Y, z / (ws.Count + 1));
                return new CameraShot(name, focus, WorkshopYaw, 45f, 20f, top);
            }
            case "slice":
            {
                var (px, pz) = PeakColumn(sim);
                int slice = Math.Min(SliceLevel, top);
                return new CameraShot(name, new Vector3(px + 0.5f, slice + 1, pz + 0.5f), 45f, 55f, 50f, slice);
            }
            default:
                throw new ArgumentException($"unknown screenshot preset '{name}' (known: {string.Join(",", Names)})");
        }
    }

    /// <summary>Center of the farm tiles on X/Z, one cell above their mean top; null when there are none.</summary>
    public static Vector3? FarmFocus(Simulation sim)
    {
        if (sim.Farms.Count == 0) return null;
        float sx = 0, sy = 0, sz = 0;
        foreach (var t in sim.Farms.All) { sx += t.Cell.X + 0.5f; sy += t.Cell.Y + 1; sz += t.Cell.Z + 0.5f; }
        int n = sim.Farms.Count;
        return new Vector3(sx / n, sy / n, sz / n);
    }

    /// <summary>Center of the first building's footprint (the pre-placed hub) at its origin height; the world center
    /// when there is no building.</summary>
    public static Vector3 HubFocus(Simulation sim)
    {
        var hub = sim.Buildings.All.FirstOrDefault();
        if (hub == null)
        {
            int cx = sim.World.SizeX / 2, cz = sim.World.SizeZ / 2;
            return new Vector3(cx, SurfaceY(sim, cx, cz) + 1, cz);
        }
        long sx = 0, sz = 0;
        int n = 0;
        foreach (var c in hub.FootprintCells()) { sx += c.X; sz += c.Z; n++; }
        return new Vector3((float)sx / n + 0.5f, hub.Origin.Y, (float)sz / n + 0.5f);
    }

    /// <summary>The topmost wet cell of the nearest column with water on the line <c>x</c>, searching outward from
    /// <paramref name="z"/> (ties: smaller z first). Null if the line is dry.</summary>
    public static Int3? NearestWaterAlongZ(Simulation sim, int x, int z)
    {
        var w = sim.World;
        for (int d = 0; d < w.SizeZ; d++)
        {
            foreach (int zz in d == 0 ? new[] { z } : new[] { z - d, z + d })
            {
                if (zz < 0 || zz >= w.SizeZ) continue;
                for (int y = w.SizeY - 1; y >= 0; y--)
                {
                    var c = new Int3(x, y, zz);
                    if (sim.Water.GetLevel(c) > 0) return c;
                }
            }
        }
        return null;
    }

    /// <summary>Column with the highest solid cell (ties: first in z-then-x order).</summary>
    public static (int x, int z) PeakColumn(Simulation sim)
    {
        var w = sim.World;
        int best = -1, bx = 0, bz = 0;
        for (int z = 0; z < w.SizeZ; z++)
            for (int x = 0; x < w.SizeX; x++)
            {
                int y = SurfaceY(sim, x, z);
                if (y > best) { best = y; bx = x; bz = z; }
            }
        return (bx, bz);
    }

    /// <summary>Highest solid y in a column, or -1.</summary>
    public static int SurfaceY(Simulation sim, int x, int z)
    {
        for (int y = sim.World.SizeY - 1; y >= 0; y--)
            if (sim.World.IsSolid(x, y, z)) return y;
        return -1;
    }
}
