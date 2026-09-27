using Aurvangar.Sim.Buildings;
using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.Jobs;

/// <summary>JOB-12 (M7-T5, G3 answer 7, ADR-058): recurring jobs that can never succeed. A delivery, pump or haul
/// source earns a strike when one of its jobs is cancelled after its fifth failure (JOB-08), and when a check finds its
/// open job out of every living agent's region (such a job is never claimed, so it never fails). At
/// <see cref="GiveUpMarks.StrikeLimit"/> strikes the source is given up: its poster withdraws its open jobs and posts
/// none until the mark goes, which happens when a job of the source completes, a walkability change lands within
/// <see cref="ResetRadius"/> of the mark's cell, a storage building is completed (all marks), or the source is gone.
/// Stateless: the marks are <see cref="Simulation.GiveUps"/>.</summary>
public static class JobGiveUp
{
    /// <summary>Ticks between reachability checks (the JOB-08 retry cooldown).</summary>
    public const int CheckInterval = Job.RetryCooldown;

    /// <summary>Chebyshev distance from a mark's cell within which a walkability change resets it.</summary>
    public const int ResetRadius = 8;

    /// <summary>The recurring source a job was posted for, or false for any other job (digs, chops, farm work, need
    /// jobs, construction work, jobs posted by tests).</summary>
    public static bool SourceOf(Simulation sim, Job job, out GiveUpSource source, out int id)
    {
        source = default;
        id = 0;
        if (Pumps.IsOperate(job)) { source = GiveUpSource.Pump; id = job.Steps[1].Target; }
        else if (Pumps.IsBufferHaul(job)) { source = GiveUpSource.PumpHaul; id = job.Steps[1].Target; }
        else if (HaulSystem.IsPileHaul(job))
        {
            if (!sim.World.InBounds(job.Target)) return false;
            source = GiveUpSource.Pile;
            id = sim.World.Index(job.Target);
        }
        else if (job.Kind == JobKind.Deliver && Construction.IsSiteJob(job)) { source = GiveUpSource.Site; id = Construction.SiteOf(job).Value; }
        else return false;
        return true;
    }

    /// <summary>JOB-08: the job was cancelled at its fifth failure. One strike against its source.</summary>
    internal static void OnCancelled(Simulation sim, Job job)
    {
        if (SourceOf(sim, job, out var s, out int id)) sim.GiveUps.Strike(s, id, job.Target);
    }

    /// <summary>The job completed: its source succeeded, so its strikes are forgotten.</summary>
    internal static void OnCompleted(Simulation sim, Job job)
    {
        if (sim.GiveUps.Count > 0 && SourceOf(sim, job, out var s, out int id)) sim.GiveUps.Remove(s, id);
    }

    /// <summary>ARCH-01 step 11, right after the region rebuild: resets from this tick's walkability changes, drops
    /// marks whose source is gone, and every <see cref="CheckInterval"/> ticks strikes unreachable sources.</summary>
    public static void Tick(Simulation sim)
    {
        if (sim.GiveUps.Count > 0)
        {
            ResetNear(sim);
            DropStale(sim);
        }
        sim.PathGrid.ClearWalkChanges();
        if (sim.Clock.Tick % CheckInterval == 0) StrikeUnreachable(sim);
    }

    private static void ResetNear(Simulation sim)
    {
        var changes = sim.PathGrid.WalkChanges;
        if (changes.Count == 0) return;
        List<GiveUpMark>? reset = null;
        foreach (var m in sim.GiveUps.All)
        {
            foreach (int ci in changes)
            {
                var c = sim.World.CellOf(ci);
                if (Math.Abs(c.X - m.Cell.X) <= ResetRadius && Math.Abs(c.Y - m.Cell.Y) <= ResetRadius
                    && Math.Abs(c.Z - m.Cell.Z) <= ResetRadius)
                {
                    (reset ??= new()).Add(m);
                    break;
                }
            }
        }
        if (reset is not null)
            foreach (var m in reset) sim.GiveUps.Remove(m.Source, m.Id);
    }

    private static void DropStale(Simulation sim)
    {
        List<GiveUpMark>? gone = null;
        foreach (var m in sim.GiveUps.All)
        {
            bool live = m.Source switch
            {
                GiveUpSource.Site => sim.Buildings.Get(new BuildingId(m.Id)) is { State: BuildingState.Blueprint or BuildingState.UnderConstruction },
                GiveUpSource.Pile => !sim.Piles.At(sim.World.CellOf(m.Id)).IsEmpty,
                _ => sim.Buildings.Get(new BuildingId(m.Id)) is { State: BuildingState.Complete, Def.Producer: not null },
            };
            if (!live) (gone ??= new()).Add(m);
        }
        if (gone is not null)
            foreach (var m in gone) sim.GiveUps.Remove(m.Source, m.Id);
    }

    /// <summary>One strike per source whose open (unclaimed) job no living agent's region reaches (PTH-13, no A*),
    /// and recovery of unreachable marks whose cell is reachable again. Skipped when no living agent stands in a
    /// region.</summary>
    private static void StrikeUnreachable(Simulation sim)
    {
        var regions = new SortedSet<int>();
        foreach (var a in sim.Agents.All)
        {
            if (!a.IsAlive) continue;
            int r = sim.Regions.RegionOf(a.Cell);
            if (r != Paths.Regions.None) regions.Add(r);
        }
        if (regions.Count == 0) return;
        SortedSet<long>? struck = null;
        foreach (var job in sim.Jobs.All)
        {
            if (job.IsClaimed || !SourceOf(sim, job, out var s, out int id) || sim.GiveUps.IsGivenUp(s, id)) continue;
            long key = ((long)s << 32) | (uint)id;
            if (struck is not null && struck.Contains(key)) continue;
            bool reachable = false;
            foreach (int r in regions)
                if (JobRunner.Reachable(sim, job, r)) { reachable = true; break; }
            if (reachable) continue;
            sim.GiveUps.Strike(s, id, job.Target, unreachable: true);
            (struck ??= new()).Add(key);
        }
        // A source given up as unreachable posts no job to check, so its mark is checked instead: it goes when its
        // cell is back in a living agent's region (a far-away change, e.g. a flood draining, reconnected it).
        List<GiveUpMark>? back = null;
        foreach (var m in sim.GiveUps.All)
            if (m.GivenUp && m.Unreachable && regions.Contains(sim.Regions.RegionOf(m.Cell))) (back ??= new()).Add(m);
        if (back is not null)
            foreach (var m in back) sim.GiveUps.Remove(m.Source, m.Id);
    }
}
