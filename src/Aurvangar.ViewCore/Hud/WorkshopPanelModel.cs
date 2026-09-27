using Aurvangar.Sim;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Commands;
using Aurvangar.Sim.Content;
using Aurvangar.Sim.Core;

namespace Aurvangar.ViewCore.Hud;

/// <summary>One recipe row of the workshop panel (VIEW-28): the recipe text ("Saw planks: 1 log -> 2 planks"), the
/// row's mode (the order's, or the mode picked for a row with no order), its count (0 with no order) and, for a Make
/// order, "done/count"; <see cref="Stock"/> is the colony stock of the output (CRF-08), shown for Keep.</summary>
public sealed record WorkshopRow(int Recipe, string Text, OrderMode Mode, bool HasOrder, int Count, string? Progress, int Stock)
{
    /// <summary>"done 2/5" for Make, "in stock 12" for Keep, "no order".</summary>
    public string ProgressText => Progress is { } p ? $"done {p}" : HasOrder ? $"in stock {Stock}" : "no order";
}

/// <summary>The workshop panel (VIEW-28): the building, its title and CRF-13 status text, and one row per recipe.</summary>
public sealed record WorkshopPanel(BuildingId Building, string Title, string StatusText, WorkshopStatus Status,
    IReadOnlyList<WorkshopRow> Rows);

/// <summary>VIEW-28 (M11-T6, ADR-085): the workshop panel's texts and the commands its buttons send. The only view state
/// is the mode picked on rows with no order (<see cref="PickedModes"/>, Make by default); everything else is read from
/// the building each frame. Every change sends at most one <see cref="SetWorkshopOrder"/>: a count step on a row with
/// no order creates it in the picked mode, a step to 0 or Clear removes it (count 0), and a mode toggle on an order
/// re-sends it with the same count in the new mode (the sim resets a replaced order's done to 0, CRF-07).</summary>
public sealed class WorkshopPanelModel
{
    /// <summary>Count step of - and +; <see cref="ShiftStep"/> with Shift.</summary>
    public const int Step = 1;
    public const int ShiftStep = 5;

    /// <summary>The workshop shown, or invalid when the panel is closed.</summary>
    public BuildingId Building { get; private set; }

    /// <summary>Modes picked on rows that have no order, by recipe index.</summary>
    public Dictionary<int, OrderMode> PickedModes { get; } = new();   // lookups only

    public bool IsOpen => Building.IsValid;

    /// <summary>Opens the panel on <paramref name="b"/> when it is a workshop (complete or planned); returns whether it
    /// did. Opening another workshop forgets the picked modes.</summary>
    public bool Open(Building? b)
    {
        if (b?.Def.Workshop is null) return false;
        if (b.Id != Building) PickedModes.Clear();
        Building = b.Id;
        return true;
    }

    public void Close()
    {
        Building = default;
        PickedModes.Clear();
    }

    /// <summary>The panel for the open workshop, or null when it is closed or the building is gone (the caller then
    /// closes it).</summary>
    public WorkshopPanel? Build(Simulation sim)
    {
        if (!IsOpen || sim.Buildings.Get(Building) is not { } b || b.Def.Workshop is null) return null;
        var state = Workshops.StatusOf(sim, b);
        var rows = new List<WorkshopRow>();
        foreach (var r in sim.Content.RecipesOf(b.Def))
        {
            var o = b.OrderFor(r.Index);
            var mode = o?.Mode ?? PickedModes.GetValueOrDefault(r.Index, OrderMode.Make);
            string? progress = o is { Mode: OrderMode.Make } ? $"{o.Done}/{o.Count}" : null;
            rows.Add(new WorkshopRow(r.Index, RecipeText(sim.Content, r), mode, o is not null, o?.Count ?? 0, progress,
                Economy.Stock(sim, r.Output)));
        }
        return new WorkshopPanel(b.Id, b.Def.Name, StatusText(sim.Content, b.Def.Name, state), state.Status, rows);
    }

    /// <summary>"Saw planks: 1 log -> 2 planks".</summary>
    public static string RecipeText(ContentDb content, Recipe r) =>
        $"{r.Name}: {r.InputCount} {ItemName(content, r.Input)} -> {r.OutputCount} {ItemName(content, r.Output)}";

    /// <summary>CRF-13 in words: "Sawmill: needs log", "Sawmill: output full (planks)", "Sawmill: working", ...</summary>
    public static string StatusText(ContentDb content, string name, WorkshopState state) => name + ": " + state.Status switch
    {
        WorkshopStatus.NotBuilt => "not built yet",
        WorkshopStatus.NoOrders => "no orders",
        WorkshopStatus.Working => "working",
        WorkshopStatus.OutputFull => $"output full ({ItemName(content, state.Item)})",
        WorkshopStatus.NoInput => $"needs {ItemName(content, state.Item)}",
        WorkshopStatus.Waiting => "waiting for a crafter",
        WorkshopStatus.Done => "orders met",
        _ => state.Status.ToString(),
    };

    /// <summary>- or + on a row: the count moves by <paramref name="delta"/> (clamped to 0..999). Null when nothing
    /// changes (0 on a row with no order, or already at a limit).</summary>
    public SetWorkshopOrder? StepCount(Simulation sim, int recipe, int delta)
    {
        if (Row(sim, recipe) is not { } row) return null;
        int count = Math.Clamp(row.Count + delta, 0, WorkshopOrder.MaxCount);
        if (count == row.Count) return null;
        if (count == 0) PickedModes[recipe] = row.Mode;   // a cleared row keeps its mode
        return new SetWorkshopOrder(Building, recipe, row.Mode, count);
    }

    /// <summary>The Make or Keep toggle: on a row with an order it re-sends the order in the new mode; on a row with no
    /// order it only picks the mode for the next + (no command).</summary>
    public SetWorkshopOrder? SetMode(Simulation sim, int recipe, OrderMode mode)
    {
        if (Row(sim, recipe) is not { } row) return null;
        if (!row.HasOrder)
        {
            PickedModes[recipe] = mode;
            return null;
        }
        return row.Mode == mode ? null : new SetWorkshopOrder(Building, recipe, mode, row.Count);
    }

    /// <summary>Clear: removes the row's order (count 0); null when it has none.</summary>
    public SetWorkshopOrder? Clear(Simulation sim, int recipe)
    {
        if (Row(sim, recipe) is not { HasOrder: true } row) return null;
        PickedModes[recipe] = row.Mode;
        return new SetWorkshopOrder(Building, recipe, row.Mode, 0);
    }

    /// <summary>The count step for a click: <see cref="ShiftStep"/> with Shift, else <see cref="Step"/>.</summary>
    public static int StepSize(bool shift) => shift ? ShiftStep : Step;

    private WorkshopRow? Row(Simulation sim, int recipe) =>
        Build(sim) is { } panel && recipe >= 0 && recipe < panel.Rows.Count ? panel.Rows[recipe] : null;

    private static string ItemName(ContentDb content, ItemId item) =>
        item.IsValid ? content.ItemDef(item).Name.ToLowerInvariant() : "items";
}
