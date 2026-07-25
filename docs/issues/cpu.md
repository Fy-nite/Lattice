# CPU.cs — Issues

File: `Lattice.Runtime/Runtime/CPU.cs`

---

## CRIT-01: ExecuteMethod fast path pushes result to wrong frame

**Lines:** 283–314
**Affects:** Every compiled/JIT method call that returns a value.

When the compiled or JIT path is taken in `ExecuteMethod`, **no call frame is
pushed**. The code then writes the return value to `CurrentFrame.Previous`:

```csharp
// Line 294-296 (JIT path)
if (compiled.ReturnsValue && CurrentFrame?.Previous != null)
{
    CurrentFrame.Previous.EvaluationStack.Push(jitResult);
}
```

But `CurrentFrame` IS the caller (nothing was pushed), so
`CurrentFrame.Previous` is the grandparent. The return value lands on the
wrong evaluation stack — one frame too deep.

**Compare** with the AST path at line 321 which correctly pushes a new frame,
and the native path at line 249 which also pushes.

**Fix:** Either push a temporary frame before calling the compiled method
(and pop it after), or push the result onto `CurrentFrame.EvaluationStack`
directly (since CurrentFrame is the caller when no frame was pushed).

---

## CRIT-02: Not opcode corrupts stack when operand is not bool

**Lines:** 548–556

```csharp
case OpCode.Not:
{
    object val2 = CurrentFrame.EvaluationStack.Pop();
    object? raw2 = Unwrap(val2);
    if (raw2 is bool b2)
        CurrentFrame.EvaluationStack.Push(!b2);
    break;  // <-- if not bool, nothing is pushed back!
}
```

If the popped value is an int, string, or any non-bool type, the value is
popped and nothing is pushed. The stack shrinks by one with no recovery.
Every subsequent stack-relative operation (branch, arithmetic, return) is
now off by one.

**Fix:** Push a default value back, or throw, or coerce non-bool to bool
(e.g. `raw2 != null`).

---

## HIGH-01: No stack overflow check on JIT/compiled/native paths

**Lines:** 283–314, 241–280

The depth check at line 316 (`if (GetStackDepth() >= MaxStackDepth)`) is only
reached when neither the JIT, compiled, nor native path matches. All three
bypass it entirely.

Recursive JIT calls can recurse indefinitely and overflow the real CLR stack.

**Fix:** Add a depth check at the top of `ExecuteMethod`, before any dispatch.

---

## HIGH-02: Modules list mutated during enumeration

**Lines:** 15, 90, 775–779

`Modules` is a `List<ModuleNode>` shared across CPU instances. `SpawnThread`
does a shallow copy (line 90), but `NativeRegistry.TryRegister` (called from
`ResolveMethod` at line 753) mutates `program.Classes` under a static lock.
Other threads can be enumerating `program.Classes` (via `ResolveInModule`,
`FindClassByMethod`, `CompileAll`) concurrently.

The lock in `NativeRegistry` protects against concurrent `TryRegister` calls,
but not against a concurrent enumeration on a different CPU thread.

**Fix:** Use `ConcurrentBag` or `ReadOnlyCollection`, or snapshot before
iteration, or hold the same lock during enumeration.

---

## HIGH-03: Frame leak on exception in AST ExecuteMethod

**Lines:** 321–353

A new frame is pushed at line 321/329. If an exception occurs during the
while loop (line 335–339), the `finally` block at line 348–352 is empty.
The exception propagates, but `CurrentFrame` is left pointing at `newFrame`.
Any subsequent `Step()` call on this CPU starts with a corrupted frame.

**Fix:** The `finally` block should pop the frame:
```csharp
finally { CurrentFrame = CurrentFrame.Previous; }
```

---

## HIGH-04: Starg NPE when Location is null

**Lines:** 419

```csharp
var sx = ins.Location;
var locInfo = (sx != null) ? $"\n at {sx.Line}: {sx.SourceLine}" : "";
```

Wait, that's the Stloc path. Let me check Starg:

```csharp
// Line 419-423
case OpCode.Starg:
{
    var sx = ins.Location;
    var locInfo = (sx != null) ? $"\n at {sx.Line}: {sx.SourceLine}" : "";
```

Actually the Stloc path at line 402–410 has the same pattern but handles null
correctly. The Starg path at line 419 accesses `sx.Line` and `sx.SourceLine`
inside the null check — so this is fine. But the initial `sx` dereference...

Actually looking more carefully, `ins.Location` is checked: `(sx != null)`.
This is fine. Downgrading to LOW.

---

## MED-01: ResolveMethod called with potentially null DeclaringType

