using System.Runtime.CompilerServices;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Core;
using lattice.Throwables;

using System.Buffers;

namespace lattice.Runtime.Compiler;

public static class CompiledExecutor
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StackValue RawToStackValue(object? val) => val switch
    {
        int iv => StackValue.FromInt(iv),
        float fv => StackValue.FromFloat(fv),
        bool bv => StackValue.FromBool(bv),
        _ => StackValue.FromObject(val)
    };

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static StackValue Execute(CompiledMethod cm, StackValue[] args, CPU cpu)
    {
        var code = cm.Code;
        int localCount = cm.LocalCount;

        var locals = new StackValue[localCount];
        Array.Copy(args, locals, Math.Min(args.Length, localCount));

        var s = new StackValue[code.Length + 16];
        int sp = 0;

        int ip = 0;
        int codeLen = code.Length;

        while (ip < codeLen)
        {
            var instr = code[ip];
            switch (instr.Opcode)
            {
                case OpCode.Ldstr:
                    s[sp++] = StackValue.FromObject(cm.StringTable[instr.Operand]);
                    ip++; break;

                case OpCode.LdcI4:
                    s[sp++] = StackValue.FromInt(instr.Operand);
                    ip++; break;

                case OpCode.LdcR4:
                    s[sp++] = StackValue.FromFloat(BitConverter.Int32BitsToSingle(instr.Operand));
                    ip++; break;

                case OpCode.Ldnull:
                    s[sp++] = StackValue.FromObject(null);
                    ip++; break;

                case OpCode.Ldloc:
                    s[sp++] = locals[instr.Operand];
                    ip++; break;

                case OpCode.Stloc:
                    locals[instr.Operand] = s[--sp];
                    ip++; break;

                case OpCode.Ldarg:
                    s[sp++] = args[instr.Operand];
                    ip++; break;

                case OpCode.Starg:
                    args[instr.Operand] = s[--sp];
                    ip++; break;

                case OpCode.Dup:
                    s[sp] = s[sp - 1]; sp++;
                    ip++; break;

                case OpCode.Pop:
                    sp--;
                    ip++; break;

                case OpCode.Ret:
                    return cm.ReturnsValue && sp > 0 ? s[--sp] : default;

                case OpCode.Add:
                case OpCode.Sub:
                case OpCode.Mul:
                case OpCode.Div:
                case OpCode.Rem:
                {
                    var b = s[--sp]; var a = s[--sp];
                    if (a.Kind == StackValueKind.Float || b.Kind == StackValueKind.Float)
                    {
                        float fa = a.Kind == StackValueKind.Float ? a.AsFloat : a.AsInt;
                        float fb = b.Kind == StackValueKind.Float ? b.AsFloat : b.AsInt;
                        s[sp++] = instr.Opcode switch
                        {
                            OpCode.Add => StackValue.FromFloat(fa + fb),
                            OpCode.Sub => StackValue.FromFloat(fa - fb),
                            OpCode.Mul => StackValue.FromFloat(fa * fb),
                            OpCode.Div => StackValue.FromFloat(fa / fb),
                            OpCode.Rem => StackValue.FromFloat(fa % fb),
                            _ => StackValue.FromInt(0)
                        };
                    }
                    else
                    {
                        int ia = a.AsInt; int ib = b.AsInt;
                        s[sp++] = instr.Opcode switch
                        {
                            OpCode.Add => StackValue.FromInt(ia + ib),
                            OpCode.Sub => StackValue.FromInt(ia - ib),
                            OpCode.Mul => StackValue.FromInt(ia * ib),
                            OpCode.Div => StackValue.FromInt(ia / ib),
                            OpCode.Rem => StackValue.FromInt(ia % ib),
                            _ => StackValue.FromInt(0)
                        };
                    }
                    ip++; break;
                }

                case OpCode.Ceq:
                case OpCode.Cne:
                case OpCode.Cgt:
                case OpCode.Clt:
                case OpCode.CgtUn:
                case OpCode.CgeUn:
                {
                    var b = s[--sp]; var a = s[--sp];
                    s[sp++] = CompareNoBox(a, b, instr.Opcode);
                    ip++; break;
                }

                case OpCode.Not:
                {
                    var v = s[--sp];
                    s[sp++] = v.Kind == StackValueKind.Bool
                        ? StackValue.FromBool(!v.AsBool)
                        : StackValue.FromBool(!v.IsTruthy);
                    ip++; break;
                }

                case OpCode.Br:
                    ip = instr.Operand;
                    break;

                case OpCode.Brfalse:
                {
                    bool cond = s[--sp].IsTruthy;
                    ip = cond ? ip + 1 : instr.Operand;
                    break;
                }

                case OpCode.Brtrue:
                {
                    bool cond = s[--sp].IsTruthy;
                    ip = cond ? instr.Operand : ip + 1;
                    break;
                }

                case OpCode.Call:
                {
                    int targetIdx = instr.Operand;
                    var callInstr = targetIdx >= 0 && targetIdx < cm.CallTargets.Length
                        ? cm.CallTargets[targetIdx] : null;

                    if (callInstr != null)
                    {
                        var target = cpu.ResolveMethod(callInstr.Target);
                        if (target != null)
                        {
                            int argCount = target.Parameters.Count;
                            var pooled = ArrayPool<StackValue>.Shared.Rent(argCount);
                            try
                            {
                                for (int i = argCount - 1; i >= 0; i--)
                                    pooled[i] = s[--sp];

                            if (target.NativeImpl != null)
                            {
                                var nativeArgs = ArrayPool<Value<object>>.Shared.Rent(argCount);
                                try
                                {
                                    for (int i = 0; i < argCount; i++)
                                        nativeArgs[i] = new Value<object>(pooled[i].ToObject()!);
                                    var result = target.NativeImpl.Method(nativeArgs);
                                    if (!string.Equals(target.ReturnType.Name, "void", StringComparison.Ordinal) && result != null)
                                    {
                                        var rawResult = result is IValue iv ? iv.GetObjectData() : result;
                                        s[sp++] = RawToStackValue(rawResult);
                                    }
                                }
                                finally { ArrayPool<Value<object>>.Shared.Return(nativeArgs, clearArray: true); }
                            }
                                else
                                {
                                    var compiledTarget = cpu.GetCompiled(target);
                                    if (compiledTarget != null)
                                    {
                                        var result = Execute(compiledTarget, pooled, cpu);
                                        if (compiledTarget.ReturnsValue)
                                            s[sp++] = result;
                                    }
                                }
                            }
                            finally { ArrayPool<StackValue>.Shared.Return(pooled); }
                        }
                    }
                    ip++; break;
                }

                case OpCode.Newobj:
                {
                    int targetIdx = instr.Operand;
                    var newObj = targetIdx >= 0 && targetIdx < cm.NewObjTargets.Length
                        ? cm.NewObjTargets[targetIdx] : null;

                    if (newObj != null)
                    {
                        var instance = new ManagedObject(newObj.Type.Name);
                        if (newObj.Constructor != null)
                        {
                            var ctor = cpu.ResolveMethod(newObj.Constructor);
                            if (ctor != null)
                            {
                                var callArgs = new StackValue[ctor.Parameters.Count];
                                for (int i = ctor.Parameters.Count - 1; i >= 0; i--)
                                    callArgs[i] = s[--sp];
                                var compiledCtor = cpu.GetCompiled(ctor);
                                if (compiledCtor != null)
                                    Execute(compiledCtor, callArgs, cpu);
                            }
                        }
                        s[sp++] = StackValue.FromObject(instance);
                    }
                    ip++; break;
                }

                case OpCode.Ldfld:
                {
                    var instance = s[--sp].AsObject as ManagedObject;
                    var fieldName = cm.StringTable[instr.Operand];
                    if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                    s[sp++] = RawToStackValue(instance?.GetField(fieldName));
                    ip++; break;
                }

                case OpCode.Stfld:
                {
                    var value = s[--sp];
                    var instance = s[--sp].AsObject as ManagedObject;
                    var fieldName = cm.StringTable[instr.Operand];
                    if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                    instance?.SetField(fieldName, value.ToObject());
                    ip++; break;
                }

                default:
                    ip++; break;
            }
            }
            return default;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static StackValue CompareNoBox(StackValue a, StackValue b, OpCode op)
    {
        bool result;

        // Same-kind primitive comparisons: no boxing needed
        if (a.Kind == StackValueKind.Int && b.Kind == StackValueKind.Int)
        {
            int ia = a.AsInt, ib = b.AsInt;
            result = op switch
            {
                OpCode.Ceq => ia == ib,
                OpCode.Cne => ia != ib,
                OpCode.Cgt => ia > ib,
                OpCode.Clt => ia < ib,
                OpCode.CgtUn => ((uint)ia) > ((uint)ib),
                OpCode.CgeUn => ((uint)ia) >= ((uint)ib),
                _ => false
            };
            return StackValue.FromBool(result);
        }

        if (a.Kind == StackValueKind.Float && b.Kind == StackValueKind.Float)
        {
            float fa = a.AsFloat, fb = b.AsFloat;
            if (op == OpCode.Ceq || op == OpCode.Cne)
            {
                // NaN: NaN != NaN per IEC 60559
                bool eq = BitConverter.SingleToInt32Bits(fa) == BitConverter.SingleToInt32Bits(fb);
                result = op == OpCode.Ceq ? eq : !eq;
            }
            else
            {
                bool nan = float.IsNaN(fa) || float.IsNaN(fb);
                result = op switch
                {
                    OpCode.Cgt => !nan && fa > fb,
                    OpCode.Clt => !nan && fa < fb,
                    OpCode.CgtUn => nan || fa > fb,
                    OpCode.CgeUn => nan || fa >= fb,
                    _ => false
                };
            }
            return StackValue.FromBool(result);
        }

        // Fall back to boxed comparison for mixed or object types
        var aObj = a.ToObject();
        var bObj = b.ToObject();

        if (op == OpCode.Ceq) return StackValue.FromBool(Equals(aObj, bObj));
        if (op == OpCode.Cne) return StackValue.FromBool(!Equals(aObj, bObj));

        int cmp = aObj is IComparable ca ? ca.CompareTo(bObj) : 0;
        result = op switch
        {
            OpCode.Cgt => cmp > 0,
            OpCode.Clt => cmp < 0,
            OpCode.CgtUn => CompareUnsigned(aObj, bObj) > 0,
            OpCode.CgeUn => CompareUnsigned(aObj, bObj) >= 0,
            _ => false
        };
        return StackValue.FromBool(result);
    }
}
