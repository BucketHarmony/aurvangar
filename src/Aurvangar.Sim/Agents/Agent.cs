using Aurvangar.Sim.Core;
using Aurvangar.Sim.Items;

namespace Aurvangar.Sim.Agents;

/// <summary>Trapped (WAT-14, ADR-031): in deep water with no flee path; it re-searches every 5 ticks.</summary>
public enum AgentState : byte { Idle, Working, Dead, Trapped }

public enum DeathCause : byte { None, Starved, Dehydrated, Drowned }

/// <summary>A colonist (JOB-01). A future player avatar is an Agent driven by input instead of the job board.</summary>
public sealed class Agent
{
    public const int CarryCapacity = 10;
    public const int NeedMax = 10_000;   // ECO-02
    public const int HealthMax = 1_000;  // ECO-06

    public AgentId Id { get; init; }
    public string Name { get; init; } = "";

    public Int3 Cell { get; set; }
    public Int3 NextCell { get; set; }
    /// <summary>Ticks spent moving toward NextCell. The view lerps Cell→NextCell by MoveProgress/MoveTotal.</summary>
    public int MoveProgress { get; set; }
    public int MoveTotal { get; set; }

    public int Hunger { get; set; } = NeedMax;
    public int Thirst { get; set; } = NeedMax;
    public int Health { get; set; } = HealthMax;

    public ItemStack Carried { get; set; } = ItemStack.Empty;

    public AgentState State { get; set; } = AgentState.Idle;
    public DeathCause Death { get; set; }

    public JobId CurrentJob { get; set; }
    public int StepIndex { get; set; }
    public int StepProgress { get; set; }
    public Int3[] Path { get; set; } = Array.Empty<Int3>();
    public int PathPos { get; set; }
    /// <summary>Path-following status (PTH-15/16). Path is non-empty only while Moving.</summary>
    public MoveStatus Move { get; set; }
    /// <summary>True once the current move has used its one PTH-16 repath.</summary>
    public bool Repathed { get; set; }

    /// <summary>Tick before which the agent will not try to pick a job again (JOB-06 throttle).</summary>
    public long NextJobSearchTick { get; set; }

    /// <summary>ECO-04: tick before which the agent will not try to post a Drink / Eat job again (NeedsSystem).</summary>
    public long NextDrinkTick { get; set; }
    public long NextEatTick { get; set; }

    public bool IsAlive => State != AgentState.Dead;
}
