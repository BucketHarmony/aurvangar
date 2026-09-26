using Aurvangar.Sim;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Screenshots;

/// <summary>Optional command scripts for the screenshot harness (<c>--script</c>, VIEW-20), so shots can show
/// colonists at work. Commands are computed from the world, like the presets (ADR-020).
/// <list type="bullet">
/// <item><c>none</c>: no commands (the default).</item>
/// <item><c>digchop</c>: dig a <see cref="PitDepth"/>-deep pit of <see cref="PitLength"/>×<see cref="PitWidth"/> cells starting
/// <see cref="PitGap"/> cells east of the hub, and chop every tree within <see cref="ChopRadius"/> cells of the hub
/// center (Chebyshev on X/Z).</item>
/// </list></summary>
public static class ScreenshotScripts
{
    public static readonly IReadOnlyList<string> Names = new[] { "none", "digchop" };

    public const int PitGap = 3;
    public const int PitLength = 14;
    public const int PitWidth = 9;
    public const int PitDepth = 2;
    public const int ChopRadius = 24;

    public static IReadOnlyList<ICommand> For(string name, Simulation sim)
    {
        switch (name)
        {
            case "none":
                return Array.Empty<ICommand>();
            case "digchop":
            {
                var hub = sim.Buildings.All.FirstOrDefault()
                    ?? throw new InvalidOperationException("digchop script needs the pre-placed hub");
                int maxX = int.MinValue;
                foreach (var c in hub.FootprintCells()) maxX = Math.Max(maxX, c.X);
                var focus = ScreenshotPresets.HubFocus(sim);
                int cx = (int)focus.X, cz = (int)focus.Z, floor = hub.Origin.Y - 1;
                int x0 = maxX + 1 + PitGap, half = PitWidth / 2;
                return new ICommand[]
                {
                    new DesignateDig(new Int3(x0, floor - PitDepth + 1, cz - half), new Int3(x0 + PitLength - 1, floor, cz + half)),
                    new DesignateChop(cx - ChopRadius, cz - ChopRadius, cx + ChopRadius, cz + ChopRadius),
                };
            }
            default:
                throw new ArgumentException($"unknown screenshot script '{name}' (known: {string.Join(",", Names)})");
        }
    }
}
