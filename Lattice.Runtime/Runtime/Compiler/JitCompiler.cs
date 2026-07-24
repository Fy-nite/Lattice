using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using IrOpCode = global::ObjectIR.Core.Ast.OpCode;

namespace lattice.Runtime.Compiler;

public delegate StackValue JittedMethod(StackValue[] args, CPU cpu, CompiledMethod cm);

public static class JitCompiler
{
    private static readonly ConcurrentDictionary<CompiledMethod, JittedMethod?> _cache = new();

    private static readonly MethodInfo _unboxArg = typeof(JitCompiler).GetMethod(nameof(UnboxArg), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo _boxInt = typeof(JitCompiler).GetMethod(nameof(BoxInt), BindingFlags.Static | BindingFlags.NonPublic)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int UnboxArg(StackValue[] args, int i) => args[i].AsInt;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StackValue BoxInt(int v) => StackValue.FromInt(v);

    public static JittedMethod? GetOrCompile(CompiledMethod cm)
    {
        return _cache.GetOrAdd(cm, static (CompiledMethod cm, object? _) => Compile(cm), null);
    }

    private static JittedMethod? Compile(CompiledMethod cm)
    {
        try
        {
            var method = cm.SourceMethod;
            if (method.NativeImpl != null) return null;
            if (!CanEmitIntOnly(cm, method)) return null;
            return EmitIntOnly(cm, method);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[JIT] Failed: {cm.Name}: {ex.Message}");
            return null;
        }
    }

    private static bool CanEmitIntOnly(CompiledMethod cm, MethodNode method)
    {
        var retName = method.ReturnType.Name;
        if (retName != "void" && retName != "int32" && retName != "bool")
            return false;

        foreach (var p in method.Parameters)
        {
            var tn = p.ParameterType.Name;
            if (tn != "int32" && tn != "bool") return false;
        }

        foreach (var l in method.Locals)
        {
            var tn = l.LocalType.Name;
            if (tn != "int32" && tn != "bool") return false;
        }

        foreach (var instr in cm.Code)
        {
            switch (instr.Opcode)
            {
                case IrOpCode.Ldstr:
                case IrOpCode.Ldnull:
                case IrOpCode.Newobj:
                case IrOpCode.Ldfld:
                case IrOpCode.Stfld:
                case IrOpCode.Call:
                case IrOpCode.LdcR4:
                    return false;
            }
        }

        return true;
    }

    private static int ComputeMaxStack(CompiledMethod cm)
    {
        int max = 0, sp = 0;
        foreach (var instr in cm.Code)
        {
            switch (instr.Opcode)
            {
                case IrOpCode.LdcI4:
                case IrOpCode.Ldloc:
                case IrOpCode.Ldarg:
                    sp++;
                    if (sp > max) max = sp;
                    break;
                case IrOpCode.Dup:
                    sp++;
                    if (sp > max) max = sp;
                    break;
                case IrOpCode.Add:
                case IrOpCode.Sub:
                case IrOpCode.Mul:
                case IrOpCode.Div:
                case IrOpCode.Rem:
                case IrOpCode.Ceq:
                case IrOpCode.Cne:
                case IrOpCode.Cgt:
                case IrOpCode.Clt:
                case IrOpCode.CgtUn:
                case IrOpCode.CgeUn:
                    sp--;
                    break;
                case IrOpCode.Stloc:
                case IrOpCode.Starg:
                case IrOpCode.Pop:
                case IrOpCode.Brfalse:
                case IrOpCode.Brtrue:
                    sp--;
                    break;
                case IrOpCode.Not:
                    break;
                case IrOpCode.Ret:
                    if (cm.ReturnsValue) sp--;
                    break;
            }
        }
        return max;
    }

    private static JittedMethod EmitIntOnly(CompiledMethod cm, MethodNode method)
    {
        int argCount = cm.ArgCount;
        int localCount = cm.LocalCount;
        int codeLen = cm.Code.Length;
        bool hasReturn = cm.ReturnsValue;
        int STACK_BASE = 1 + argCount + localCount;
        int maxStack = Math.Max(ComputeMaxStack(cm), 1);

        var dm = new DynamicMethod(
            $"jit_{cm.Name}",
            typeof(StackValue),
            [typeof(StackValue[]), typeof(CPU), typeof(CompiledMethod)],
            typeof(JitCompiler).Module,
            skipVisibility: true);

        var il = dm.GetILGenerator();

        il.DeclareLocal(typeof(StackValue));
        for (int i = 0; i < argCount; i++) il.DeclareLocal(typeof(int));
        for (int i = 0; i < localCount; i++) il.DeclareLocal(typeof(int));
        for (int i = 0; i < maxStack; i++) il.DeclareLocal(typeof(int));

        var branchTargets = new HashSet<int>();
        foreach (var instr in cm.Code)
        {
            if (instr.Opcode is IrOpCode.Br or IrOpCode.Brfalse or IrOpCode.Brtrue)
                branchTargets.Add(instr.Operand);
        }

        var labels = new Label[codeLen];
        for (int i = 0; i < codeLen; i++)
            labels[i] = branchTargets.Contains(i) ? il.DefineLabel() : default;
        var retLabel = il.DefineLabel();

        for (int i = 0; i < argCount; i++)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, i);
            il.Emit(OpCodes.Call, _unboxArg);
            il.Emit(OpCodes.Stloc, 1 + i);
        }

