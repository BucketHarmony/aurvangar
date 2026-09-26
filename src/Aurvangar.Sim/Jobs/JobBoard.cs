using Aurvangar.Sim.Actions;
using Aurvangar.Sim.Core;
using Aurvangar.Sim.World;

namespace Aurvangar.Sim.Jobs;

/// <summary>Open jobs by id (JOB-03) and the reservations claimed jobs hold (JOB-04, ADR-028). Storage only:
/// claiming, running, failing and cancelling live in <see cref="JobRunner"/>. The reservation tables are derived
/// from the claimed jobs, so they are not hashed separately (a load rebuilds them with <see cref="RebuildReservations"/>).</summary>
public sealed class JobBoard
{
    private readonly VoxelWorld _world;
    private readonly SortedDictionary<int, Job> _jobs = new();
    private readonly SortedDictionary<int, int> _cells = new();                 // cell index → job id
    private readonly SortedDictionary<int, int> _pileOut = new();               // cell index → reserved count
    private readonly SortedDictionary<(int B, int Item), int> _storageIn = new();
    private readonly SortedDictionary<(int B, int Item), int> _storageOut = new();

    public JobBoard(VoxelWorld world) { _world = world; }

    public IdAllocator Ids { get; } = new();

    /// <summary>All jobs on the board (claimed or not), ascending id.</summary>
    public IEnumerable<Job> All => _jobs.Values;

    public int Count => _jobs.Count;

    public Job? Get(JobId id) => _jobs.TryGetValue(id.Value, out var j) ? j : null;

    /// <summary>Posts a job with a monotonically allocated id (JOB-03). Priority defaults to the JOB-05 table.</summary>
    public Job Post(JobKind kind, Int3 target, IEnumerable<JobStep> steps, IEnumerable<Reservation>? reservations = null,
        int? priority = null)
    {
        var job = new Job
        {
            Id = new JobId(Ids.Allocate()),
            Kind = kind,
            Target = target,
            Priority = priority ?? Job.DefaultPriority(kind),
            Steps = new List<JobStep>(steps),
            Reservations = reservations is null ? new List<Reservation>() : new List<Reservation>(reservations),
        };
        _jobs.Add(job.Id.Value, job);
        return job;
    }

    /// <summary>Removes a job. Its reservations must already be released.</summary>
    internal void Remove(Job job) => _jobs.Remove(job.Id.Value);

    // ---- reservations (JOB-04) ----

    /// <summary>True if the cell is reserved by some claimed job.</summary>
    public bool IsCellReserved(Int3 c) => _world.InBounds(c) && _cells.ContainsKey(_world.Index(c));

    /// <summary>Items of the pile at the cell reserved by claimed jobs.</summary>
    public int ReservedFromPile(Int3 c) => _world.InBounds(c) && _pileOut.TryGetValue(_world.Index(c), out var n) ? n : 0;

    /// <summary>BLD-10: items on their way into a storage building.</summary>
    public int ReservedIn(BuildingId b, ItemId item) => _storageIn.TryGetValue((b.Value, item.Value), out var n) ? n : 0;

    /// <summary>BLD-10: items promised out of a storage building.</summary>
    public int ReservedOut(BuildingId b, ItemId item) => _storageOut.TryGetValue((b.Value, item.Value), out var n) ? n : 0;

    /// <summary>True if every reservation of the job can be taken now, next to what other claimed jobs hold.</summary>
    public bool CanReserve(Simulation sim, Job job)
    {
        foreach (var r in job.Reservations)
        {
            switch (r.Kind)
            {
                case ReservationKind.Cell:
                    if (!_world.InBounds(r.Cell)) return false;
                    if (_cells.TryGetValue(_world.Index(r.Cell), out var holder) && holder != job.Id.Value) return false;
                    break;
                case ReservationKind.PileItems:
                    var pile = sim.Piles.At(r.Cell);
                    if (pile.IsEmpty || pile.Item != r.Item || pile.Count - ReservedFromPile(r.Cell) < r.Count) return false;
                    break;
                case ReservationKind.StorageOut:
                {
                    var b = sim.Buildings.Get(r.Building);
                    int stored = b is null ? 0 : WorldActions.StoredCount(b, r.Item);
                    if (stored - ReservedOut(r.Building, r.Item) < r.Count) return false;
                    break;
                }
                case ReservationKind.StorageIn:
                {
                    var b = sim.Buildings.Get(r.Building);
                    if (b?.Def.Storage is null) return false;
                    if (WorldActions.FreeCapacity(b, r.Item) - ReservedIn(r.Building, r.Item) < r.Count) return false;
                    break;
                }
            }
        }
        return true;
    }

    internal void TakeReservations(Job job) => Apply(job, +1);

    internal void ReleaseReservations(Job job) => Apply(job, -1);

    /// <summary>Rebuilds the reservation tables from the claimed jobs (after a load).</summary>
    public void RebuildReservations()
    {
        _cells.Clear(); _pileOut.Clear(); _storageIn.Clear(); _storageOut.Clear();
        foreach (var j in _jobs.Values)
            if (j.IsClaimed) Apply(j, +1);
    }

    private void Apply(Job job, int sign)
    {
        foreach (var r in job.Reservations)
        {
            switch (r.Kind)
            {
                case ReservationKind.Cell:
                    int ci = _world.Index(r.Cell);
                    if (sign > 0) _cells[ci] = job.Id.Value;
                    else if (_cells.TryGetValue(ci, out var h) && h == job.Id.Value) _cells.Remove(ci);
                    break;
                case ReservationKind.PileItems: Add(_pileOut, _world.Index(r.Cell), sign * r.Count); break;
                case ReservationKind.StorageIn: Add(_storageIn, (r.Building.Value, r.Item.Value), sign * r.Count); break;
                case ReservationKind.StorageOut: Add(_storageOut, (r.Building.Value, r.Item.Value), sign * r.Count); break;
            }
        }
    }

    private static void Add<TKey>(SortedDictionary<TKey, int> table, TKey key, int delta) where TKey : notnull
    {
        int n = (table.TryGetValue(key, out var v) ? v : 0) + delta;
        if (n > 0) table[key] = n;
        else table.Remove(key);
    }

    /// <summary>Open (unclaimed) jobs per kind, ascending kind, for the debug overlay.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> OpenCountsByKind()
    {
        var counts = new SortedDictionary<JobKind, int>();
        foreach (var j in _jobs.Values)
            if (!j.IsClaimed) counts[j.Kind] = (counts.TryGetValue(j.Kind, out var n) ? n : 0) + 1;
        var list = new List<KeyValuePair<string, int>>(counts.Count);
        foreach (var (k, n) in counts) list.Add(new(k.ToString(), n));
        return list;
    }

    public void AddToHash(ref StateHasher h)
    {
        h.Add(Ids.Next);
        h.Add(_jobs.Count);
        foreach (var j in _jobs.Values) j.AddToHash(ref h);
    }
}
