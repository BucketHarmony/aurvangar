namespace Aurvangar.ViewCore.Frame;

/// <summary>DF-style view level (VIEW-04). <see cref="SliceY"/> starts at <c>SizeY - 1</c> (no slicing) and is
/// clamped to 0..SizeY-1. Every change queues the remeshes it needs on the <see cref="RemeshRouter"/>.</summary>
public sealed class SliceController
{
    public SliceController(int sizeY)
    {
        MaxY = sizeY - 1;
        SliceY = MaxY;
    }

    public int MaxY { get; }
    public int SliceY { get; private set; }

    /// <summary>True when cells are hidden (the slice is below the top of the world).</summary>
    public bool IsSliced => SliceY < MaxY;

    /// <summary>PageUp / ']' (+1) and PageDown / '[' (-1). Returns true if the slice moved.</summary>
    public bool Step(int delta, RemeshRouter router) => Set(SliceY + delta, router);

    public bool Set(int sliceY, RemeshRouter router)
    {
        int next = Math.Clamp(sliceY, 0, MaxY);
        if (next == SliceY) return false;
        router.EnqueueSliceChange(SliceY, next);
        SliceY = next;
        return true;
    }
}
