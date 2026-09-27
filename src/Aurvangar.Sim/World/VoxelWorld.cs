using Aurvangar.Sim.Core;

namespace Aurvangar.Sim.World;

/// <summary>Block ids. Must match data/blocks.json (validated by ContentDb).</summary>
public enum BlockId : byte
{
    Air = 0,
    Bedrock = 1,
    Stone = 2,
    Dirt = 3,
    Grass = 4,
    Sand = 5,
    Farmland = 6,
    BuildingSolid = 7,
    /// <summary>CON-01 construction blocks (M8-T2): built by dwarves from a plan entry.</summary>
    Masonry = 8,
    Planks = 9,
    PolishedStone = 10,
    /// <summary>M9-T4 construction blocks (CON-01).</summary>
    Rubble = 11,
    Beam = 12,
    Slate = 13,
}

/// <summary>Dense voxel storage with chunk dirty tracking. Spec: docs/specs/world.md (WLD-01..07).</summary>
public sealed class VoxelWorld
{
    public const int ChunkSize = 32;
    public const int ChunkShift = 5;

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public int ChunksZ { get; }
    public int CellCount { get; }
    public int ChunkCount { get; }

    private readonly byte[] _blocks;
    private readonly bool[] _solid;
    private readonly bool[] _chunkDirty;
    private readonly List<int> _dirtyChunks = new();
    private readonly List<int> _changedCells = new();
    /// <summary>CON-19 (M11-T10): the form of every cell that is not Full, by cell index (packed). Sparse: only built
    /// blocks with a fine shape have one. Iterated in index order (saved and hashed).</summary>
    private readonly SortedDictionary<int, byte> _forms = new();