**Lines:** 753

```csharp
if (NativeRegistry.TryRegister(target.DeclaringType.Name, program))
```

`target.DeclaringType` is never null-checked. A `MethodReference` for a
static-global method with no declaring class would throw NPE.

**Fix:** Add a null check before accessing `.Name`.

---

## MED-02: ResolveThisMethod uses parent CPU's frame for child

**Lines:** 98

`SpawnThread` calls `this.ResolveMethod(entryPoint.Method)` on the parent CPU.
If the method requires `CurrentFrame` context for `this` resolution (line
766–767), it resolves against the parent's frame, not the child's. The child
CPU has no frame yet (set via `PushFrame` at line 103).

---

## MED-03: WhileStatement uses source line heuristics

**Lines:** 622–641

The backward scan to find the condition start relies on `Location?.Line`
matching. If the compiler doesn't emit source locations for condition
instructions, or if they span multiple lines, this heuristic fails silently.
The loop sets the wrong IP, causing infinite loops or skipped instructions.

---

## MED-04: WhileStatement stale stack assumption

**Lines:** 636

The while condition is evaluated only if `EvaluationStack.Count > 0`. If the
condition is not `"stack"`, the stack check is meaningless and the condition
always returns false.

---

## MED-05: EvaluateCondition only handles "stack"

**Lines:** 721–743

Only the string `"stack"` is recognized as a condition type. Any other
condition string returns false. If the condition field is set to anything else
(e.g. `"true"`, a variable name), the while/if never executes its body.

---

## MED-06: NativeImpl exception leaves evaluation stack dirty

**Lines:** 251–279

If a native method throws, the `finally` block pops the frame, but any values
the native method pushed onto the evaluation stack before throwing remain. The
caller's evaluation stack now has unexpected values.

---

## MED-07: NewObjInstruction catch wraps exception but loses inner

**Lines:** 597–600

The catch block creates `new RuntimeException(ex.Message)` but does not pass
`ex` as an inner exception. The original stack trace is lost.

---

## MED-08: LdcI4 / LdcR4 FormatException not caught

**Lines:** 384–388

`int.Parse(simple.Operand!)` and `float.Parse(simple.Operand!)` can throw
`FormatException` if the operand is malformed. These propagate unhandled.

---

## LOW-01: GetStackDepth is O(n), called every ExecuteMethod

**Lines:** 203–211

Walks the entire frame chain on every call. A simple counter field would be
O(1). Hot path allocation pressure.

---

## LOW-02: LINQ FirstOrDefault in hot paths allocates delegates

**Lines:** 59, 65, 72, 124, 128, 799, 801, 806, 808, etc.

`FirstOrDefault` with a lambda allocates a delegate on every call. In
`ResolveMethod` (called for every `CallInstruction`), this is allocation
pressure. Use a `for` loop instead.

---

## LOW-03: PopTwo() allocates a tuple

**Lines:** 883–888

Each arithmetic operation allocates a `ValueTuple<object, object>`. Minor
but in a hot loop.

---

## LOW-04: ResolveThisMethod linear scans all modules

**Lines:** 784–814

On every `this`-prefixed method call, scans the current class, then ALL
classes in ALL modules. No indexing.

---

## LOW-05: Dead code — mainArgs list never used

**Lines:** 136–147

`InitializeMain` creates `List<object> mainArgs` and populates it, but never
uses it. Actual arg assignment happens via `CurrentFrame.Args`.

---

## LOW-06: CallMethod ClassName.MethodName parsing wrong for deep paths

**Lines:** 981–986

`methodPath.Split('.')` splits on every dot. For `Namespace.Class.Method`,
only the last two segments are used. Silently ignores namespace prefixes.

---

## LOW-07: Compare IComparable may throw for incomparable types

**Lines:** 904–908

If `a` implements `IComparable` but `b` is an incompatible type,
`CompareTo(b)` may throw `ArgumentException`. No fallback.

---

## LOW-08: DoBinaryArith unchecked int overflow

**Lines:** 898

`intOp` is called outside a `checked` context. `int` overflow wraps silently.
The outer `checked` in `ExecuteMethod` (line 220) covers this path, but
`CallMethod<T>` (line 1029) may not be in a checked context.

---

## LOW-09: LoadProgram misleading error message

**Lines:** 189–193

Error says "File not found" but could also be a parse failure. Misleading.

---

## LOW-10: Dup / Pop no stack-empty guard

**Lines:** 429, 433

`Peek()` and `Pop()` throw `InvalidOperationException` if the stack is
empty. No guard, no domain-specific error.
