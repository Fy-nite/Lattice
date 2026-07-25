using System.Text;

namespace lattice.Runtime.Memory;

public sealed class HeapString
{
    public const int HeaderSize = 8;

    private readonly MemoryArena _arena;
    private readonly int _handle;

    public int Handle => _handle;

    public HeapString(MemoryArena arena, int handle)
    {
        _arena = arena;
        _handle = handle;
    }

    public int Length
    {
        get => _arena.ReadInt32(_handle);
        set => _arena.WriteInt32(_handle, value);
    }

    public int ByteCount
    {
        get => _arena.ReadInt32(_handle + 4);
        set => _arena.WriteInt32(_handle + 4, value);
    }

    public string Value
    {
        get
        {
            int len = Length;
            if (len == 0) return string.Empty;
            var bytes = _arena.ReadBytes(_handle + HeaderSize, ByteCount);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public static int AllocationSize(string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        return HeaderSize + byteCount;
    }

    public static HeapString Allocate(MemoryArena arena, string value)
    {
        int charCount = value.Length;
        int byteCount = Encoding.UTF8.GetByteCount(value);
        int size = HeaderSize + byteCount;
        int handle = arena.Malloc(size);

        var str = new HeapString(arena, handle);
        str.Length = charCount;
        str.ByteCount = byteCount;

        var utf8 = Encoding.UTF8.GetBytes(value);
        arena.WriteBytes(handle + HeaderSize, utf8);

        return str;
    }
}