    /// <param name="solidTable">ContentDb.SolidTable (indexed by block byte).</param>
    public VoxelWorld(int sizeX, int sizeY, int sizeZ, bool[] solidTable)
    {
        // WLD-01
        if (sizeX % ChunkSize != 0 || sizeY % ChunkSize != 0 || sizeZ % ChunkSize != 0 || sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
            throw new ArgumentException($"World size must be positive multiples of {ChunkSize}: {sizeX}x{sizeY}x{sizeZ}");
        SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
        ChunksX = sizeX >> ChunkShift; ChunksY = sizeY >> ChunkShift; ChunksZ = sizeZ >> ChunkShift;
        CellCount = sizeX * sizeY * sizeZ;
        ChunkCount = ChunksX * ChunksY * ChunksZ;
        _blocks = new byte[CellCount];
        _solid = solidTable;
        _chunkDirty = new bool[ChunkCount];
    }

    /// <summary>WLD-02: x + z*SizeX + y*SizeX*SizeZ.</summary>
    public int Index(Int3 c) => c.X + c.Z * SizeX + c.Y * SizeX * SizeZ;

    public int Index(int x, int y, int z) => x + z * SizeX + y * SizeX * SizeZ;

    public Int3 CellOf(int index)
    {
        int layer = SizeX * SizeZ;
        int y = index / layer;
        int rem = index - y * layer;
        int z = rem / SizeX;
        return new Int3(rem - z * SizeX, y, z);
    }

    public bool InBounds(Int3 c) => (uint)c.X < (uint)SizeX && (uint)c.Y < (uint)SizeY && (uint)c.Z < (uint)SizeZ;

    public bool InBounds(int x, int y, int z) => (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;

    /// <summary>WLD-04: below the world is Bedrock, everything else out of bounds is Air.</summary>
    public BlockId GetBlock(Int3 c) => GetBlock(c.X, c.Y, c.Z);

    public BlockId GetBlock(int x, int y, int z)
    {
        if (InBounds(x, y, z)) return (BlockId)_blocks[Index(x, y, z)];
        return y < 0 ? BlockId.Bedrock : BlockId.Air;
    }

    public bool IsSolid(Int3 c) => _solid[(byte)GetBlock(c)];

    public bool IsSolid(int x, int y, int z) => _solid[(byte)GetBlock(x, y, z)];

    /// <summary>Solidity by flat index (caller guarantees bounds). Hot path.</summary>
    public bool IsSolidAt(int index) => _solid[_blocks[index]];

    /// <summary>Whether a raw block byte (from <see cref="Blocks"/>) is solid.</summary>
    public bool IsSolidBlock(byte block) => _solid[block];

    /// <summary>Set a block. Returns false if out of bounds or unchanged. Marks chunks dirty and records the change.</summary>
    public bool SetBlock(Int3 c, BlockId b)
    {
        if (!InBounds(c)) return false;                     // WLD-04: OOB writes rejected
        int i = Index(c);
        if (_blocks[i] == (byte)b) return false;
        _blocks[i] = (byte)b;
        if (_forms.Count != 0) _forms.Remove(i);   // CON-19: any write that changes the block makes the cell Full again
        _changedCells.Add(i);
        MarkDirtyAround(c);
        return true;
    }

    /// <summary>Bulk write used by generators and loaders. Does not record changes or dirty flags; call MarkAllDirty after.</summary>
    public void SetBlockRaw(int index, BlockId b) => _blocks[index] = (byte)b;

    public ReadOnlySpan<byte> Blocks => _blocks;

    /// <summary>CON-19: the form of a cell (Full for every natural block, air and out of bounds).</summary>
    public BlockForm FormAt(Int3 c) =>
        _forms.Count != 0 && InBounds(c) && _forms.TryGetValue(Index(c), out var f) ? BlockForm.FromPacked(f) : BlockForm.Full;

    /// <summary>CON-19: sets the form of a solid cell (after <see cref="SetBlock"/>; a block write resets it to Full).
    /// Marks the chunks dirty for the view; solidity is unchanged, so no cell change is recorded. False when out of
    /// bounds, not solid or unchanged.</summary>
    public bool SetForm(Int3 c, BlockForm form)
    {
        if (!InBounds(c) || !IsSolid(c) || FormAt(c) == form) return false;
        int i = Index(c);
        if (form.IsFull) _forms.Remove(i);
        else _forms[i] = form.Packed;
        MarkDirtyAround(c);
        return true;
    }

    /// <summary>CON-19: every cell that is not Full, ascending cell index.</summary>
    public IEnumerable<(int Index, BlockForm Form)> Forms
    {
        get
        {
            foreach (var (i, f) in _forms) yield return (i, BlockForm.FromPacked(f));
        }
    }

    public int FormCount => _forms.Count;

    /// <summary>SaveGame load (after the blocks).</summary>
    internal void RestoreForm(int index, BlockForm form) => _forms[index] = form.Packed;

    /// <summary>CON-19: hashed only when some cell is not Full, so a world without shapes hashes as before M11-T10.</summary>
    public void AddFormsToHash(ref StateHasher h)
    {
        if (_forms.Count == 0) return;
        h.Add(_forms.Count);
        foreach (var (i, f) in _forms) { h.Add(i); h.Add(f); }
    }

    /// <summary>Mutable span for SaveGame load only.</summary>
    internal Span<byte> BlocksMutable => _blocks;

    public int ChunkIndex(int cx, int cy, int cz) => cx + cz * ChunksX + cy * ChunksX * ChunksZ;

    public int ChunkIndexOf(Int3 c) => ChunkIndex(c.X >> ChunkShift, c.Y >> ChunkShift, c.Z >> ChunkShift);

    public (int cx, int cy, int cz) ChunkCoords(int chunkIndex)
    {
        int layer = ChunksX * ChunksZ;
        int cy = chunkIndex / layer;
        int rem = chunkIndex - cy * layer;
        int cz = rem / ChunksX;
        return (rem - cz * ChunksX, cy, cz);
    }

    /// <summary>Cells changed since the last ClearChangeLog (flat indices, in change order). Consumed by water and path grid.</summary>
    public IReadOnlyList<int> ChangedCells => _changedCells;

    /// <summary>Chunks dirtied since the last TakeDirtyChunks, ascending.</summary>
    public List<int> TakeDirtyChunks()
    {
        var result = new List<int>(_dirtyChunks);
        result.Sort();
        foreach (var ci in _dirtyChunks) _chunkDirty[ci] = false;
        _dirtyChunks.Clear();
        return result;
    }

    /// <summary>Number of changes cleared from the log so far. <c>ChangeLogBase + ChangedCells.Count</c> is the total
    /// number of logged changes ever, so a reader with its own cursor (PathGrid, PTH-03) can tell whether entries it
    /// has not seen were cleared.</summary>
    public long ChangeLogBase { get; private set; }

    public void ClearChangeLog()
    {
        ChangeLogBase += _changedCells.Count;
        _changedCells.Clear();
    }

    public void MarkAllDirty()
    {
        for (int i = 0; i < ChunkCount; i++) MarkChunkDirty(i);
    }

    // WLD-03: a change on a chunk border also dirties the neighbor chunk.
    private void MarkDirtyAround(Int3 c)
    {
        MarkChunkDirty(ChunkIndexOf(c));
        foreach (var d in Int3.Neighbors6)
        {
            var n = c + d;
            if (InBounds(n)) MarkChunkDirty(ChunkIndexOf(n));
        }
    }

    private void MarkChunkDirty(int ci)
    {
        if (_chunkDirty[ci]) return;
        _chunkDirty[ci] = true;
        _dirtyChunks.Add(ci);
    }
}
