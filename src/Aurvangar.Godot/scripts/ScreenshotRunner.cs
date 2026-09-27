using Aurvangar.ViewCore.Screenshots;
using Godot;

namespace Aurvangar.Client;

/// <summary>Screenshot harness (VIEW-20), root of scenes/Screenshot.tscn with Main.tscn instanced as its "Main"
/// child. Reads the user args (<see cref="ScreenshotArgs"/>), pauses the real-time loop, runs the sim for
/// <c>--ticks</c> ticks without rendering waits (with the optional <c>--script</c>; a timed one enqueues each command at its tick), then for each preset (<see cref="ScreenshotPresets"/>) sets the
/// slice and camera, remeshes everything (agents, piles and designations included), waits for drawn frames, saves <c>&lt;out&gt;/&lt;preset&gt;.png</c> and
/// finally quits. Exit code 0 on success, 1 on a failed save, 2 on bad arguments.</summary>
public partial class ScreenshotRunner : Node
{
    /// <summary>Frames drawn after a camera/slice change before capturing (lets meshes and shadows settle).</summary>
    public const int SettleFrames = 3;

    public override void _Ready() => _ = RunAsync();

    private async System.Threading.Tasks.Task RunAsync()
    {
        ScreenshotArgs args;
        try
        {
            args = ScreenshotArgs.Parse(OS.GetCmdlineUserArgs());
        }
        catch (System.ArgumentException e)
        {
            GD.PrintErr(e.Message);
            GetTree().Quit(2);
            return;
        }

        var root = GetNode<GameRoot>("Main");
        root.SpeedIndex = 0;          // the harness drives ticks itself
        root.PickingEnabled = false;  // no hover marker in shots
        string outDir = System.IO.Path.GetFullPath(args.OutDir);
        System.IO.Directory.CreateDirectory(outDir);

        GD.Print($"screenshot: seed {args.Seed}, script {args.Script}, running {args.Ticks} ticks");
        root.RunScriptNow(args.Script, args.Ticks);
        if (ScreenshotScripts.GhostPick(args.Script, root.Sim) is { } ghost)
        {
            root.PickOverride = ghost;          // M5-T6: show the build ghost and its tooltip in the shots
            root.SetTool(Aurvangar.ViewCore.Tools.ToolKind.Build);
        }
        if (args.Script == "blocks" && Aurvangar.ViewCore.Scripts.BlocksScript.GhostDrag(root.Sim) is { } drag)
            root.ShowBlockDrag(drag.From, drag.To, Aurvangar.Sim.World.BlockId.Masonry, Aurvangar.Sim.Blocks.BuildShape.Wall);   // M8-T5

        int failures = 0;
        foreach (var name in args.Shots)
        {
            root.ApplyShot(ScreenshotPresets.For(name, root.Sim));
            for (int i = 0; i < SettleFrames; i++)
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            string path = System.IO.Path.Combine(outDir, name + ".png");
            var image = GetViewport().GetTexture().GetImage();
            var err = image.SavePng(path);
            if (err == Error.Ok) GD.Print($"screenshot: wrote {path}");
            else { GD.PrintErr($"screenshot: failed to save {path}: {err}"); failures++; }
        }
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
}
