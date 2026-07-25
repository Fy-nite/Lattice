# Lattice Patch Notes

## vNext — Heap allocator, experimental features, codebase audit

### New: Experimental feature system

A `--experimental` CLI flag gates new and unstable features. Features are opt-in, space-separated:

```powershell
lattice program.oir --experimental jit heap manual-malloc
```

Available flags: `jit`, `heap`, `manual-malloc`, `generalized-jit`, `native-transpile`.

Programmatic API:

```csharp
Experimental.Set(ExperimentalFeature.Heap | ExperimentalFeature.Jit);
Experimental.IsEnabled(ExperimentalFeature.Heap); // true
```

See [EXPERIMENTAL.md](EXPERIMENTAL.md) for full documentation.

### New: Arena-based heap allocator

A `Memory<byte>`-backed heap allocator with per-thread arenas. Objects, strings, and arrays are stored in contiguous memory with offset-based field access, replacing `ConcurrentDictionary` lookups.

**New files:**

| File | Purpose |
|------|---------|
| `Runtime/Memory/HeapHandle.cs` | Offset-based handle into the arena |
| `Runtime/Memory/FieldValue.cs` | Tagged union (int/float/bool/handle), 8 bytes on-disk |
| `Runtime/Memory/MemoryArena.cs` | Bump allocator over `byte[]`, 1MB default, 4-byte aligned |
| `Runtime/Memory/TypeRegistry.cs` | Type name → integer ID mapping |
| `Runtime/Memory/HeapObject.cs` | Object layout: `[typeId:4][fieldCount:4][fields:N×8]` |
| `Runtime/Memory/HeapString.cs` | String layout: `[charLen:4][byteLen:4][utf8:N]` |
| `Runtime/Memory/HeapArray.cs` | Array layout: `[count:4][capacity:4][elementKind:1][elements:N×8]` |
| `Runtime/Memory/HeapAllocator.cs` | Per-thread arena + type registry + field maps |
| `Runtime/Stdlib/MallocHook.cs` | `[NativeHook("Malloc")]` — `Alloc`, `GetUsedMemory`, `GetFreeMemory`, `Reset` |

**Modified files:**

| File | Change |
|------|--------|
| `ManagedObject.cs` | Added `Heap?` + `HeapHandle` properties; `GetField`/`SetField` use arena when heap is set, `ConcurrentDictionary` otherwise |
| `CPU.cs` | Added `Heap` property; `NewObj` creates heap objects when heap is set; JIT gated behind `ExperimentalFeature.Jit`; `SpawnThread` copies heap ref |
| `CompiledExecutor.cs` | Same `NewObj` heap path as CPU |
| `Program.cs` | Added `--experimental` CLI option; heap initialization; MallocHook assembly registration |

**Backward compatible.** When `CPU.Heap` is null (default), everything works exactly as before. The heap is fully opt-in.

### New: Malloc stdlib hook

The `Malloc` class is available as a native hook when the `manual-malloc` experimental flag is enabled:

```textir
Malloc.Alloc("ClassName")    // allocate a heap object
Malloc.GetUsedMemory()       // arena used bytes
Malloc.GetFreeMemory()       // arena free bytes
Malloc.Reset()               // reset arena
```

### Modified: JIT gated behind experimental flag

JIT compilation (both the background compiler and the runtime dispatch) is now gated behind `--experimental jit`. Without this flag:

- `CompiledExecutor` still runs bytecode-compiled methods (tier 2)
- The JIT path is never entered
- No background compilation is queued

With `--experimental jit`, behavior is identical to previous versions.

### Codebase audit: 68 issues documented

A full audit of CPU.cs, JitCompiler.cs, CompiledExecutor.cs, BytecodeCompiler.cs, and Scheduler.cs identified 68 issues across severity levels. Issues are documented in `docs/issues/`:

| File | Issues | Critical |
|------|--------|----------|
| `docs/issues/cpu.md` | 25 | 2 (wrong frame on fast path, Not stack corruption) |
| `docs/issues/jit-compiler.md` | 13 | 1 (And/Or/Xor/Shl/Shr no-op) |
| `docs/issues/compiled-executor.md` | 13 | 1 (locals initialized from args) |
| `docs/issues/bytecode-compiler.md` | 8 | 0 |
| `docs/issues/scheduler.md` | 9 | 0 |

See `docs/issues/00-overview.md` for the priority fix order.

### Tests

- 313 total tests (274 original + 39 new heap memory tests)
- 0 failures
- New test file: `HeapMemoryTests.cs` — covers MemoryArena, FieldValue, HeapObject, HeapString, HeapArray, HeapAllocator, TypeRegistry
