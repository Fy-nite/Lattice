using System.Buffers.Binary;

namespace lattice.Runtime.Memory;

public sealed class MemoryArena : IDisposable
{
    private byte[] _buffer;
    private int _offset;
    private bool _disposed;

    public MemoryArena(int capacityInBytes = 1024 * 1024)
    {
        _buffer = new byte[capacityInBytes];
        _offset = 0;
    }

    public int Malloc(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sizeInBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

        int aligned = (sizeInBytes + 3) & ~3;
        int newOffset = _offset + aligned;
        if (newOffset > _buffer.Length)
            throw new OutOfMemoryException($"Heap arena full: requested {sizeInBytes} bytes, only {_buffer.Length - _offset} free.");

        int handle = _offset;
        _offset = newOffset;
        return handle;
    }

    public int ReadInt32(int handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + 4 > _offset)
            throw new ArgumentOutOfRangeException(nameof(handle));
        return BitConverter.ToInt32(_buffer, handle);
    }

    public void WriteInt32(int handle, int value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + 4 > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(handle));
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(handle), value);
    }

    public float ReadSingle(int handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + 4 > _offset)
            throw new ArgumentOutOfRangeException(nameof(handle));
        return BitConverter.ToSingle(_buffer, handle);
    }

    public void WriteSingle(int handle, float value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + 4 > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(handle));
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(handle), value);
    }

    public byte ReadByte(int handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle >= _offset)
            throw new ArgumentOutOfRangeException(nameof(handle));
        return _buffer[handle];
    }

    public void WriteByte(int handle, byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle >= _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(handle));
        _buffer[handle] = value;
    }

    public void WriteBytes(int handle, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + data.Length > _buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(handle));
        data.CopyTo(_buffer.AsSpan(handle));
    }

    public Span<byte> ReadBytes(int handle, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (handle < 0 || handle + count > _offset)
            throw new ArgumentOutOfRangeException(nameof(handle));
        return new Span<byte>(_buffer, handle, count);
    }

    public void Reset()
    {
        _offset = 0;
    }

    public int UsedBytes => _offset;
    public int FreeBytes => _buffer.Length - _offset;
    public int Capacity => _buffer.Length;

    public void Dispose()
    {
        _disposed = true;
        _buffer = [];
    }
}
