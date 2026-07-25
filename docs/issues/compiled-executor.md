# CompiledExecutor.cs — Issues

File: `Lattice.Runtime/Runtime/Compiler/CompiledExecutor.cs`

---

## CRIT-01: Locals initialized from args array

**Lines:** 31–32

```csharp
var locals = new StackValue[localCount];
args.AsSpan(0, Math.Min(args.Length, localCount)).CopyTo(locals);
```

The compiler assigns args and locals **separate index namespaces** (`Ldarg 0`
reads `args[0]`, `Ldloc 0` reads `locals[0]`). But the executor copies args
into the first slots of locals at startup. If a method has 2 params and 3
locals, `locals[0]` is initialized to `args[0]` instead of the local's default
value (0).

Any local read before it is written will return the wrong parameter's value.

**Fix:** Don't copy args into locals. Locals should be zero-initialized:
```csharp
var locals = new StackValue[localCount]; // already zero-initialized
```

---

## CRIT-02: Call silently skips when method resolution fails — stack corruption

**Lines:** 169–214

```csharp
if (target != null)
{
    int argCount = target.Parameters.Count;
    // ... pop args and do work ...
}
ip++; break;
```

If `cpu.ResolveMethod(callInstr.Target)` returns null, the args that were
pushed before the `Call` instruction are **never consumed** from the stack.
The call is a no-op, but the stack is now polluted with the argument values.

Compare with the interpreter (`CPU.cs:569–571`) which throws
`MethodResolutionException`.

**Fix:** Throw or at minimum pop the args:
```csharp
if (target == null)
{
    sp -= callArgCount;
    throw new MethodResolutionException(callInstr.Target.Name, null);
}
```

---

## CRIT-03: Call silently skips when compiled target is null — stack corruption

**Lines:** 197–206

```csharp
var compiledTarget = cpu.GetCompiled(target);
if (compiledTarget != null)
{
    var result = Execute(compiledTarget, pooled.ToArray(), cpu);
    if (compiledTarget.ReturnsValue)
        s[sp++] = result;
}
// If compiledTarget is null: args popped, no result pushed
```

When the method exists but hasn't been compiled (e.g., an empty-body method
or one that failed compilation), the args are consumed from the stack but no
result is pushed for non-void methods.

**Fix:** Throw or push a default:
```csharp
else
{
    sp -= argCount;  // already consumed
    throw new RuntimeException($"Method {target.Name} has no compiled code");
}
```

---

## HIGH-01: Newobj doesn't call native constructors

**Lines:** 235–237

```csharp
var compiledCtor = cpu.GetCompiled(ctor);
if (compiledCtor != null)
    Execute(compiledCtor, callArgs, cpu);
```

`GetCompiled()` returns null for native methods (they have no compiled
bytecode). If a constructor is native, it is silently skipped — the instance
is created but never initialized.

The interpreter (`CPU.cs:241–280`) handles native methods via
`NativeImpl.Method()`.

**Fix:** Add a native check:
```csharp
if (ctor.NativeImpl != null)
{
    // call native constructor
}
else
{
    var compiledCtor = cpu.GetCompiled(ctor);
    if (compiledCtor != null)
        Execute(compiledCtor, callArgs, cpu);
}
```

---

## HIGH-02: Newobj doesn't pass `this` to the constructor

**Lines:** 236–237

```csharp
Execute(compiledCtor, callArgs, cpu);
```

The interpreter calls `ExecuteMethod(ctor, instance)` which stores `instance`
as `Args["this"]` in the frame. The compiled executor's `Execute()` has no
`thisObj` parameter — the instance reference is never available inside a
compiled constructor. Any `stfld` in a constructor body won't find `this`.

**Fix:** Need to either add a `thisObj` parameter to `Execute()`, or inject
the instance into the args array.

---

## HIGH-03: Newobj stack leak when constructor is null or unresolvable

**Lines:** 226–240

