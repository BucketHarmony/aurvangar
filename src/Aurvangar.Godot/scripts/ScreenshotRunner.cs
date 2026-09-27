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
        if (args.Ghost is { } ghostDef && root.Sim.Content.FindBuilding(ghostDef) is { } gd
            && ScreenshotScripts.GhostPickFor(root.Sim, gd) is { } ghostPick)
        {
            root.PickOverride = ghostPick;      // M11-T1: --ghost levee shows that building's ghost (no entrance tile)
            root.ChooseBuilding(gd.Id);
        }
        if (args.Script == "blocks" && Aurvangar.ViewCore.Scripts.BlocksScript.GhostDrag(root.Sim) is { } drag)
            root.ShowBlockPaint(drag.From, new[] { drag.To.Adjacent }, Aurvangar.Sim.World.BlockId.Masonry);   // M8-T5, M9-T1
        if (args.Script == "paint" && Aurvangar.ViewCore.Scripts.PaintScript.LiveDrag(root.Sim) is { } paint)
            root.ShowBlockPaint(paint.Start, paint.Path, Aurvangar.Sim.World.BlockId.Masonry);   // M9-T1
        if (args.Script == "wall" && Aurvangar.ViewCore.Scripts.PaintScript.WallDrag(root.Sim) is { } wall)
            root.ShowBlockPaint(wall.Start, wall.Path, Aurvangar.Sim.World.BlockId.Planks);   // M10-T3
        if (args.Script == "shapes" && Aurvangar.ViewCore.Scripts.ShapesScript.GhostDrag(root.Sim) is { } shapes)
            root.ShowBlockPaint(shapes.Start, shapes.Path, Aurvangar.Sim.World.BlockId.Slate,
                Aurvangar.ViewCore.Scripts.ShapesScript.StairSouth);   // M11-T11
        if (args.Script == "stairs")
            root.ShowStairTool(Aurvangar.ViewCore.Scripts.StairScript.TooltipPick(root.Sim));   // M11-T8

        if (args.Panel == "workshop")   // M11-T6: the first workshop's panel
            root.OpenWorkshopPanel(root.Sim.Buildings.All.Where(b => b.Def.Workshop is not null).OrderBy(b => b.Id.Value).FirstOrDefault());
        else if (args.Panel == "trade")
            root.OpenTradePanel();

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
