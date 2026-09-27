using Aurvangar.Sim;
using Aurvangar.Sim.Agents;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.Jobs;

namespace Aurvangar.ViewCore.Hud;

/// <summary>One colonist row of the left panel (VIEW-16). Bars are 0..1.</summary>
public readonly record struct ColonistRow(AgentId Id, string Name, float Hunger, float Thirst, float Health,
    string Activity, bool Alive, Int3 Cell);

/// <summary>Colonist panel data (VIEW-16): name, hunger/thirst/health bars and what the dwarf is doing. Player-facing
/// text uses the dwarf theme (ADR-008).</summary>
public static class ColonistPanelModel
{
    /// <summary>All colonists in ascending id order, the dead included (until a save/load drops them, SAV-06).</summary>
    public static List<ColonistRow> Build(Simulation sim)
    {
        var rows = new List<ColonistRow>(sim.Agents.Count);
        foreach (var a in sim.Agents.All)
        {
            rows.Add(new ColonistRow(a.Id, a.Name,
                Fraction(a.Hunger, Agent.NeedMax), Fraction(a.Thirst, Agent.NeedMax), Fraction(a.Health, Agent.HealthMax),
                Activity(sim, a), a.IsAlive, a.Cell));
        }
        return rows;
    }

    /// <summary>The current job label ("Digging", "Hauling stone", …), or the agent's state when it has no job.</summary>
    public static string Activity(Simulation sim, Agent a)
    {
        if (!a.IsAlive) return a.Death switch
        {
            DeathCause.Drowned => "Drowned",
            DeathCause.Starved => "Starved",
            DeathCause.Dehydrated => "Died of thirst",
            _ => "Dead",
        };
        if (a.State == AgentState.Trapped) return "Trapped in deep water!";
        var job = a.CurrentJob.IsValid ? sim.Jobs.Get(a.CurrentJob) : null;
        if (job is null) return "Idle";
        return job.Kind switch
        {
            JobKind.Dig => "Digging",
            JobKind.Chop => "Felling a tree",
            JobKind.Haul => HaulLabel(sim, job),
            JobKind.Flee => "Fleeing the water",
            JobKind.Drink => "Drinking",
            JobKind.Eat => "Eating",
            JobKind.Deliver => "Carrying building materials",
            JobKind.Construct => "Building",
            JobKind.Deconstruct => "Tearing down",
            JobKind.Build => "Placing blocks",
            JobKind.OperatePump => "Working the pump",
            JobKind.Harvest => "Harvesting",
            JobKind.Plant => "Planting",
            JobKind.Craft => CraftLabel(sim, job),   // CRF-10
            JobKind.Unload => HaulLabel(sim, job),   // CRF-12
            _ => job.Kind.ToString(),
        };
    }

    /// <summary>CRF-10: the recipe name ("Saw planks") of a Craft job, from its last step (the workshop and recipe).</summary>
    private static string CraftLabel(Simulation sim, Job job)
    {
        var last = job.Steps[^1];
        if (sim.Buildings.Get(new BuildingId(last.Target)) is { } b && last.Count < sim.Content.RecipesOf(b.Def).Count)
            return sim.Content.RecipesOf(b.Def)[last.Count].Name;
        return "Crafting";
    }

    private static string HaulLabel(Simulation sim, Job job)
    {
        foreach (var s in job.Steps)
            if (s.Kind is StepKind.PickUp or StepKind.PickUpFromStorage && s.Item.IsValid)
                return "Hauling " + sim.Content.ItemDef(s.Item).Name.ToLowerInvariant();
        return "Hauling";
    }

    private static float Fraction(int value, int max) => Math.Clamp(value / (float)max, 0f, 1f);
}