If `newObj.Constructor` is null (line 226) or `cpu.ResolveMethod()` returns
null (line 228–229), the constructor args that were pushed onto the stack
before `Newobj` are never consumed. The instance is pushed (line 240), but
the stale args remain.

The interpreter (`CPU.cs:584–592`) throws `MethodResolutionException` instead.

**Fix:** Pop the args on failure:
```csharp
if (ctor == null || resolvedCtor == null)
{
    sp -= ctorArgCount;
    throw new MethodResolutionException(...);
}
```

---

## MED-01: ResolveArg returns -1 for unknown args → IndexOutOfRangeException

**Lines:** 69 (executor), 76–82 (compiler)

```csharp
// Compiler:
int ResolveArg(string name) { /* not found */ return -1; }
// Emitted as: Emit(OpCode.Ldarg, -1);

// Executor:
case OpCode.Ldarg:
    s[sp++] = args[instr.Operand]; // args[-1] → IndexOutOfRangeException
```

**Fix:** Throw a compile-time error in `ResolveArg` instead of returning -1.

---

## MED-02: Missing opcode implementations — silent no-ops

**Lines:** 264–265 (default case)

The bytecode compiler emits any opcode from `SimpleInstruction`, but the
executor only handles a subset. The following opcodes are silently skipped:

- **Arithmetic:** `Neg`, `And`, `Or`, `Xor`, `Shl`, `Shr`
- **Branches:** `Beq`, `Bne`, `Bgt`, `Blt`
- **Conversions:** `ConvI4`, `ConvI8`, `ConvR4`, `ConvR8`, `ConvU4`, `ConvU8`
- **Arrays:** `Newarr`, `Ldelem`, `Stelem`, `Ldlen`
- **Objects:** `Box`, `Unbox`, `Castclass`, `Isinst`
- **Static fields:** `Ldsfld`, `Stsfld`
- **Constants:** `LdcI8`, `LdcR8`
- **Calls:** `Callvirt`, `Calli`

**Fix:** At minimum, throw for unhandled opcodes:
```csharp
default:
    throw new RuntimeException($"Bytecode interpreter does not implement {instr.Opcode}");
```

---

## MED-03: Ldfld/Stfld dot-splitting wrong for multi-part names

**Lines:** 249, 259

```csharp
if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
```

If `fieldName` is `"Namespace.Class.Field"`, `Split('.')[1]` returns
`"Class"`, not `"Field"`.

Same bug in the interpreter (`CPU.cs:442, 455`).

**Fix:** Use `Split('.')[^1]` or `Substring(fieldName.LastIndexOf('.') + 1)`.

---

## MED-04: Native method args array contains stale data from ArrayPool

**Lines:** 183

```csharp
var nativeArgs = ArrayPool<Value<object>>.Shared.Rent(argCount);
```

`ArrayPool.Rent` returns an array that may be larger than `argCount` and
contains stale data from previous uses. The native method receives the full
rented array, not just `argCount` elements.

**Fix:** Only pass `argCount` elements, or use `new Value<object>[argCount]`.

---

## LOW-01: No stack bounds checking

**Lines:** 34–35

```csharp
var s = new StackValue[codeLen + 16];
int sp = 0;
```

No bounds check on `sp`. If bytecode pushes more values than `codeLen + 16`,
an unhandled `IndexOutOfRangeException` occurs. The interpreter has stack
depth checking.

---

## LOW-02: Ret returns default for void methods with leftover stack values

**Lines:** 84–85

```csharp
return cm.ReturnsValue && sp > 0 ? s[--sp] : default;
```

If `cm.ReturnsValue` is false but `sp > 0`, leftover values are silently
abandoned. If `cm.ReturnsValue` is true but `sp == 0` (stack underflow),
`default` is returned and pushed onto the caller's stack.

---

## LOW-03: Newobj target index out-of-bounds silently skips

**Lines:** 220–221

```csharp
var newObj = targetIdx >= 0 && targetIdx < cm.NewObjTargets.Length
    ? cm.NewObjTargets[targetIdx] : null;
```

If the index is invalid, constructor args remain on the stack and no object
is pushed.
