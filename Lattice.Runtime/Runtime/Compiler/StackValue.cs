using System.Runtime.CompilerServices;

namespace lattice.Runtime.Compiler;

public enum StackValueKind : byte
{
    None,
    Int,
    Float,
    Bool,
    Object
}

public struct StackValue
{
    public StackValueKind Kind;
    private int _intValue;
    private object? _objectValue;

    private StackValue(StackValueKind kind, int intValue, object? objectValue)
    {
        Kind = kind;
        _intValue = intValue;
        _objectValue = objectValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackValue FromInt(int value) => new(StackValueKind.Int, value, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackValue FromFloat(float value) => new(StackValueKind.Float, BitConverter.SingleToInt32Bits(value), null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackValue FromBool(bool value) => new(StackValueKind.Bool, value ? 1 : 0, null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StackValue FromObject(object? value) => new(StackValueKind.Object, 0, value);

    public readonly int AsInt
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _intValue;
    }

    public readonly float AsFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitConverter.Int32BitsToSingle(_intValue);
    }

    public readonly bool AsBool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _intValue != 0;
    }

    public readonly object? AsObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _objectValue;
    }

    public readonly bool IsTruthy => Kind switch
    {
        StackValueKind.Bool => _intValue != 0,
        StackValueKind.Int => _intValue != 0,
        StackValueKind.Float => AsFloat != 0.0f,
        StackValueKind.Object => _objectValue != null,
        _ => false
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly object? ToObject() => Kind switch
    {
        StackValueKind.Int => _intValue,
        StackValueKind.Float => AsFloat,
        StackValueKind.Bool => AsBool,
        StackValueKind.Object => _objectValue,
        _ => null
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTruthyValue(StackValue v) => v.IsTruthy;

    public override readonly string ToString() => Kind switch
    {
        StackValueKind.Int => _intValue.ToString(),
        StackValueKind.Float => AsFloat.ToString(),
        StackValueKind.Bool => AsBool.ToString(),
        StackValueKind.Object => _objectValue?.ToString() ?? "null",
        _ => "none"
    };
}
