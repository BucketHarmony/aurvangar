namespace Aurvangar.ViewCore.Hud;

/// <summary>One item of the plan's material line (VIEW-23): the need, the stock for the building part, and whether the
/// stock falls short (shown orange).</summary>
public readonly record struct PlanItem(string Name, int Need, int? Stock, bool Short)
{
    public string Text => Stock is { } s ? $"{Name} {Need}/{s}" : $"{Name} {Need}";
}

/// <summary>VIEW-23: "Building: stone 120/64 · Planned: stone 70, log 12". <see cref="Building"/> is the Released
/// entries' need against stock; <see cref="Planned"/> the Planned entries' need. Built by
/// <see cref="TopBarModel.PlanText"/>.</summary>
public sealed record PlanText(IReadOnlyList<PlanItem> Building, IReadOnlyList<PlanItem> Planned)
{
    public const string Separator = " · ";

    public bool IsEmpty => Building.Count == 0 && Planned.Count == 0;

    public string Text => Compose(i => i.Text);

    /// <summary>The text with each short item wrapped in a BBCode colour tag (Godot RichTextLabel).</summary>
    public string BbCode(string shortColor) => Compose(i => i.Short ? $"[color={shortColor}]{i.Text}[/color]" : i.Text);

    /// <summary>The text in runs, each flagged short or not (a UI that colours runs, like the Godot top bar).
    /// Concatenated, the runs are <see cref="Text"/>.</summary>
    public List<(string Text, bool Short)> Segments()
    {
        var runs = new List<(string, bool)>();
        void Part(string head, IReadOnlyList<PlanItem> items)
        {
            if (items.Count == 0) return;
            if (runs.Count > 0) runs.Add((Separator, false));
            runs.Add((head, false));
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) runs.Add((", ", false));
                runs.Add((items[i].Text, items[i].Short));
            }
        }
        Part("Building: ", Building);
        Part("Planned: ", Planned);
        return runs;
    }

    /// <summary>The line in wrap units (M9-T3): each item with the separator before it (", " or <see cref="Separator"/>,
    /// dropped when the unit starts a line) and its runs; a part's header ("Building: ") stays with its first item.
    /// Concatenated with their leads, the units are <see cref="Text"/>.</summary>
    public List<PlanAtom> Atoms()
    {
        var atoms = new List<PlanAtom>();
        void Part(string head, IReadOnlyList<PlanItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                string lead = i > 0 ? ", " : atoms.Count > 0 ? Separator : "";
                var runs = new List<(string, bool)>(2);
                if (i == 0) runs.Add((head, false));
                runs.Add((items[i].Text, items[i].Short));
                atoms.Add(new PlanAtom(lead, runs));
            }
        }
        Part("Building: ", Building);
        Part("Planned: ", Planned);
        return atoms;
    }

    private string Compose(Func<PlanItem, string> item)
    {
        var parts = new List<string>(2);
        if (Building.Count > 0) parts.Add("Building: " + string.Join(", ", Building.Select(item)));
        if (Planned.Count > 0) parts.Add("Planned: " + string.Join(", ", Planned.Select(item)));
        return string.Join(Separator, parts);
    }
}

/// <summary>One wrap unit of the plan line (<see cref="PlanText.Atoms"/>): the separator before it and its coloured
/// runs.</summary>
public sealed record PlanAtom(string Lead, IReadOnlyList<(string Text, bool Short)> Runs);
