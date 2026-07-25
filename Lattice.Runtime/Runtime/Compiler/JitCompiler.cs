using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using ObjectIR.StdLib.Core.Memory;
using lattice.Core;
using IrOpCode = global::ObjectIR.Core.Ast.OpCode;

namespace lattice.Runtime.Compiler;

public delegate object? JittedMethod(object?[] args, CPU cpu, CompiledMethod cm);

public static class JitCompiler
{
    private static readonly MethodInfo _unboxArg = typeof(JitCompiler).GetMethod(nameof(UnboxArg), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo _boxInt = typeof(JitCompiler).GetMethod(nameof(BoxInt), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo _boxObject = typeof(JitCompiler).GetMethod(nameof(BoxObject), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo _isTruthy = typeof(JitCompiler).GetMethod(nameof(JitIsTruthy), BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo _jitAdd = typeof(JitCompiler).GetMethod(nameof(JitAdd), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitSub = typeof(JitCompiler).GetMethod(nameof(JitSub), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitMul = typeof(JitCompiler).GetMethod(nameof(JitMul), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitDiv = typeof(JitCompiler).GetMethod(nameof(JitDiv), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitRem = typeof(JitCompiler).GetMethod(nameof(JitRem), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitNeg = typeof(JitCompiler).GetMethod(nameof(JitNeg), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitCompare = typeof(JitCompiler).GetMethod(nameof(JitCompare), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitNot = typeof(JitCompiler).GetMethod(nameof(JitNot), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitLdfld = typeof(JitCompiler).GetMethod(nameof(JitLdfld), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitStfld = typeof(JitCompiler).GetMethod(nameof(JitStfld), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitNewobj = typeof(JitCompiler).GetMethod(nameof(JitNewobj), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitCall = typeof(JitCompiler).GetMethod(nameof(JitCall), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitCallSimple = typeof(JitCompiler).GetMethod(nameof(JitCallSimple), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo _jitNewobjSimple = typeof(JitCompiler).GetMethod(nameof(JitNewobjSimple), BindingFlags.Static | BindingFlags.Public)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object? UnboxArg(object[] args, int i) => i < args.Length ? args[i] : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object BoxInt(int v) => v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static object BoxObject(object? v) => v!;

    public static JittedMethod? GetOrCompile(CompiledMethod cm)
    {
        var method = cm.SourceMethod;
        if (method.NativeImpl != null) return null;
        if (!CanEmitIntOnly(cm, method)) return EmitGeneral(cm, method);
        return EmitIntOnly(cm, method) ?? EmitGeneral(cm, method);
    }

    private static JittedMethod? EmitIntOnly(CompiledMethod cm, MethodNode method)
    {
        try
        {
            int argCount = cm.ArgCount;
            int localCount = cm.LocalCount;
            int codeLen = cm.Code.Length;
            bool hasReturn = cm.ReturnsValue;
            int STACK_BASE = 1 + argCount + localCount;
            int maxStack = Math.Max(ComputeMaxStack(cm), 1);

            var dm = new DynamicMethod(
                $"jit_intonly_{cm.Name}",
                typeof(object),
                [typeof(object?[]), typeof(CPU), typeof(CompiledMethod)],
                typeof(JitCompiler).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();

            il.DeclareLocal(typeof(object));
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
                il.Emit(OpCodes.Unbox_Any, typeof(int));
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
                            il.Emit(OpCodes.Box, typeof(int));
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
                        return null;
                }
            }

            il.MarkLabel(retLabel);
            il.Emit(OpCodes.Ldloc, 0);
            il.Emit(OpCodes.Ret);

            var cmCaptured = cm;
            var del = dm.CreateDelegate<Func<object?[], CPU, CompiledMethod, object?>>();
            return (args, cpu, _) => del(args, cpu, cmCaptured);
        }
        catch
        {
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
                case IrOpCode.LdcR4:
                case IrOpCode.Ldloc:
                case IrOpCode.Ldarg:
                case IrOpCode.Ldstr:
                case IrOpCode.Ldnull:
                case IrOpCode.Dup:
                    sp++;
                    if (sp > max) max = sp;
                    break;

                case IrOpCode.Add:
                case IrOpCode.Sub:
                case IrOpCode.Mul:
                case IrOpCode.Div:
                case IrOpCode.Rem:
                case IrOpCode.And:
                case IrOpCode.Or:
                case IrOpCode.Xor:
                case IrOpCode.Shl:
                case IrOpCode.Shr:
                case IrOpCode.Ceq:
                case IrOpCode.Cne:
                case IrOpCode.Cgt:
                case IrOpCode.Clt:
                case IrOpCode.CgtUn:
                case IrOpCode.CgeUn:
                    sp--;
                    break;

                case IrOpCode.Neg:
                case IrOpCode.Not:
                    break;

                case IrOpCode.Stloc:
                case IrOpCode.Starg:
                case IrOpCode.Pop:
                case IrOpCode.Brfalse:
                case IrOpCode.Brtrue:
                    sp--;
                    break;

                case IrOpCode.Ret:
                    if (cm.ReturnsValue) sp--;
                    break;

                case IrOpCode.Ldfld:
                    sp--;
                    sp++;
                    break;

                case IrOpCode.Stfld:
                    sp -= 2;
                    break;

                case IrOpCode.Newobj:
                    break;

                case IrOpCode.Call:
                    break;
            }
        }
        return max;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Generalized JIT path — handles all opcodes via object? locals
    // ═══════════════════════════════════════════════════════════════════

    private static JittedMethod? EmitGeneral(CompiledMethod cm, MethodNode method)
    {
        try
        {
            int argCount = cm.ArgCount;
            int localCount = cm.LocalCount;
            int codeLen = cm.Code.Length;
            bool hasReturn = cm.ReturnsValue;
            int STACK_BASE = 1 + argCount + localCount;
            int maxStack = Math.Max(ComputeMaxStack(cm), 1);

            var dm = new DynamicMethod(
                $"jit_gen_{cm.Name}",
                typeof(object),
                [typeof(object?[]), typeof(CPU), typeof(CompiledMethod)],
                typeof(JitCompiler).Module,
                skipVisibility: true);

            var il = dm.GetILGenerator();

            il.DeclareLocal(typeof(object));
            for (int i = 0; i < argCount; i++) il.DeclareLocal(typeof(object));
            for (int i = 0; i < localCount; i++) il.DeclareLocal(typeof(object));
            for (int i = 0; i < maxStack; i++) il.DeclareLocal(typeof(object));

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
                        il.Emit(OpCodes.Box, typeof(int));
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                        sp++;
                        break;

                    case IrOpCode.LdcR4:
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, typeof(CompiledMethod).GetProperty(nameof(CompiledMethod.FloatTable))!.GetMethod!);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.Emit(OpCodes.Ldelem_R4);
                        il.Emit(OpCodes.Box, typeof(float));
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                        sp++;
                        break;

                    case IrOpCode.Ldstr:
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, typeof(CompiledMethod).GetProperty(nameof(CompiledMethod.StringTable))!.GetMethod!);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                        sp++;
                        break;

                    case IrOpCode.Ldnull:
                        il.Emit(OpCodes.Ldnull);
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
                            il.Emit(OpCodes.Stloc, 0);
                        }
                        il.Emit(OpCodes.Br, retLabel);
                        break;

                    case IrOpCode.Add:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _jitAdd);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Sub:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _jitSub);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Mul:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _jitMul);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Div:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _jitDiv);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Rem:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _jitRem);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Neg:
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Call, _jitNeg);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Ceq:
                    case IrOpCode.Cne:
                    case IrOpCode.Cgt:
                    case IrOpCode.Clt:
                    case IrOpCode.CgtUn:
                    case IrOpCode.CgeUn:
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Ldc_I4, (int)instr.Opcode);
                        il.Emit(OpCodes.Call, _jitCompare);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Not:
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
                        il.Emit(OpCodes.Call, _jitNot);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
                        break;

                    case IrOpCode.Br:
                        il.Emit(OpCodes.Br, labels[instr.Operand]);
                        break;

                    case IrOpCode.Brfalse:
                    {
                        sp--;
                        var nextLabel = il.DefineLabel();
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _isTruthy);
                        il.Emit(OpCodes.Brtrue, nextLabel);
                        il.Emit(OpCodes.Br, labels[instr.Operand]);
                        il.MarkLabel(nextLabel);
                        break;
                    }

                    case IrOpCode.Brtrue:
                    {
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Call, _isTruthy);
                        il.Emit(OpCodes.Brtrue, labels[instr.Operand]);
                        break;
                    }

                    case IrOpCode.Ldfld:
                    {
                        sp--;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, typeof(CompiledMethod).GetProperty(nameof(CompiledMethod.StringTable))!.GetMethod!);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Call, _jitLdfld);
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                        sp++;
                        break;
                    }

                    case IrOpCode.Stfld:
                    {
                        sp -= 2;
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
                        il.Emit(OpCodes.Ldloc, STACK_BASE + sp + 1);
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, typeof(CompiledMethod).GetProperty(nameof(CompiledMethod.StringTable))!.GetMethod!);
                        il.Emit(OpCodes.Ldc_I4, instr.Operand);
                        il.Emit(OpCodes.Ldelem_Ref);
                        il.Emit(OpCodes.Call, _jitStfld);
                        break;
                    }

                    case IrOpCode.Newobj:
                    {
                        int targetIdx = instr.Operand;
                        var newObj = targetIdx >= 0 && targetIdx < cm.NewObjTargets.Length
                            ? cm.NewObjTargets[targetIdx] : null;
                        if (newObj == null) break;

                        var ctor = newObj.Constructor;
                        int ctorArgCount = ctor?.ParameterTypes.Count ?? 0;

                        il.Emit(OpCodes.Ldstr, newObj.Type.Name);
                        il.Emit(OpCodes.Ldc_I4, ctorArgCount);
                        il.Emit(OpCodes.Newarr, typeof(object));
                        for (int i = 0; i < ctorArgCount; i++)
                        {
                            il.Emit(OpCodes.Dup);
                            il.Emit(OpCodes.Ldc_I4, i);
                            il.Emit(OpCodes.Ldloc, STACK_BASE + sp - ctorArgCount + i);
                            il.Emit(OpCodes.Stelem_Ref);
                        }
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Call, _jitNewobjSimple);
                        sp -= ctorArgCount;
                        il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                        sp++;
                        break;
                    }

                    case IrOpCode.Call:
                    {
                        int targetIdx = instr.Operand;
                        var callInstr = targetIdx >= 0 && targetIdx < cm.CallTargets.Length
                            ? cm.CallTargets[targetIdx] : null;
                        if (callInstr == null) break;

                        int callArgCount = callInstr.Target.ParameterTypes.Count;

                        sp -= callArgCount;

                        il.Emit(OpCodes.Ldc_I4, targetIdx);
                        il.Emit(OpCodes.Ldc_I4, callArgCount);
                        il.Emit(OpCodes.Newarr, typeof(object));
                        for (int i = 0; i < callArgCount; i++)
                        {
                            il.Emit(OpCodes.Dup);
                            il.Emit(OpCodes.Ldc_I4, i);
                            il.Emit(OpCodes.Ldloc, STACK_BASE + sp + i);
                            il.Emit(OpCodes.Stelem_Ref);
                        }
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Ldarg_2);
                        il.Emit(OpCodes.Call, _jitCallSimple);

                        bool returnsValue = !string.Equals(callInstr.Target.ReturnType?.Name, "void", StringComparison.Ordinal);
                        if (returnsValue)
                        {
                            il.Emit(OpCodes.Stloc, STACK_BASE + sp);
                            sp++;
                        }
                        break;
                    }

                    default:
                        break;
                }
            }

            il.MarkLabel(retLabel);
            il.Emit(OpCodes.Ldloc, 0);
            il.Emit(OpCodes.Ret);

            var cmCaptured = cm;
            var del = dm.CreateDelegate<Func<object?[], CPU, CompiledMethod, object?>>();
            return (args, cpu, _) => del(args, cpu, cmCaptured);
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helper methods — called from JIT'd IL via Call
    // ═══════════════════════════════════════════════════════════════════

    public static object? JitAdd(object? a, object? b)
    {
        if (a is float fa && b is float fb) return fa + fb;
        if (a is float fa2) return fa2 + Convert.ToSingle(b);
        if (b is float fb2) return Convert.ToSingle(a) + fb2;
        return Convert.ToInt32(a) + Convert.ToInt32(b);
    }

    public static object? JitSub(object? a, object? b)
    {
        if (a is float fa && b is float fb) return fa - fb;
        if (a is float fa2) return fa2 - Convert.ToSingle(b);
        if (b is float fb2) return Convert.ToSingle(a) - fb2;
        return Convert.ToInt32(a) - Convert.ToInt32(b);
    }

    public static object? JitMul(object? a, object? b)
    {
        if (a is float fa && b is float fb) return fa * fb;
        if (a is float fa2) return fa2 * Convert.ToSingle(b);
        if (b is float fb2) return Convert.ToSingle(a) * fb2;
        return Convert.ToInt32(a) * Convert.ToInt32(b);
    }

    public static object? JitDiv(object? a, object? b)
    {
        if (a is float fa && b is float fb) return fa / fb;
        if (a is float fa2) return fa2 / Convert.ToSingle(b);
        if (b is float fb2) return Convert.ToSingle(a) / fb2;
        return Convert.ToInt32(a) / Convert.ToInt32(b);
    }

    public static object? JitRem(object? a, object? b)
    {
        if (a is float fa && b is float fb) return fa % fb;
        if (a is float fa2) return fa2 % Convert.ToSingle(b);
        if (b is float fb2) return Convert.ToSingle(a) % fb2;
        return Convert.ToInt32(a) % Convert.ToInt32(b);
    }

    public static object? JitNeg(object? a)
    {
        if (a is float fa) return -fa;
        return -Convert.ToInt32(a);
    }

    public static object? JitCompare(object? a, object? b, int opcode)
    {
        if (a is int ia && b is int ib)
        {
            bool result = opcode switch
            {
                (int)IrOpCode.Ceq => ia == ib,
                (int)IrOpCode.Cne => ia != ib,
                (int)IrOpCode.Cgt => ia > ib,
                (int)IrOpCode.Clt => ia < ib,
                (int)IrOpCode.CgtUn => ((uint)ia) > ((uint)ib),
                (int)IrOpCode.CgeUn => ((uint)ia) >= ((uint)ib),
                _ => false
            };
            return result ? 1 : 0;
        }

        if (a is float fa && b is float fb)
        {
            bool result = opcode switch
            {
                (int)IrOpCode.Ceq => BitConverter.SingleToInt32Bits(fa) == BitConverter.SingleToInt32Bits(fb),
                (int)IrOpCode.Cne => BitConverter.SingleToInt32Bits(fa) != BitConverter.SingleToInt32Bits(fb),
                (int)IrOpCode.Cgt => !float.IsNaN(fa) && !float.IsNaN(fb) && fa > fb,
                (int)IrOpCode.Clt => !float.IsNaN(fa) && !float.IsNaN(fb) && fa < fb,
                (int)IrOpCode.CgtUn => (float.IsNaN(fa) || float.IsNaN(fb)) || fa > fb,
                (int)IrOpCode.CgeUn => (float.IsNaN(fa) || float.IsNaN(fb)) || fa >= fb,
                _ => false
            };
            return result ? 1 : 0;
        }

        bool cmpResult;
        if (a == null && b == null) cmpResult = opcode == (int)IrOpCode.Ceq || opcode == (int)IrOpCode.CgeUn;
        else if (a == null) cmpResult = opcode == (int)IrOpCode.Cne || opcode == (int)IrOpCode.Cgt || opcode == (int)IrOpCode.CgtUn;
        else if (b == null) cmpResult = opcode == (int)IrOpCode.Cne || opcode == (int)IrOpCode.Clt || opcode == (int)IrOpCode.CgeUn;
        else if (a is IComparable ca)
        {
            int c = ca.CompareTo(b);
            cmpResult = opcode switch
            {
                (int)IrOpCode.Ceq => c == 0,
                (int)IrOpCode.Cne => c != 0,
                (int)IrOpCode.Cgt => c > 0,
                (int)IrOpCode.Clt => c < 0,
                (int)IrOpCode.CgtUn => c > 0,
                (int)IrOpCode.CgeUn => c >= 0,
                _ => false
            };
        }
        else cmpResult = false;

        return cmpResult ? 1 : 0;
    }

    public static int JitIsTruthy(object? v)
    {
        if (v == null) return 0;
        if (v is int iv) return iv != 0 ? 1 : 0;
        if (v is float fv) return fv != 0.0f ? 1 : 0;
        if (v is bool bv) return bv ? 1 : 0;
        return 1;
    }

    public static object? JitNot(object? v)
    {
        if (v is bool bv) return !bv;
        if (v is int iv) return iv == 0 ? 1 : 0;
        if (v is float fv) return fv == 0.0f ? 1 : 0;
        return v == null ? 1 : 0;
    }

    public static object? JitLdfld(object? instance, string fieldName)
    {
        if (instance is ManagedObject mo)
        {
            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
            return mo.GetField(fieldName);
        }
        return null;
    }

    public static void JitStfld(object? value, object? instance, string fieldName)
    {
        if (instance is ManagedObject mo)
        {
            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
            mo.SetField(fieldName, value);
        }
    }

    public static object? JitNewobj(string typeName, object?[] args, int ctorArgCount, CPU cpu)
    {
        var instance = new ManagedObject(typeName);

        if (cpu.program != null)
        {
            foreach (var cls in cpu.program.Classes)
            {
                if (string.Equals(cls.Name, typeName, StringComparison.Ordinal) && cls.Constructors.Count > 0)
                {
                    var ctor = cls.Constructors[0];
                    var ctorMethod = new MethodNode("constructor", ctor.Parameters, TypeRef.Void, false, null, ctor.Body);
                    cpu.ExecuteMethod(ctorMethod, instance);
                    break;
                }
            }
        }

        return instance;
    }

    public static object? JitCall(int targetIndex, object?[] args, int argCount, CPU cpu, CompiledMethod cm)
    {
        if (targetIndex < 0 || targetIndex >= cm.CallTargets.Length) return null;
        var callInstr = cm.CallTargets[targetIndex];
        if (callInstr == null) return null;

        var target = cpu.ResolveMethod(callInstr.Target);
        if (target == null) return null;

        if (target.NativeImpl != null)
        {
            var nativeArgs = new Value<object>[argCount];
            for (int i = 0; i < argCount; i++)
                nativeArgs[i] = new Value<object>(args[i]);

            Value<object> result;
            using (ProgramLoader.Activate(cpu))
            {
                result = target.NativeImpl.Method(nativeArgs);
            }

            if (!string.Equals(target.ReturnType.Name, "void", StringComparison.Ordinal) && result != null)
            {
                return result is IValue iv ? iv.GetObjectData() : result;
            }
            return null;
        }

        var compiledTarget = cpu.GetCompiled(target);
        if (compiledTarget != null)
        {
            var jitDel = cpu.Cache.GetJit(target);
            if (jitDel != null)
            {
                var jitArgs = new object?[argCount];
                for (int i = 0; i < argCount; i++)
                    jitArgs[i] = args[i];
                var jitResult = jitDel(jitArgs, cpu, compiledTarget);
                return compiledTarget.ReturnsValue ? jitResult : null;
            }

            var stackArgs = new StackValue[argCount];
            for (int i = 0; i < argCount; i++)
                stackArgs[i] = CompiledExecutor.RawToStackValue(args[i]);
            var result = CompiledExecutor.Execute(compiledTarget, stackArgs, cpu);
            return compiledTarget.ReturnsValue ? result.ToObject() : null;
        }

        return null;
    }

    public static object? JitCallSimple(int targetIndex, object?[] args, CPU cpu, CompiledMethod cm)
        => JitCall(targetIndex, args, args.Length, cpu, cm);

    public static object? JitNewobjSimple(string typeName, object?[] args, CPU cpu)
        => JitNewobj(typeName, args, args.Length, cpu);
}
