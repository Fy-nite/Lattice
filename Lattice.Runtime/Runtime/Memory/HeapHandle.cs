namespace lattice.Runtime.Memory;

public readonly struct HeapHandle : IEquatable<HeapHandle>
{
    public readonly int Offset;

    public static readonly HeapHandle Null = new(-1);

    public HeapHandle(int offset) => Offset = offset;
    public bool IsNull => Offset < 0;

    public bool Equals(HeapHandle other) => Offset == other.Offset;
    public override bool Equals(object? obj) => obj is HeapHandle h && Equals(h);
    public override int GetHashCode() => Offset;
    public override string ToString() => IsNull ? "null" : $"HeapHandle({Offset})";

    public static bool operator ==(HeapHandle a, HeapHandle b) => a.Offset == b.Offset;
    public static bool operator !=(HeapHandle a, HeapHandle b) => a.Offset != b.Offset;
}