        int sp = 0;

        for (int idx = 0; idx < codeLen; idx++)
        {
            if (branchTargets.Contains(idx))
                il.MarkLabel(labels[idx]);

            var instr = cm.Code[idx];

            switch (instr.Opcode)
            {
                case IrOpCode.LdcI4:
                    il.Emit(OpCodes.Ldc_I4, instr.Operand);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                    sp++;
                    break;

                case IrOpCode.Ldloc:
                    il.Emit(OpCodes.Ldloc, 1 + argCount + instr.Operand);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                    sp++;
                    break;

                case IrOpCode.Stloc:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Stloc, 1 + argCount + instr.Operand);
                    break;

                case IrOpCode.Ldarg:
                    il.Emit(OpCodes.Ldloc, 1 + instr.Operand);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                    sp++;
                    break;

                case IrOpCode.Starg:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Stloc, 1 + instr.Operand);
                    break;

                case IrOpCode.Dup:
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                    sp++;
                    break;

                case IrOpCode.Pop:
                    sp--;
                    break;

                case IrOpCode.Ret:
                    if (hasReturn)
                    {
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _boxInt);
                        il.Emit(OpCodes.Stloc, 0);
                    }
                    il.Emit(OpCodes.Br, retLabel);
                    break;

                case IrOpCode.Add:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Sub:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Sub);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Mul:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Mul);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Div:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Div);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Rem:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Rem);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Ceq:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Cne:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Cgt:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Cgt);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Clt:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Clt);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.CgtUn:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Cgt_Un);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.CgeUn:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Clt_Un);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Not:
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                    break;

                case IrOpCode.Br:
                    il.Emit(OpCodes.Br, labels[instr.Operand]);
                    break;

                case IrOpCode.Brfalse:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Brfalse, labels[instr.Operand]);
                    break;

                case IrOpCode.Brtrue:
                    sp--;
                    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                    il.Emit(OpCodes.Brtrue, labels[instr.Operand]);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported opcode in IntOnly JIT: {instr.Opcode}");
            }
        }

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ldloc, 0);
        il.Emit(OpCodes.Ret);

        var cmCaptured = cm;
        var del = dm.CreateDelegate<Func<StackValue[], CPU, CompiledMethod, StackValue>>();
        return (args, cpu, _) => del(args, cpu, cmCaptured);
    }
}
