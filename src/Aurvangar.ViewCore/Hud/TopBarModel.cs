using Aurvangar.Sim;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Buildings;

namespace Aurvangar.ViewCore.Hud;

/// <summary>One item total of the top bar.</summary>
public readonly record struct ItemTotal(string Name, int Count);

/// <summary>The HUD top bar (VIEW-15): day (1-based for the player), speed, stored totals per item, alerts, and whether
/// the colony is lost (VIEW-18). The season and days until it changes (ECO-18) arrive with the weather system
/// (M6-T4, M6-T5; ADR-044).</summary>
public sealed record TopBar(int Day, string Speed, IReadOnlyList<ItemTotal> Totals, IReadOnlyList<string> Alerts, bool ColonyLost)
{
    public string DayText => $"Day {Day}";

    public string TotalsText => string.Join("   ", Totals.Select(t => $"{t.Name} {t.Count}"));
}

/// <summary>Builds the <see cref="TopBar"/> from sim state. Pure read.</summary>
public static class TopBarModel
{
    public const string NoFood = "No food";
    public const string NoWater = "No water";
    public const string PumpDry = "Pump has no water";

    public static TopBar Build(Simulation sim, int speedMultiplier)
    {
        var alerts = new List<string>();
        if (NeedsSystem.NoFood(sim)) alerts.Add(NoFood);
        if (NeedsSystem.NoWater(sim)) alerts.Add(NoWater);
        if (sim.Buildings.All.Any(b => b.State == BuildingState.Complete && b.Def.Producer is not null && b.NoWater))
            alerts.Add(PumpDry);
        return new TopBar(sim.Clock.Day + 1, SpeedText(speedMultiplier), Totals(sim), alerts, sim.Agents.ColonyLost);
    }

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
