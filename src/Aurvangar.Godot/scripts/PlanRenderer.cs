using Aurvangar.Sim;
using Aurvangar.ViewCore.Entities;
using Aurvangar.ViewCore.Meshing;

namespace Aurvangar.Client;

/// <summary>Plan entries as translucent ghosts (VIEW-22, <see cref="PlanGhostMesher"/>). Rebuilt when an entry or the
/// slice changes, and every <see cref="PlanGhostMesher.RefreshTicks"/> ticks while entries exist (their statuses follow
/// the world). One batched status scan per rebuild.</summary>
public partial class PlanRenderer : TranslucentMesh
{
    private Simulation _sim = null!;
    private BlockColors _blocks = null!;
    private EntityColors _entities = null!;
    private ulong _signature;
    private int _slice = int.MinValue;
    private long _tick = long.MinValue;

    public void Init(Simulation sim, BlockColors blocks, EntityColors entities)
    {
        _sim = sim;
        _blocks = blocks;
        _entities = entities;
    }

    public void Refresh(int sliceY)
    {
        ulong signature = PlanGhostMesher.Signature(_sim);
        long tick = _sim.Clock.Tick;
        bool stale = _sim.Plans.Count > 0 && (tick - _tick >= PlanGhostMesher.RefreshTicks || tick < _tick);
        if (signature == _signature && sliceY == _slice && !stale) return;
        _signature = signature;
        _slice = sliceY;
        _tick = tick;
        SetData(_sim.Plans.Count == 0 ? null : PlanGhostMesher.Build(PlanGhostMesher.Ghosts(_sim, sliceY), sliceY, _blocks, _entities));
    }
}
