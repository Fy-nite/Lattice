using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using IrOpCode = global::ObjectIR.Core.Ast.OpCode;
using lattice.Core;

namespace lattice.Runtime.Compiler;

public delegate StackValue JittedMethod(StackValue[] args, CPU cpu, CompiledMethod cm);

public static class JitCompiler
{
    private static readonly Dictionary<CompiledMethod, JittedMethod> _cache = new();

    private static readonly MethodInfo _add = GetHelper("Add", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _sub = GetHelper("Sub", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _mul = GetHelper("Mul", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _div = GetHelper("Div", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _rem = GetHelper("Rem", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _ceq = GetHelper("Ceq", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _cne = GetHelper("Cne", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _cgt = GetHelper("Cgt", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _clt = GetHelper("Clt", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _cgtUn = GetHelper("CgtUn", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _cgeUn = GetHelper("CgeUn", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _pushInt = GetHelper("PushInt", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(int)]);
    private static readonly MethodInfo _pushFloat = GetHelper("PushFloat", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(float)]);
    private static readonly MethodInfo _pushString = GetHelper("PushString", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(string)]);
    private static readonly MethodInfo _pushObj = GetHelper("PushObj", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(object)]);
    private static readonly MethodInfo _pop = GetHelper("Pop", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _dup = GetHelper("Dup", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _ldloc = GetHelper("Ldloc", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(StackValue[]), typeof(int)]);
    private static readonly MethodInfo _stloc = GetHelper("Stloc", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(StackValue[]), typeof(int)]);
    private static readonly MethodInfo _ldarg = GetHelper("Ldarg", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(StackValue[]), typeof(int)]);
    private static readonly MethodInfo _starg = GetHelper("Starg", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(StackValue[]), typeof(int)]);
    private static readonly MethodInfo _brfalse = GetHelper("Brfalse", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _brtrue = GetHelper("Brtrue", [typeof(StackValue[]), typeof(int).MakeByRefType()]);
    private static readonly MethodInfo _executeSub = GetHelper("ExecuteSub", [typeof(StackValue[]), typeof(int).MakeByRefType(), typeof(CPU), typeof(CompiledMethod), typeof(int)]);

    private static MethodInfo GetHelper(string name, Type[] paramTypes) =>
        typeof(JitHelper).GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, paramTypes, null)!;

    public static JittedMethod? GetOrCompile(CompiledMethod cm)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(cm, out var existing))
                return existing;
            var jit = Compile(cm);
            if (jit != null)
                _cache[cm] = jit;
            return jit;
        }
    }

    private static JittedMethod? Compile(CompiledMethod cm)
    {
        try
        {
            var cmCaptured = cm; // captured for delegate
            var dm = new DynamicMethod(
                $"jit_{cm.Name}",
                typeof(StackValue),
                [typeof(StackValue[]), typeof(CPU), typeof(CompiledMethod)],
                typeof(JitCompiler).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();

            // Locals: stackArr, sp, localsArr, compiled method ref
            var sArr = il.DeclareLocal(typeof(StackValue[]));
            var sp = il.DeclareLocal(typeof(int));
            var lArr = il.DeclareLocal(typeof(StackValue[]));
            var retVal = il.DeclareLocal(typeof(StackValue));

            // Initialize stack
            il.Emit(OpCodes.Ldc_I4, cm.Code.Length + 16);
            il.Emit(OpCodes.Newarr, typeof(StackValue));
            il.Emit(OpCodes.Stloc, sArr);

            // Initialize locals from args
            il.Emit(OpCodes.Ldc_I4, cm.LocalCount);
            il.Emit(OpCodes.Newarr, typeof(StackValue));
            il.Emit(OpCodes.Stloc, lArr);
            for (int i = 0; i < Math.Min(cm.ArgCount, cm.LocalCount); i++)
            {
                il.Emit(OpCodes.Ldloc, lArr);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem, typeof(StackValue));
                il.Emit(OpCodes.Stelem, typeof(StackValue));
            }

            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, sp);

            var labels = new Label[cm.Code.Length];
            for (int i = 0; i < cm.Code.Length; i++)
                labels[i] = il.DefineLabel();
            var retLabel = il.DefineLabel();

            for (int idx = 0; idx < cm.Code.Length; idx++)
            {
                il.MarkLabel(labels[idx]);
                var instr = cm.Code[idx];

                switch (instr.Opcode)
                {
                    case IrOpCode.LdcI4:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.EmitCall(OpCodes.Call, _pushInt, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.LdcR4:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldc_R4, BitConverter.Int32BitsToSingle(instr.Operand));
                        il.EmitCall(OpCodes.Call, _pushFloat, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ldstr:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldstr, cm.StringTable[instr.Operand]);
                        il.EmitCall(OpCodes.Call, _pushString, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ldnull:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldnull);
                        il.EmitCall(OpCodes.Call, _pushObj, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ldloc:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldloc, lArr);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.EmitCall(OpCodes.Call, _ldloc, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Stloc:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldloc, lArr);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.EmitCall(OpCodes.Call, _stloc, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ldarg:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.EmitCall(OpCodes.Call, _ldarg, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Starg:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.EmitCall(OpCodes.Call, _starg, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Dup:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.EmitCall(OpCodes.Call, _dup, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Pop:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.EmitCall(OpCodes.Call, _pop, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ret:
                        if (cm.ReturnsValue)
                        {
                            il.Emit(OpCodes.Ldloc, sArr);
                            il.Emit(OpCodes.Ldloca, sp);
                            il.EmitCall(OpCodes.Call, _pop, null);
                            il.Emit(OpCodes.Stloc, retVal);
                        }
                        il.Emit(OpCodes.Br, retLabel);
                        break;

                    case IrOpCode.Add:
                        CallArith(il, sArr, sp, _add);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Sub:
                        CallArith(il, sArr, sp, _sub);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Mul:
                        CallArith(il, sArr, sp, _mul);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Div:
                        CallArith(il, sArr, sp, _div);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Rem:
                        CallArith(il, sArr, sp, _rem);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Ceq:
                        CallArith(il, sArr, sp, _ceq);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Cne:
                        CallArith(il, sArr, sp, _cne);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Cgt:
                        CallArith(il, sArr, sp, _cgt);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.Clt:
                        CallArith(il, sArr, sp, _clt);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.CgtUn:
                        CallArith(il, sArr, sp, _cgtUn);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                    case IrOpCode.CgeUn:
                        CallArith(il, sArr, sp, _cgeUn);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Br:
                        il.Emit(OpCodes.Br, labels[instr.Operand]);
                        break;

                    case IrOpCode.Brfalse:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.EmitCall(OpCodes.Call, _brfalse, null);
                        il.Emit(OpCodes.Brtrue, labels[instr.Operand]);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Brtrue:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.EmitCall(OpCodes.Call, _brtrue, null);
                        il.Emit(OpCodes.Brtrue, labels[instr.Operand]);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    case IrOpCode.Call:
                        il.Emit(OpCodes.Ldloc, sArr);
                        il.Emit(OpCodes.Ldloca, sp);
                        il.Emit(OpCodes.Ldarg_1); // CPU
                        il.Emit(OpCodes.Ldarg_2); // cm (CompiledMethod)
                        il.Emit(OpCodes.Ldc_I4, instr.Operand); // target index
                        il.EmitCall(OpCodes.Call, _executeSub, null);
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;

                    default:
                        GotoNext(il, idx, cm.Code.Length, labels);
                        break;
                }
            }

            il.MarkLabel(retLabel);
            il.Emit(OpCodes.Ldloc, retVal);
            il.Emit(OpCodes.Ret);

            var del = (Func<StackValue[], CPU, CompiledMethod, StackValue>)dm.CreateDelegate(
                typeof(Func<StackValue[], CPU, CompiledMethod, StackValue>));
            return (args, cpu, _) => del(args, cpu, cmCaptured);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[JIT] Failed to compile {cm.Name}: {ex.Message}");
            return null;
        }
    }

    private static void CallArith(ILGenerator il, LocalBuilder sArr, LocalBuilder sp, MethodInfo helper)
    {
        il.Emit(OpCodes.Ldloc, sArr);
        il.Emit(OpCodes.Ldloca, sp);
        il.EmitCall(OpCodes.Call, helper, null);
    }

    private static void GotoNext(ILGenerator il, int idx, int len, Label[] labels)
    {
        if (idx + 1 < len)
            il.Emit(OpCodes.Br, labels[idx + 1]);
    }
}

// All stack operations delegated to verified C# helpers
file static class JitHelper
{
    // Use JitHelper's own static methods (defined below)
    // Renamed to avoid conflict with methods that take StackValue[] by ref
    private static StackValue Add2(StackValue a, StackValue b) => Add(a, b);
    private static StackValue Sub2(StackValue a, StackValue b) => Sub(a, b);
    private static StackValue Mul2(StackValue a, StackValue b) => Mul(a, b);
    private static StackValue Div2(StackValue a, StackValue b) => Div(a, b);
    private static StackValue Rem2(StackValue a, StackValue b) => Rem(a, b);
    private static StackValue Ceq2(StackValue a, StackValue b) => Ceq(a, b);
    private static StackValue Cne2(StackValue a, StackValue b) => Cne(a, b);
    private static StackValue Cgt2(StackValue a, StackValue b) => Cgt(a, b);
    private static StackValue Clt2(StackValue a, StackValue b) => Clt(a, b);
    private static StackValue CgtUn2(StackValue a, StackValue b) => CgtUn(a, b);
    private static StackValue CgeUn2(StackValue a, StackValue b) => CgeUn(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static StackValue Arith(StackValue a, StackValue b, Func<int, int, int> intOp, Func<float, float, float> floatOp)
    {
        if (a.Kind == StackValueKind.Float || b.Kind == StackValueKind.Float)
            return StackValue.FromFloat(floatOp(a.Kind == StackValueKind.Float ? a.AsFloat : a.AsInt,
                                               b.Kind == StackValueKind.Float ? b.AsFloat : b.AsInt));
        return StackValue.FromInt(intOp(a.AsInt, b.AsInt));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static StackValue Cmp(StackValue a, StackValue b, Func<int, int, bool> intCmp, Func<float, float, bool> floatCmp, Func<object?, object?, bool> objCmp)
    {
        if (a.Kind == StackValueKind.Int && b.Kind == StackValueKind.Int) return StackValue.FromBool(intCmp(a.AsInt, b.AsInt));
        if (a.Kind == StackValueKind.Float && b.Kind == StackValueKind.Float) return StackValue.FromBool(floatCmp(a.AsFloat, b.AsFloat));
        return StackValue.FromBool(objCmp(a.ToObject(), b.ToObject()));
    }

    public static StackValue Add(StackValue a, StackValue b) => Arith(a, b, (x, y) => x + y, (x, y) => x + y);
    public static StackValue Sub(StackValue a, StackValue b) => Arith(a, b, (x, y) => x - y, (x, y) => x - y);
    public static StackValue Mul(StackValue a, StackValue b) => Arith(a, b, (x, y) => x * y, (x, y) => x * y);
    public static StackValue Div(StackValue a, StackValue b) => Arith(a, b, (x, y) => x / y, (x, y) => x / y);
    public static StackValue Rem(StackValue a, StackValue b) => Arith(a, b, (x, y) => x % y, (x, y) => x % y);
    public static StackValue Ceq(StackValue a, StackValue b) => Cmp(a, b, (x, y) => x == y, (x, y) => x == y, (x, y) => Equals(x, y));
    public static StackValue Cne(StackValue a, StackValue b) => Cmp(a, b, (x, y) => x != y, (x, y) => x != y, (x, y) => !Equals(x, y));
    public static StackValue Cgt(StackValue a, StackValue b) => Cmp(a, b, (x, y) => x > y, (x, y) => x > y, (x, y) => x is IComparable c && c.CompareTo(y) > 0);
    public static StackValue Clt(StackValue a, StackValue b) => Cmp(a, b, (x, y) => x < y, (x, y) => x < y, (x, y) => x is IComparable c && c.CompareTo(y) < 0);
    public static StackValue CgtUn(StackValue a, StackValue b) => Cmp(a, b, (x, y) => ((uint)x) > ((uint)y),
        (x, y) => float.IsNaN(x) || float.IsNaN(y) || x > y,
        (x, y) => CompareUnsigned(x, y) > 0);
    public static StackValue CgeUn(StackValue a, StackValue b) => Cmp(a, b, (x, y) => ((uint)x) >= ((uint)y),
        (x, y) => float.IsNaN(x) || float.IsNaN(y) || x >= y,
        (x, y) => CompareUnsigned(x, y) >= 0);

    private static int CompareUnsigned(object? a, object? b)
    {
        if (b is null) return a is null ? 0 : 1;
        if (a is null) return -1;
        if (a is double da && b is double db) { if (double.IsNaN(da) || double.IsNaN(db)) return 1; return da.CompareTo(db); }
        if (a is float fa && b is float fb) { if (float.IsNaN(fa) || float.IsNaN(fb)) return 1; return fa.CompareTo(fb); }
        if (a is int ia && b is int ib) return ((uint)ia).CompareTo((uint)ib);
        if (a is IComparable ca) return ca.CompareTo(b);
        return 0;
    }
    // Stack pointer is passed by ref so helpers can update it directly

    public static void Push(StackValue[] s, ref int sp, StackValue v) { s[sp++] = v; }
    public static void PushInt(StackValue[] s, ref int sp, int v) { s[sp++] = StackValue.FromInt(v); }
    public static void PushFloat(StackValue[] s, ref int sp, float v) { s[sp++] = StackValue.FromFloat(v); }
    public static void PushString(StackValue[] s, ref int sp, string v) { s[sp++] = StackValue.FromObject(v); }
    public static void PushObj(StackValue[] s, ref int sp, object? v) { s[sp++] = StackValue.FromObject(v); }

    public static StackValue Pop(StackValue[] s, ref int sp) => s[--sp];
    public static StackValue Peek(StackValue[] s, int sp) => s[sp - 1];

    public static void Dup(StackValue[] s, ref int sp) { s[sp] = s[sp - 1]; sp++; }

    public static void Ldloc(StackValue[] s, ref int sp, StackValue[] locals, int idx) { s[sp++] = locals[idx]; }
    public static void Stloc(StackValue[] s, ref int sp, StackValue[] locals, int idx) { locals[idx] = s[--sp]; }
    public static void Ldarg(StackValue[] s, ref int sp, StackValue[] args, int idx) { s[sp++] = args[idx]; }
    public static void Starg(StackValue[] s, ref int sp, StackValue[] args, int idx) { args[idx] = s[--sp]; }

    public static bool Brfalse(StackValue[] s, ref int sp) => !s[--sp].IsTruthy;
    public static bool Brtrue(StackValue[] s, ref int sp) => s[--sp].IsTruthy;

    public static void Add(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Add2(a, b);
    }
    public static void Sub(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Sub2(a, b);
    }
    public static void Mul(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Mul2(a, b);
    }
    public static void Div(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Div2(a, b);
    }
    public static void Rem(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Rem2(a, b);
    }
    public static void Ceq(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Ceq2(a, b);
    }
    public static void Cne(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Cne2(a, b);
    }
    public static void Cgt(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Cgt2(a, b);
    }
    public static void Clt(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = Clt2(a, b);
    }
    public static void CgtUn(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = CgtUn2(a, b);
    }
    public static void CgeUn(StackValue[] s, ref int sp)
    {
        var b = s[--sp]; var a = s[--sp];
        s[sp++] = CgeUn2(a, b);
    }

    public static void ExecuteSub(StackValue[] s, ref int sp, CPU cpu, CompiledMethod cm, int targetIdx)
    {
        var callInstr = targetIdx >= 0 && targetIdx < cm.CallTargets.Length ? cm.CallTargets[targetIdx] : null;
        if (callInstr == null) return;

        var target = cpu.ResolveMethod(callInstr.Target);
        if (target == null) return;

        int argCount = target.Parameters.Count;
        var pooled = System.Buffers.ArrayPool<StackValue>.Shared.Rent(argCount);
        try
        {
            for (int i = argCount - 1; i >= 0; i--)
                pooled[i] = s[--sp];

            if (target.NativeImpl != null)
            {
                var nativeArgs = System.Buffers.ArrayPool<Value<object>>.Shared.Rent(argCount);
                try
                {
                    for (int i = 0; i < argCount; i++)
                        nativeArgs[i] = new Value<object>(pooled[i].ToObject()!);
                    var result = target.NativeImpl.Method(nativeArgs);
                    if (!string.Equals(target.ReturnType.Name, "void", StringComparison.Ordinal) && result != null)
                    {
                        var rawResult = result is IValue iv ? iv.GetObjectData() : result;
                        s[sp++] = CompiledExecutor.RawToStackValue(rawResult);
                    }
                }
                finally { System.Buffers.ArrayPool<Value<object>>.Shared.Return(nativeArgs, clearArray: true); }
            }
            else
            {
                var compiledTarget = cpu.GetCompiled(target);
                if (compiledTarget != null)
                {
                    var result = CompiledExecutor.Execute(compiledTarget, pooled, cpu);
                    if (compiledTarget.ReturnsValue)
                        s[sp++] = result;
                }
            }
        }
        finally { System.Buffers.ArrayPool<StackValue>.Shared.Return(pooled); }
    }
}
