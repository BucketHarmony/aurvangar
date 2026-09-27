using Aurvangar.Sim;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Water;

namespace Aurvangar.ViewCore.Hud;

/// <summary>One item total of the top bar.</summary>
public readonly record struct ItemTotal(string Name, int Count);

/// <summary>The HUD top bar (VIEW-15): day (1-based for the player), season and whole days until it changes (ECO-18,
/// M6-T5), speed, stored totals per item, alerts, and whether the colony is lost (VIEW-18).</summary>
public sealed record TopBar(int Day, Season Season, int DaysLeft, string Speed, IReadOnlyList<ItemTotal> Totals,
    IReadOnlyList<string> Alerts, bool ColonyLost)
{
    public string DayText => $"Day {Day}";

    public string SeasonText => TopBarModel.SeasonText(Season, DaysLeft);

    public string TotalsText => string.Join("   ", Totals.Select(t => $"{t.Name} {t.Count}"));
}

/// <summary>Builds the <see cref="TopBar"/> from sim state. Pure read.</summary>
public static class TopBarModel
{
    public const string NoFood = "No food";
    public const string NoWater = "No water";
    public const string PumpDry = "Pump has no water";
    /// <summary>JOB-12 notice: "Unreachable: Water Pump, log pile x2".</summary>
    public const string UnreachablePrefix = "Unreachable: ";

    public static TopBar Build(Simulation sim, int speedMultiplier)
    {
        var alerts = new List<string>();
        if (NeedsSystem.NoFood(sim)) alerts.Add(NoFood);
        if (NeedsSystem.NoWater(sim)) alerts.Add(NoWater);
        if (sim.Buildings.All.Any(b => b.State == BuildingState.Complete && b.Def.Producer is not null && b.NoWater))
            alerts.Add(PumpDry);
        if (GiveUpText(sim) is { } gaveUp) alerts.Add(gaveUp);
        long tick = sim.Clock.Tick;
        return new TopBar(sim.Clock.Day + 1, WeatherSystem.SeasonAt(tick), WeatherSystem.DaysUntilChange(tick),
            SpeedText(speedMultiplier), Totals(sim), alerts, sim.Agents.ColonyLost);
    }

    /// <summary>JOB-12 / VIEW-15: one notice naming every given-up job source in (source, id) order, repeated names
    /// counted ("log pile x2"); null when nothing is given up.</summary>
    public static string? GiveUpText(Simulation sim)
    {
        var names = new List<string>();
        var counts = new Dictionary<string, int>();   // lookups only; output order is the names list
        foreach (var m in sim.GiveUps.All)
        {
            if (!m.GivenUp) continue;
            string name = Label(sim, m);
            if (counts.TryGetValue(name, out int n)) counts[name] = n + 1;
            else { counts[name] = 1; names.Add(name); }
        }
        if (names.Count == 0) return null;
        return UnreachablePrefix + string.Join(", ", names.Select(n => counts[n] > 1 ? $"{n} x{counts[n]}" : n));
    }

    private static string Label(Simulation sim, GiveUpMark m)
    {
        if (m.Source == GiveUpSource.Pile)
        {
            var stack = sim.Piles.At(sim.World.CellOf(m.Id));
            return stack.IsEmpty ? "pile" : $"{sim.Content.Items[stack.Item.Value].Name.ToLowerInvariant()} pile";
        }
        string building = sim.Buildings.Get(new BuildingId(m.Id))?.Def.Name ?? "building";
        return m.Source switch
        {
            GiveUpSource.Site => $"{building} site",
            GiveUpSource.PumpHaul => $"{building} output",
            _ => building,
        };
    }

    /// <summary>ECO-18: "Wet season, 5 days left" / "Drought, 1 day left".</summary>
    public static string SeasonText(Season season, int daysLeft) =>
        $"{(season == Season.Drought ? "Drought" : "Wet season")}, {daysLeft} day{(daysLeft == 1 ? "" : "s")} left";

    /// <summary>The toast shown on <c>SeasonChanged</c>.</summary>
    public static string SeasonMessage(Season season) => season == Season.Drought
        ? $"Drought: the springs stop for {WeatherSystem.DroughtDays} days and the river drains"
        : $"Wet season: the springs flow again for {WeatherSystem.WetDays} days";

    public static string SpeedText(int multiplier) => multiplier <= 0 ? "Paused" : $"{multiplier}x";

    /// <summary>Stored items in every complete storage building (BLD-12), one entry per item in content order
    /// (log, stone, berries, potato, water), zeros included. Summed here from the buildings rather than read from
    /// <see cref="BuildingSystem.Totals"/>, which is empty after a load until the next tick.</summary>
    public static List<ItemTotal> Totals(Simulation sim)
    {
        var items = sim.Content.Items;
        var counts = new int[items.Count];
        foreach (var b in sim.Buildings.All)
        {
            if (b.State != BuildingState.Complete || b.Def.Storage is null) continue;
            foreach (var (item, n) in b.Stored)
                if (item > 0 && item < counts.Length) counts[item] += n;
        }
        var list = new List<ItemTotal>(items.Count - 1);
        for (int i = 1; i < items.Count; i++) list.Add(new ItemTotal(items[i].Name, counts[i]));
        return list;
    }
}
