using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>JOB-05 job kinds.</summary>
public enum JobKind : byte
{
    Drink = 1, Eat, Flee, Deliver, Construct, OperatePump, Harvest, Plant, Dig, Chop, Haul, Deconstruct,
}

/// <summary>What one job step does. GoTo moves; Work spends ticks; the rest are one <see cref="Actions.WorldActions"/>
/// call each (JOB-08).</summary>
public enum StepKind : byte { GoTo, Work, Dig, Chop, PickUp, PickUpFromStorage, DeliverTo, Consume, Drop, Plant, Harvest, HarvestBush }

/// <summary>Where a GoTo step may end: on the cell itself, anywhere the cell is in reach (ARCH-07), anywhere a
/// building's footprint is in reach, or (JOB-09) in reach of a dig target but never on top of it.</summary>
public enum GoalMode : byte { Exact, Reach, Building, Dig }

/// <summary>One step of a job. Plain data so it can be hashed and saved. <see cref="Target"/> is a building id (GoTo
/// Building, Work on a building, storage steps) or a plant id (Chop, HarvestBush).</summary>
public readonly record struct JobStep(StepKind Kind, Int3 Cell, int Target, ItemId Item, int Count, int Ticks, GoalMode Goal)
{
    public static JobStep GoTo(Int3 cell, GoalMode goal = GoalMode.Reach) => new(StepKind.GoTo, cell, 0, default, 0, 0, goal);
    public static JobStep GoToBuilding(BuildingId b) => new(StepKind.GoTo, default, b.Value, default, 0, 0, GoalMode.Building);
    public static JobStep Work(Int3 cell, int ticks) => new(StepKind.Work, cell, 0, default, 0, ticks, default);
    public static JobStep WorkOn(BuildingId b, int ticks) => new(StepKind.Work, default, b.Value, default, 0, ticks, GoalMode.Building);
    public static JobStep Dig(Int3 cell) => new(StepKind.Dig, cell, 0, default, 0, 0, default);
    public static JobStep Chop(PlantId tree) => new(StepKind.Chop, default, tree.Value, default, 0, 0, default);
    public static JobStep PickUp(Int3 pile, ItemId item, int count) => new(StepKind.PickUp, pile, 0, item, count, 0, default);
    public static JobStep PickUpFromStorage(BuildingId b, ItemId item, int count) => new(StepKind.PickUpFromStorage, default, b.Value, item, count, 0, default);
    public static JobStep DeliverTo(BuildingId b) => new(StepKind.DeliverTo, default, b.Value, default, 0, 0, default);
    public static JobStep Consume(BuildingId b, ItemId item) => new(StepKind.Consume, default, b.Value, item, 1, 0, default);
    public static JobStep Drop(Int3 cell) => new(StepKind.Drop, cell, 0, default, 0, 0, default);
    public static JobStep PlantCrop(Int3 tile) => new(StepKind.Plant, tile, 0, default, 0, 0, default);
    public static JobStep HarvestCrop(Int3 tile) => new(StepKind.Harvest, tile, 0, default, 0, 0, default);
    public static JobStep HarvestBush(PlantId bush) => new(StepKind.HarvestBush, default, bush.Value, default, 0, 0, default);

    public void AddToHash(ref StateHasher h)
    {
        h.Add((byte)Kind); h.Add(Cell); h.Add(Target); h.Add(Item.Value); h.Add(Count); h.Add(Ticks); h.Add((byte)Goal);
    }
}

/// <summary>JOB-04 reservation kinds: a cell (dig target, pile), items taken from a pile, items entering or leaving
/// a storage building.</summary>
public enum ReservationKind : byte { Cell, PileItems, StorageIn, StorageOut }

public readonly record struct Reservation(ReservationKind Kind, Int3 Cell, BuildingId Building, ItemId Item, int Count)
{
    public static Reservation OnCell(Int3 c) => new(ReservationKind.Cell, c, default, default, 0);
    public static Reservation FromPile(Int3 c, ItemId item, int count) => new(ReservationKind.PileItems, c, default, item, count);
    public static Reservation IntoStorage(BuildingId b, ItemId item, int count) => new(ReservationKind.StorageIn, default, b, item, count);
    public static Reservation OutOfStorage(BuildingId b, ItemId item, int count) => new(ReservationKind.StorageOut, default, b, item, count);

    public void AddToHash(ref StateHasher h)
    {
        h.Add((byte)Kind); h.Add(Cell); h.Add(Building.Value); h.Add(Item.Value); h.Add(Count);
    }
}

/// <summary>A job on the board (JOB-04). Steps run in order; reservations are held while claimed.</summary>
public sealed class Job
{
    /// <summary>JOB-08: failures before a job is cancelled.</summary>
    public const int MaxFailures = 5;

    /// <summary>JOB-08: ticks a failed job waits before it can be claimed again.</summary>
    public const int RetryCooldown = 50;

    public JobId Id { get; init; }
    public JobKind Kind { get; init; }
    /// <summary>Higher first (JOB-05, DSG-04 bonus).</summary>
    public int Priority { get; set; }
    /// <summary>The cell used for the JOB-06 distance tie-break and for designation bookkeeping.</summary>
    public Int3 Target { get; init; }
    /// <summary>Appendable (JOB-11 chains a haul onto a harvest).</summary>
    public List<JobStep> Steps { get; init; } = new();
    public List<Reservation> Reservations { get; init; } = new();

    public AgentId ClaimedBy { get; set; }
    public int Failures { get; set; }
    public long RetryAfterTick { get; set; }

    public bool IsClaimed => ClaimedBy.IsValid;

    /// <summary>JOB-07 need jobs: generated per agent and claimed at once.</summary>
    public bool IsNeed => Kind is JobKind.Drink or JobKind.Eat or JobKind.Flee;

    /// <summary>JOB-05 base priorities.</summary>
    public static int DefaultPriority(JobKind kind) => kind switch
    {
        JobKind.Flee => 200,
        JobKind.Drink => 100,
        JobKind.Eat => 90,
        JobKind.Deliver => 50,
        JobKind.Construct => 45,
        JobKind.OperatePump => 40,
        JobKind.Harvest => 35,
        JobKind.Plant => 30,
        JobKind.Dig => 25,
        JobKind.Chop => 25,
        JobKind.Deconstruct => 25,
        JobKind.Haul => 20,
        _ => 0,
    };

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Id.Value); h.Add((byte)Kind); h.Add(Priority); h.Add(Target);
        h.Add(Steps.Count); foreach (var s in Steps) s.AddToHash(ref h);
        h.Add(Reservations.Count); foreach (var r in Reservations) r.AddToHash(ref h);
        h.Add(ClaimedBy.Value); h.Add(Failures); h.Add(RetryAfterTick);
    }
}
