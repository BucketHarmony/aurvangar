using Aurvangar.Sim.Core;
using Aurvangar.Sim.Events;
using Aurvangar.Sim.Jobs;
using Aurvangar.Sim.Paths;

namespace Aurvangar.Sim.Agents;

/// <summary>Owns agents; runs job selection and step execution (JOB-01..09, PTH-15..17).
/// Spawning and path following (M4-T4); job selection and steps run in <see cref="JobRunner"/> (M4-T6).</summary>
public sealed class AgentSystem
{
    private readonly SortedDictionary<int, Agent> _agents = new();
    private readonly EventBus _events;

    public AgentSystem(EventBus events) { _events = events; }

    public IdAllocator Ids { get; } = new();

    /// <summary>All agents, ascending id (JOB-02). Includes dead agents until save/load.</summary>
    public IEnumerable<Agent> All => _agents.Values;

    public int Count => _agents.Count;

    public Agent? Get(AgentId id) => _agents.TryGetValue(id.Value, out var a) ? a : null;

    public Agent Spawn(Int3 cell, string name)
    {
        var a = new Agent { Id = new AgentId(Ids.Allocate()), Name = name, Cell = cell, NextCell = cell };
        _agents.Add(a.Id.Value, a);
        _events.Emit(new AgentSpawned(a.Id));
        return a;
    }

    /// <summary>True if a living agent other than <paramref name="except"/> stands in the cell or is stepping into it.</summary>
    public bool AnyHolds(Int3 c, AgentId except = default)
    {
        foreach (var a in _agents.Values)
            if (a.IsAlive && a.Id != except && (a.Cell == c || a.NextCell == c)) return true;
        return false;
    }

    /// <summary>Paths the agent to a goal and starts following it (PTH-15). See <see cref="AgentMovement.Start"/>.</summary>
    public PathStatus MoveTo(Simulation sim, Agent a, Int3 goal) => MoveTo(sim, a, new[] { goal });

    /// <summary>PTH-11: paths to the cheapest of several goals and starts following it.</summary>
    public PathStatus MoveTo(Simulation sim, Agent a, IReadOnlyList<Int3> goals) =>
        AgentMovement.Start(sim.Pathfinder, a, goals);

    public void Tick(Simulation sim)
    {
        // JOB-02: ascending id; dead agents are skipped. Pick a job if idle, then run the current step (JOB-06..08).
        foreach (var a in _agents.Values)
        {
            if (!a.IsAlive) continue;
            if (FleeRules.Tick(sim, a)) continue;   // WAT-14
            JobRunner.Tick(sim, a);
        }
    }

    /// <summary>Death (WAT-14, ECO-06): releases the agent's job (dropping its stack), stops it and marks it dead. The
    /// agent stays in the list (JOB-02) and leaves the hash (SAV-06). Emits <see cref="AgentDied"/> once.</summary>
    public void Kill(Simulation sim, Agent a, DeathCause cause)
    {
        if (!a.IsAlive) return;
        JobRunner.ReleaseCurrent(sim, a);
        if (!a.Carried.IsEmpty) sim.Actions.Drop(a.Id, a.Cell);
        AgentMovement.Halt(a);
        a.Health = 0;
        a.State = AgentState.Dead;
        a.Death = cause;
        _events.Emit(new AgentDied(a.Id, cause.ToString()));
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
            h.Add((byte)a.Move); h.Add(a.Repathed);
            h.Add(a.NextJobSearchTick);
        }
    }
}
