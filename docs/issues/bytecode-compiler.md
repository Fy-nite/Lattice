# BytecodeCompiler.cs — Issues

File: `Lattice.Runtime/Runtime/Compiler/BytecodeCompiler.cs`

---

## CRIT-01: ResolveArg returns -1 silently instead of erroring

**Lines:** 76–82

```csharp
int ResolveArg(string name)
{
    // ... not found ...
    return -1;
}
```

When an `ldarg` or `starg` references a parameter name not in the method
signature, `-1` is emitted as the operand. At runtime, `CompiledExecutor`
does `args[-1]` → `IndexOutOfRangeException`.

The interpreter handles this with a `KeyNotFoundException`. Neither path
gives a useful error message.

**Fix:** Throw at compile time:
```csharp
int ResolveArg(string name)
{
    if (argNameMap.TryGetValue(name, out var idx)) return idx;
    throw new CompilationException($"Unknown argument '{name}' in method '{method.Name}'");
}
```

---

## CRIT-02: ResolveLocal creates on-the-fly locals instead of erroring

**Lines:** 67–73

```csharp
int ResolveLocal(string name)
{
    if (localNameMap.TryGetValue(name, out var idx)) return idx;
    idx = localNames.Count;  // creates a new local on the fly
    localNameMap[name] = idx;
    localNames.Add(name);
    return idx;
}
```

If code references a variable not in `method.Locals` (a typo or undeclared
variable), it is silently added as a new local. The variable won't have a
`LocalDeclarationStatement`, so it won't be skipped during compilation, and
its default value is whatever the args-copy bug (CE-01) leaves in that slot.

**Fix:** Throw at compile time for undeclared variables, or at minimum warn.

---

## HIGH-01: SwitchStatement / ForStatement cause InvalidCastException

**Lines:** 182–184

```csharp
else
{
    CompileInstructionStmt((InstructionStatement)stmt);
    i++;
}
```

`SwitchStatement` and `ForStatement` are `Statement` subtypes but are not
`InstructionStatement`. The cast throws `InvalidCastException`. These AST
nodes are defined but completely unhandled by the bytecode compiler.

**Fix:** Either implement compilation for these nodes, or throw a clear error:
```csharp
throw new CompilationException(
    $"SwitchStatement and ForStatement are not supported in bytecode compilation. " +
    $"Use IfStatement/WhileStatement equivalents.");
```

---

## MED-01: FindConditionStart heuristic is fragile

**Lines:** 191–207

The condition grouping for `IfStatement` and `WhileStatement` depends
entirely on source line numbers matching between condition instructions and
the `while`/`if` keyword. Auto-formatters, line wrapping, or comments between
the condition and the keyword can break the grouping.

If the condition instructions are on a different source line than the keyword,
the condition is empty and `brfalse` always branches (empty stack → `IsTruthy`
on default `StackValue` is false).

**Fix:** The `Condition` string field on `IfStatement`/`WhileStatement` could
be used as a fallback, or the compiler could emit an error when no condition
instructions are found.

---

## MED-02: IfStatement.Condition / WhileStatement.Condition strings are unused

**Lines:** 209–256

The `IfStatement` and `WhileStatement` AST records have a `string Condition`
field, but the bytecode compiler ignores it entirely. It instead uses the
source-line heuristic above.

This means the `Condition` field is dead data that misleads readers. The
compiler should either use it (e.g., as a variable name to test) or remove it.

---

## MED-03: Ldloc/Stloc operand type mismatch between compiler and interpreter

**Lines:** 109–110 (compiler) vs CPU.cs:396, 412 (interpreter)

The bytecode compiler resolves local names to integer indices (`ResolveLocal`
returns `int`). The interpreter uses string keys:
`CurrentFrame.Locals[simple.Operand!]` where `Operand` is a `string`.

The compiled executor uses `locals[instr.Operand]` with integer indexing. This
means the bytecode compiler and the interpreter have fundamentally different
local variable models — locals are indexed by name in the interpreter but by
integer position in the bytecode compiler.

---

## LOW-01: LocalNameToIndex is a meaningless identity mapping

**Lines:** 272

```csharp
LocalNameToIndex = localNames.Select((_, i) => i).ToArray(),
```

This produces `[0, 1, 2, ...]` — the identity mapping. The property name
implies it should map local names to their indices, but it's just a
sequential array.

---

## LOW-02: NewObj args aren't consumed when constructor is null

**Lines:** 126–130

The compiler emits `Newobj` and pushes the `NewObjInstruction` to the targets
list, but doesn't encode how many stack values the constructor expects. At
runtime, the executor determines arg count from `ctor.Parameters.Count`. If
the constructor is null/unresolvable, those args are never popped.

The compiler could mitigate this by encoding the expected arg count in the
instruction itself.
