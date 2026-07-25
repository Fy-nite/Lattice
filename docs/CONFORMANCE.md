# ObjectIR V2 Conformance

Lattice implements ObjectIR V2 as specified in the [ObjectIR V2 Language Specification](https://github.com/Fy-nite/ObjectIR). This document details what is implemented, what is not, and why.

## Implementation status

### ✅ Fully implemented

| Feature | Notes |
|---------|-------|
| **All opcodes** | Stack, load/store, arithmetic, comparison, logical, type conversion, control flow, calls, exceptions, objects, arrays. |
| **Structured exception handling** | `try`/`catch`/`finally` with exception propagation and stack unwinding. `break` and `continue` are control-flow tokens, not user-catchable. |
| **Static and virtual dispatch** | Both static and virtual method calls. Dispatch follows the call resolution order (native lookup → host reflection → IR dispatch). |
| **Call resolution order** | (1) Exact native binding match, (2) simplified name match, (3) host reflection, (4) IR method dispatch. |
| **Value coercion** | Lazy coercion at instruction boundaries per Section 5.5 of the spec (bool, integer, float, string, object). |
| **Host type registration** | Register .NET types and methods; ObjectIR code can instantiate and call them. |
| **Native method bindings** | Fine-grained registration of method callbacks via signature strings. |
| **Observability hooks** | Step hook (before each instruction) and exception hook (unhandled exceptions). |
| **Module lifecycle** | Load, reset, active module management. |
| **All input formats** | TextIR (`.oir`), JSON (`.jir`), BIR/BSON (`.bir`), FOB/IR v3 (`.fob`). |
| **Type system** | Primitive types, user-defined types (class, interface, struct, enum), inheritance (single base + multiple interfaces), null. |
| **Fields and properties** | Instance and static fields. |
| **Arrays** | Dynamic arrays via `newarr`, `ldelem`, `stelem`. Arena-backed `HeapArray` available with `--experimental heap`. |
| **String operations** | String concatenation via `add` instruction when either operand is string. |

### ⚠️ Known limitations (V2 spec limits, not Lattice bugs)

| Limitation | Reason | Planned for |
|-----------|--------|-------------|
| **No generics** | Generic type parameters and instantiation are not defined in V2. Use host type registration to expose generic .NET types. | ObjectIR V3 |
| **Untyped arrays** | All arrays are dynamically typed. Distinct `int[]`, `float[]` etc. are not defined. | ObjectIR V3 |
| **Exact type matching** | `castclass` and `isinst` match by exact type name only; inheritance chains are not walked. | ObjectIR V3 |
| **No method overloading on IR types** | Only one method per name per type in IR. Overloading resolution applies only to host native bindings. | ObjectIR V3 |
| **Single active module** | The runtime holds one module at a time. Loading a new module replaces the previous. | Design choice (simplicity) |
| **No thread safety** | The specification does not define thread-safety semantics. Lattice uses `ConcurrentDictionary` for shared state and `ManualResetEventSlim` for scheduler signaling, but cross-thread object sharing is not safe without external synchronization. | Future enhancement |
| **No module-level free functions** | The `functions` field is reserved in the spec but not executed. | ObjectIR V3 |

## Conformance requirements

A conforming ObjectIR V2 runtime must:

1. ✅ Accept at least one of the four module formats (TextIR, JSON, BIR/BSON, FOB/IR v3) as input.
2. ✅ Maintain a call stack of frames with independent evaluation stacks.
3. ✅ Implement all opcodes defined in Section 6.
4. ✅ Apply the coercion rules of Section 5.5 at instruction boundaries.
5. ✅ Implement the call resolution order of Section 6.10.1.
6. ✅ Implement structured exception handling (Section 7), with break/continue as non-user-catchable control-flow tokens.
7. ✅ Expose an entry-point invocation mechanism (canonically `Program.Main()`).
8. ✅ Expose a named method invocation mechanism allowing the host to call any method by qualified name with arbitrary arguments.
9. ✅ Expose a named method existence check that inspects module metadata without executing code.

A conforming runtime should additionally:

10. ✅ Implement host type registration (Section 10).
11. ✅ Implement native method binding (Section 10).
12. ✅ Implement step and exception observability hooks.
13. ✅ Pre-register built-in System.Console and System.String native bindings.

## Testing

Lattice's conformance is validated by:

- **313 tests** covering all opcodes, control flow, exception propagation, host interoperability, JIT compilation, heap memory, and scheduling.
- **Worked examples** from the ObjectIR V2 spec (Hello World, Fibonacci, OOP, exception handling).
- **Integration tests** with real ObjectIR modules compiled by FCC (Finite Compiler Collection).

Run tests:

```bash
dotnet test Lattice.Runtime.Tests
```

## Non-spec extensions

Lattice includes features that extend beyond the ObjectIR V2 specification. These are opt-in and do not affect conformance.

| Extension | Description | Flag |
|-----------|-------------|------|
| **Arena-based heap allocator** | Contiguous `byte[]`-backed memory for objects, strings, and arrays. Offset-based field access replaces dictionary lookups. | `--experimental heap` |
| **Malloc stdlib hook** | Explicit memory management from ObjectIR code (`Malloc.Alloc`, `Malloc.GetUsedMemory`, `Malloc.Reset`). | `--experimental manual-malloc` |
| **JIT compilation control** | Background `Reflection.Emit` compilation gated behind an explicit flag. Without it, only bytecode interpreter runs. | `--experimental jit` |
| **Experimental feature system** | `--experimental` CLI flag and `Experimental` static class for gating unstable features. | N/A (infrastructure) |

See [EXPERIMENTAL.md](EXPERIMENTAL.md) for the full experimental feature reference.

## Migration from V1 to V2

If you have ObjectIR V1 code:

- V1 uses low-level jumps (`br`, `brtrue`, `brfalse`); V2 adds structured control flow (`if`, `while`, `try`).
- V1 has no exception model; V2 adds `try`/`catch`/`finally` and `throw`.
- V2 adds better type system support and call resolution.

Lattice does not support V1. Rewrite your modules to V2 (usually straightforward; the spec includes examples).

## Migration from V2 to V3

ObjectIR V3 is in active development. It adds:

- **Generics** — Generic type parameters and instantiation.
- **Typed arrays** — Distinct `int[]`, `float[]`, etc. with type safety.
- **Inheritance-aware type tests** — `castclass` and `isinst` walk inheritance chains.
- **Method overloading on IR types** — Multiple methods per name with different signatures.
- **Module-level free functions** — Functions outside any type.

Lattice currently targets V2. V3 support is planned.

## Reporting conformance issues

If you find a case where Lattice violates the spec:

1. Check the [ObjectIR V2 Language Specification](https://github.com/Fy-nite/ObjectIR).
2. Create a minimal reproducer (small ObjectIR module that fails).
3. File an issue on the [Lattice repository](https://github.com/Fy-nite/Lattice) with:
   - Your ObjectIR module
   - Expected behavior per the spec
   - Actual behavior from Lattice
   - .NET version and OS

## References

- [ObjectIR V2 Language Specification](https://github.com/Fy-nite/ObjectIR)
- [ObjectIR V3 Specification (draft)](https://github.com/Fy-nite/ObjectIR)
- [ObjectIR.Core](https://github.com/Fy-nite/ObjectIR.Core) — IR schema and utilities
