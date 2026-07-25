namespace lattice.Runtime.Memory;

public enum FieldValueKind : byte
{
    None = 0,
    Int = 1,
    Float = 2,
    Bool = 3,
    Handle = 4,
}

public readonly struct FieldValue : IEquatable<FieldValue>
{
    public const int SizeOf = 8;

    public readonly FieldValueKind Kind;
    private readonly int _bits;

    public static readonly FieldValue Null = new(FieldValueKind.None, 0);

    private FieldValue(FieldValueKind kind, int bits)
    {
        Kind = kind;
        _bits = bits;
    }

    internal static FieldValue FromRaw(FieldValueKind kind, int bits) => new(kind, bits);

    internal int GetRawBits() => _bits;

    public static FieldValue FromInt(int v) => new(FieldValueKind.Int, v);
    public static FieldValue FromFloat(float v) => new(FieldValueKind.Float, BitConverter.SingleToInt32Bits(v));
    public static FieldValue FromBool(bool v) => new(FieldValueKind.Bool, v ? 1 : 0);
    public static FieldValue FromHandle(HeapHandle h) => new(FieldValueKind.Handle, h.Offset);

    public readonly int AsInt => _bits;
    public readonly float AsFloat => BitConverter.Int32BitsToSingle(_bits);
    public readonly bool AsBool => _bits != 0;
    public readonly HeapHandle AsHandle => new(_bits);
    public readonly bool IsNull => Kind == FieldValueKind.None;

    public readonly object? ToObject() => Kind switch
    {
        FieldValueKind.Int => _bits,
        FieldValueKind.Float => AsFloat,
        FieldValueKind.Bool => AsBool,
        FieldValueKind.Handle => AsHandle,
        _ => null,
    };

    public static FieldValue FromObject(object? obj) => obj switch
    {
        int i => FromInt(i),
        float f => FromFloat(f),
        bool b => FromBool(b),
        HeapHandle h => FromHandle(h),
        null => Null,
        _ => Null,
    };

    public bool Equals(FieldValue other) => Kind == other.Kind && _bits == other._bits;
    public override bool Equals(object? obj) => obj is FieldValue fv && Equals(fv);
    public override int GetHashCode() => HashCode.Combine(Kind, _bits);
    public override string ToString() => Kind switch
    {
        FieldValueKind.Int => AsInt.ToString(),
        FieldValueKind.Float => AsFloat.ToString(),
        FieldValueKind.Bool => AsBool.ToString(),
        FieldValueKind.Handle => AsHandle.ToString(),
        _ => "null",
    };

    public static bool operator ==(FieldValue a, FieldValue b) => a.Equals(b);
    public static bool operator !=(FieldValue a, FieldValue b) => !a.Equals(b);
}
