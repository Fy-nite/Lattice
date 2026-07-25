namespace lattice.Runtime.Memory;

public sealed class HeapObject
{
    public const int HeaderSize = 8;
    public const int FieldSize = 8;

    private readonly MemoryArena _arena;
    private readonly int _handle;

    public int Handle => _handle;

    public HeapObject(MemoryArena arena, int handle)
    {
        _arena = arena;
        _handle = handle;
    }

    public int TypeId
    {
        get => _arena.ReadInt32(_handle);
        set => _arena.WriteInt32(_handle, value);
    }

    public int FieldCount
    {
        get => _arena.ReadInt32(_handle + 4);
        set => _arena.WriteInt32(_handle + 4, value);
    }

    private int FieldOffset(int fieldIndex) => _handle + HeaderSize + (fieldIndex * FieldSize);

    public FieldValue GetField(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= FieldCount)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex));

        int off = FieldOffset(fieldIndex);
        var kind = (FieldValueKind)_arena.ReadByte(off);
        int bits = _arena.ReadInt32(off + 1);
        return FieldValue.FromRaw(kind, bits);
    }

    public void SetField(int fieldIndex, FieldValue value)
    {
        if (fieldIndex < 0 || fieldIndex >= FieldCount)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex));

        int off = FieldOffset(fieldIndex);
        _arena.WriteByte(off, (byte)value.Kind);
        _arena.WriteInt32(off + 1, value.GetRawBits());
    }

    public static int AllocationSize(int fieldCount) => HeaderSize + (fieldCount * FieldSize);

    public static HeapObject Allocate(MemoryArena arena, int typeId, int fieldCount)
    {
        int size = AllocationSize(fieldCount);
        int handle = arena.Malloc(size);
        var obj = new HeapObject(arena, handle);
        obj.TypeId = typeId;
        obj.FieldCount = fieldCount;

        for (int i = 0; i < fieldCount; i++)
        {
            int off = handle + HeaderSize + (i * FieldSize);
            arena.WriteByte(off, (byte)FieldValueKind.None);
            arena.WriteInt32(off + 1, 0);
        }

        return obj;
    }
}
