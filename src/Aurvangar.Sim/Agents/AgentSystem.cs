using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Agents;

/// <summary>Owns agents; runs job selection and step execution (JOB-01..09, PTH-15..17). M4-T4 onward.</summary>
public sealed class AgentSystem
{
    private readonly SortedDictionary<int, Agent> _agents = new();

    public IdAllocator Ids { get; } = new();

    /// <summary>All agents, ascending id (JOB-02). Includes dead agents until save/load.</summary>
    public IEnumerable<Agent> All => _agents.Values;

    public int Count => _agents.Count;

    public Agent? Get(AgentId id) => _agents.TryGetValue(id.Value, out var a) ? a : null;

    public Agent Spawn(Int3 cell, string name)
    {
        var a = new Agent { Id = new AgentId(Ids.Allocate()), Name = name, Cell = cell, NextCell = cell };
        _agents.Add(a.Id.Value, a);
        return a;
    }

    public void Tick(Simulation sim)
    {
        // M4-T4/M4-T6: for each agent in id order: pick job if idle, advance movement, run current step.
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Ids.Next);
        int alive = 0;
        foreach (var a in _agents.Values) if (a.IsAlive) alive++;
        h.Add(alive);
        foreach (var a in _agents.Values)
        {
            if (!a.IsAlive) continue; // SAV-06
            h.Add(a.Id.Value); h.Add(a.Cell); h.Add(a.NextCell); h.Add(a.MoveProgress); h.Add(a.MoveTotal);
            h.Add(a.Hunger); h.Add(a.Thirst); h.Add(a.Health);
            h.Add(a.Carried.Item.Value); h.Add(a.Carried.Count);
            h.Add((byte)a.State); h.Add(a.CurrentJob.Value); h.Add(a.StepIndex); h.Add(a.StepProgress);
            h.Add(a.PathPos); h.Add(a.Path.Length); foreach (var c in a.Path) h.Add(c);
            h.Add(a.NextJobSearchTick);
        }
    }
}
