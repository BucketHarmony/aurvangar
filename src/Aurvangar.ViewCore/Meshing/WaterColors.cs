using System.Numerics;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Water;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Water surface colors from palette.json (`water.shallow`, `water.deep`, `water.alpha`), VIEW-07.
/// Semi-transparent blue that darkens with water depth (ADR-017).</summary>
public sealed class WaterColors
{
    /// <summary>Water depth (in cells) at which the color reaches the deep color.</summary>
    public const int DeepCells = 3;

    private static readonly Lazy<WaterColors> _default = new(() => new WaterColors(ContentDb.LoadEmbedded()));

    /// <summary>Colors from the embedded palette. Used when a caller passes no colors.</summary>
    public static WaterColors Default => _default.Value;

    public Vector4 Shallow { get; }
    public Vector4 Deep { get; }

    public WaterColors(ContentDb content)
    {
        var p = content.Palette.Water;
        Shallow = BlockColors.ParseHex(p.Shallow) with { W = p.Alpha };
        Deep = BlockColors.ParseHex(p.Deep) with { W = p.Alpha };
    }

    /// <summary>Color for a water column of the given depth in level units (Full per cell); clamped at DeepCells.</summary>
    public Vector4 ForDepth(int depthUnits)
    {
        float t = Math.Clamp(depthUnits / (float)(DeepCells * WaterGrid.Full), 0f, 1f);
        return Vector4.Lerp(Shallow, Deep, t);
    }
}
