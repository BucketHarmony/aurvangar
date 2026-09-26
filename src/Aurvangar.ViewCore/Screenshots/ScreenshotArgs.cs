using System.Globalization;

namespace Aurvangar.ViewCore.Screenshots;

/// <summary>Command-line user args of the screenshot harness (VIEW-20), i.e. what follows <c>--</c> on the Godot
/// command line: <c>--seed 1 --ticks 1200 --shots overview,river,hub,slice --out artifacts/screens</c>.
/// Every key is optional; bad input throws <see cref="ArgumentException"/> naming the key.</summary>
public sealed record ScreenshotArgs(ulong Seed, int Ticks, IReadOnlyList<string> Shots, string OutDir,
    string Script = ScreenshotArgs.DefaultScript)
{
    public const ulong DefaultSeed = 1;
    public const int DefaultTicks = 1200;
    public const string DefaultOutDir = "artifacts/screens";
    public const string DefaultScript = "none";

    public static ScreenshotArgs Parse(IReadOnlyList<string> args)
    {
        ulong seed = DefaultSeed;
        int ticks = DefaultTicks;
        IReadOnlyList<string> shots = ScreenshotPresets.DefaultShots;
        string outDir = DefaultOutDir;
        string script = DefaultScript;

        for (int i = 0; i < args.Count; i++)
        {
            string key = args[i];
            if (i + 1 >= args.Count) throw new ArgumentException($"screenshot args: {key} needs a value");
            string value = args[++i];
            switch (key)
            {
                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                        throw new ArgumentException($"screenshot args: --seed '{value}' is not an unsigned integer");
                    break;
                case "--ticks":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ticks))
                        throw new ArgumentException($"screenshot args: --ticks '{value}' is not a non-negative integer");
                    break;
                case "--shots":
                    shots = ParseShots(value);
                    break;
                case "--out":
                    if (value.Length == 0) throw new ArgumentException("screenshot args: --out is empty");
                    outDir = value;
                    break;
                case "--script":
                    if (!ScreenshotScripts.Names.Contains(value))
                        throw new ArgumentException(
                            $"screenshot args: unknown script '{value}' (known: {string.Join(",", ScreenshotScripts.Names)})");
                    script = value;
                    break;
                default:
                    throw new ArgumentException($"screenshot args: unknown key '{key}'");
            }
        }
        return new ScreenshotArgs(seed, ticks, shots, outDir, script);
    }

    /// <summary>PNG path for a preset: <c>&lt;out&gt;/&lt;preset&gt;.png</c>.</summary>
    public string OutputPath(string preset) => Path.Combine(OutDir, preset + ".png");

    private static List<string> ParseShots(string value)
    {
        var list = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (list.Count == 0) throw new ArgumentException("screenshot args: --shots is empty");
        foreach (var s in list)
            if (!ScreenshotPresets.Names.Contains(s))
                throw new ArgumentException(
                    $"screenshot args: unknown shot '{s}' (known: {string.Join(",", ScreenshotPresets.Names)})");
        return list;
    }
}
