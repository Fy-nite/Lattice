namespace lattice.Runtime.Memory;

public enum HeapArrayElementKind : byte
{
    Int = 1,
    Float = 2,
    Bool = 3,
    Handle = 4,
}

public sealed class HeapArray
{
    public const int HeaderSize = 12;

    private readonly MemoryArena _arena;
    private readonly int _handle;

    public int Handle => _handle;

    public HeapArray(MemoryArena arena, int handle)
    {
        _arena = arena;
        _handle = handle;
    }

    public int Count
    {
        get => _arena.ReadInt32(_handle);
        set => _arena.WriteInt32(_handle, value);
    }

    public int Capacity
    {
        get => _arena.ReadInt32(_handle + 4);
        set => _arena.WriteInt32(_handle + 4, value);
    }

    public HeapArrayElementKind ElementKind
    {
        get => (HeapArrayElementKind)_arena.ReadByte(_handle + 8);
        set => _arena.WriteByte(_handle + 8, (byte)value);
    }

    private int ElementOffset(int index) => _handle + HeaderSize + (index * FieldValue.SizeOf);

    public FieldValue GetElement(int index)
    {
        if (index < 0 || index >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        int off = ElementOffset(index);
        var kind = (FieldValueKind)_arena.ReadByte(off);
        int bits = _arena.ReadInt32(off + 1);
        return FieldValue.FromRaw(kind, bits);
    }

    public void SetElement(int index, FieldValue value)
    {
        if (index < 0 || index >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(index));

        int off = ElementOffset(index);
        _arena.WriteByte(off, (byte)value.Kind);
        _arena.WriteInt32(off + 1, value.GetRawBits());

        if (index >= Count)
            Count = index + 1;
    }

    public static int AllocationSize(int capacity) => HeaderSize + (capacity * FieldValue.SizeOf);

    public static HeapArray Allocate(MemoryArena arena, HeapArrayElementKind elementKind, int capacity)
    {
        int size = AllocationSize(capacity);
        int handle = arena.Malloc(size);

        var arr = new HeapArray(arena, handle);
        arr.Count = 0;
        arr.Capacity = capacity;
        arr.ElementKind = elementKind;

        return arr;
    }
}
