using System.Globalization;
using System.Numerics;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.World;

namespace Aurvangar.ViewCore.Meshing;

/// <summary>Block color lookup built from palette.json.</summary>
public sealed class BlockColors
{
    private readonly Vector4[] _colors = new Vector4[256];

    public float CutDarken { get; }

    public BlockColors(ContentDb content)
    {
        CutDarken = content.Palette.CutFaceDarken;
        foreach (BlockId id in Enum.GetValues<BlockId>())
        {
            _colors[(int)id] = content.Palette.Blocks.TryGetValue(id.ToString(), out var hex) ? ParseHex(hex) : new Vector4(1, 0, 1, 1);
        }
    }

    public Vector4 Get(BlockId id) => _colors[(int)id];

    public Vector4 GetCut(BlockId id)
    {
        var c = _colors[(int)id];
        return new Vector4(c.X * CutDarken, c.Y * CutDarken, c.Z * CutDarken, c.W);
    }

    public static Vector4 ParseHex(string hex)
    {
        var s = hex.TrimStart('#');
        int r = int.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int g = int.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int b = int.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
    }
}
