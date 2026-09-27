using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;

namespace Aurvangar.Sim.Blocks;

public static partial class BlockBuildSystem
{
    /// <summary>CON-07 <see cref="DesignateBuild"/>: rejections in order (BadHeight, NotBuildable, BadShape, BadRotation
    /// (CON-19), TooLarge, OutOfWorld), then every cell valid by CON-08 (plan support counts the command's own valid cells, one flood) gets
    /// an entry in state Planned or Released; invalid cells are skipped, and NothingToBuild when none is valid. A cell
    /// with an entry takes the new block, form and state; if the block or form changes, the Build job holding it is
    /// cancelled.</summary>
    public static void Designate(Simulation sim, DesignateBuild cmd)
    {
        if (cmd.Height < BuildShapes.MinHeight || cmd.Height > BuildShapes.MaxHeight) { Reject(sim, cmd, "BadHeight"); return; }
        if (!sim.Content.IsConstruction(cmd.Block)) { Reject(sim, cmd, "NotBuildable"); return; }
        if ((int)cmd.Form.Shape >= sim.Content.Shapes.Count) { Reject(sim, cmd, "BadShape"); return; }
        if (!sim.Content.IsValidForm(cmd.Form)) { Reject(sim, cmd, "BadRotation"); return; }
        if (BuildShapes.Count(cmd.Shape, cmd.A, cmd.B, cmd.Height) > BuildShapes.MaxCells) { Reject(sim, cmd, "TooLarge"); return; }
        var cells = BuildShapes.Cells(cmd.Shape, cmd.A, cmd.B, cmd.Height);
        bool any = false;
        foreach (var c in cells)
            if (sim.World.InBounds(c)) { any = true; break; }
        if (!any) { Reject(sim, cmd, "OutOfWorld"); return; }

        var valid = new List<Int3>();
        foreach (var c in cells)
            if (BlockPlans.CheckCell(sim, c) == PlanResult.Ok) valid.Add(c);
        var supported = Support.PlanSupported(sim, valid);
        var add = new List<Int3>();
        foreach (var c in valid)
            if (supported.Contains(sim.World.Index(c))) add.Add(c);
        if (add.Count == 0) { Reject(sim, cmd, "NothingToBuild"); return; }

        var state = cmd.Plan ? PlanState.Planned : PlanState.Released;
        foreach (var c in add)
        {
            var before = sim.Plans.Get(c);
            sim.Plans.Set(c, new PlanEntry(cmd.Block, state, cmd.Form));
            if (before is { } b && (b.Block != cmd.Block || b.Form != cmd.Form)) CancelHolder(sim, c);
        }
    }

    private static void Reject(Simulation sim, DesignateBuild cmd, string reason) =>
        sim.Events.Emit(new CommandRejected(cmd.Tag, reason));
}
