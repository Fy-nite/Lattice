# JitCompiler.cs — Issues

File: `Lattice.Runtime/Runtime/Compiler/JitCompiler.cs`

---

## CRIT-01: And / Or / Xor / Shl / Shr silently no-op in EmitGeneral

**Lines:** 687–718 (the `default: break;` at 721)

These opcodes are valid — the bytecode compiler emits them via its `default`
case — but `EmitGeneral` has no case for them. They hit `default: break;`,
which is a silent no-op. The operands stay on the virtual stack, the operation
is simply skipped, and the result is wrong.

**Affected opcodes:** `And`, `Or`, `Xor`, `Shl`, `Shr`

**Fix:** Add cases for each, similar to Add/Sub:

```csharp
case IrOpCode.And:
    sp--;
    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
    il.Emit(OpCodes.Ldloc, STACK_BASE + sp);
    il.Emit(OpCodes.Call, _jitAnd);  // need to add helper
    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
    break;
```

And corresponding helper methods:
```csharp
public static object? JitAnd(object? a, object? b) =>
    Convert.ToInt32(a) & Convert.ToInt32(b);
```

---

## CRIT-02: Neg missing from EmitIntOnly (causes unnecessary fallback)

**Lines:** 110–280 (no `case IrOpCode.Neg:`)

`CanEmitIntOnly` doesn't reject `Neg`, so code containing it enters
`EmitIntOnly`. Since there's no case, it hits `default: return null` at
line 278, falling back to `EmitGeneral`. This works (EmitGeneral handles
Neg), but it's an unnecessary fallback to the slower path.

**Fix:** Add a Neg case to EmitIntOnly:
```csharp
case IrOpCode.Neg:
    il.Emit(OpCodes.Ldloc, STACK_BASE + sp - 1);
    il.Emit(OpCodes.Neg);
    il.Emit(OpCodes.Stloc, STACK_BASE + sp - 1);
    break;
```

---

## HIGH-01: ComputeMaxStack doesn't model Call stack effects

**Lines:** 398–399

`Call` is treated as having zero net stack effect. In reality, it consumes
`callArgCount` items and optionally pushes 1 (if the target returns a value).

```
case IrOpCode.Call:
    break;  // wrong! should be: sp -= callArgCount; if (returnsValue) sp++;
```

This underestimates `maxStack`. The allocated locals for the virtual stack
will be too few, causing `IndexOutOfRangeException` or `VerificationException`
at JIT time when a method has multiple sequential calls that return values.

Same issue for `Newobj` at line 395–396.

**Fix:** Need to look up the call target's arg count and return type from
`cm.CallTargets[instr.Operand]`, or at minimum do:
```csharp
case IrOpCode.Call:
{
    var ct = instr.Operand >= 0 && instr.Operand < cm.CallTargets.Length
        ? cm.CallTargets[instr.Operand] : null;
    int argc = ct?.Target.ParameterTypes.Count ?? 1;
    sp -= argc;
    bool hasReturn = !string.Equals(ct?.Target.ReturnType?.Name, "void", StringComparison.Ordinal);
    if (hasReturn) sp++;
    if (sp > max) max = sp;
    break;
}
```

---

## HIGH-02: JitNewobj drops constructor arguments

**Lines:** 881–900

`JitNewobj` receives an `args` array and `ctorArgCount`, but never passes
them to the constructor. At line 893:

```csharp
cpu.ExecuteMethod(ctorMethod, instance);  // providedArgs defaults to null
```

The constructor then tries to pop args from the evaluation stack (wrong in
this context), or gets zeroed-out `poppedArgs`.

**Fix:** Pass the args:
```csharp
cpu.ExecuteMethod(ctorMethod, instance, args);
```

But also need to ensure `ExecuteMethod` handles the `providedArgs` path
correctly for constructors (it needs to set up Args["this"] too).

---

## HIGH-03: JitNewobj always uses first constructor

**Lines:** 891

```csharp
var ctor = cls.Constructors[0];
```

If a class has overloaded constructors with different parameter counts, the
wrong constructor body may be executed. The `args` array is never consulted
for constructor selection.

**Fix:** Match by parameter count:
```csharp
var ctor = cls.Constructors.FirstOrDefault(c => c.Parameters.Count == ctorArgCount)
    ?? cls.Constructors[0];
```

---

## MED-01: Null comparison semantics differ from interpreter

**Lines:** 822–825

When one operand is null and the other isn't:

| Opcode | JIT | Interpreter |
|--------|-----|-------------|
| `Cgt(null, x)` | `true` | `false` |
| `Clt(null, x)` | `true` | `false` |
| `CgtUn(null, x)` | `true` | `false` |
| `CgeUn(null, x)` | `true` | `false` |

JIT and interpreter produce different results for null comparisons.

**Fix:** Match the interpreter's behavior (cmp = 0 for null):
```csharp
if (a == null || b == null)
{
    // null is treated as "less than" everything
    return opcode switch
    {
        IrOpCode.Clt => a == null && b != null,
        IrOpCode.Cgt => a != null && b == null,
        _ => false
    };
}
```

---

## MED-02: Null target corrupts virtual stack in EmitGeneral

**Lines:** 664, 692

When `newObj == null` (line 664) or `callInstr == null` (line 692), the code
does `break` without adjusting `sp`. The call/ctor args remain on the virtual
stack unconsumed, and no result is pushed. The virtual stack pointer is wrong
for all subsequent instructions.

**Fix:** Either throw, or pop the args:
```csharp
if (callInstr == null)
{
    sp -= callArgCount;  // consume the args even on failure
    break;
}
```

---

## MED-03: CanEmitIntOnly doesn't reject Neg

**Lines:** 297–331

`CanEmitIntOnly` returns `true` for code containing `Neg`. Since `EmitIntOnly`
can't handle it (no case), it returns null, falling back to `EmitGeneral`.
Not a correctness bug but causes unnecessary fallback to the slower path.

**Fix:** Add `Neg` to the rejection list, or handle it in EmitIntOnly (see
CRIT-02).

---

## MED-04: JitNewobj skips constructor when cpu.program is null

**Lines:** 885

```csharp
if (cpu.program != null)
{
    // run constructor body
}
```

If the CPU doesn't have a program loaded (e.g., during unit testing), the
constructor body is silently skipped. The object is created but never
initialized.

**Fix:** Either always run the constructor, or throw when program is needed.

---

## LOW-01: Brfalse creates a new Label per instruction

**Lines:** 614

Each `Brfalse` defines a new `Label` via `il.DefineLabel()`. Functionally
correct but wasteful. The logic could be inverted (use Brtrue to skip the
fall-through) or labels could be pre-computed.

---

## LOW-02: Float Ceq uses bit-level comparison

**Lines:** 811

`BitConverter.SingleToInt32Bits(fa) == SingleToInt32Bits(fb)` means
`0.0f != -0.0f` (different bit patterns). Consistent with `CompiledExecutor`,
but differs from IEEE 754 where they're equal. Worth noting.

---

## LOW-03: ComputeMaxStack tracking mismatch

**Lines:** 333–403

`ComputeMaxStack` tracks the virtual stack pointer, but `EmitGeneral` uses
additional IL evaluation stack depth during `Ldfld`, `Stfld`, `Newobj`, and
`Call` that goes beyond the virtual stack. The IL eval stack depth is
transient and managed by the CLR verifier, so this isn't a bug, but
`ComputeMaxStack` is measuring the wrong thing for `EmitGeneral`'s actual
IL stack needs.
