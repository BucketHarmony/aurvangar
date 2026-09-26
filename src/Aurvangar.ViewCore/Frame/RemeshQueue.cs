namespace Aurvangar.ViewCore.Frame;

/// <summary>FIFO of chunk indices waiting to be remeshed (VIEW-02). A chunk is queued at most once; queueing it again
/// while it waits is a no-op. The view takes a budgeted batch per frame.</summary>
public sealed class RemeshQueue
{
    private readonly Queue<int> _queue = new();
    private readonly bool[] _queued;

    public RemeshQueue(int chunkCount) => _queued = new bool[chunkCount];

    public int Count => _queue.Count;

    public void Enqueue(int chunkIndex)
    {
        if ((uint)chunkIndex >= (uint)_queued.Length) throw new ArgumentOutOfRangeException(nameof(chunkIndex));
        if (_queued[chunkIndex]) return;
        _queued[chunkIndex] = true;
        _queue.Enqueue(chunkIndex);
    }

    /// <summary>Clears <paramref name="batch"/> and moves up to <paramref name="budget"/> chunks into it, oldest first.</summary>
    public void TakeBatch(int budget, List<int> batch)
    {
        batch.Clear();
        while (batch.Count < budget && _queue.Count > 0)
        {
            int ci = _queue.Dequeue();
            _queued[ci] = false;
            batch.Add(ci);
        }
    }
}
