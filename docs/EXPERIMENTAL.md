# Experimental Features

Lattice gates new or unstable features behind an `--experimental` flag. Experimental features are opt-in: they are disabled by default and must be explicitly enabled per session.

## CLI usage

Pass space-separated feature names after `--experimental`:

```powershell
lattice program.oir --experimental jit heap manual-malloc
lattice program.oir --experimental heap
lattice program.oir   # no experimental features (baseline)
```

Available features:

| Feature | Description |
|---------|-------------|
| `jit` | Enable JIT compilation (Reflection.Emit). Without this flag, methods run through the bytecode interpreter only. |
| `heap` | Enable the arena-based heap allocator (`HeapAllocator`). Objects are allocated in contiguous memory with offset-based field access. |
| `manual-malloc` | Enable the `Malloc` stdlib hook (also implies `heap`). Provides explicit memory management from ObjectIR code. |
| `generalized-jit` | Enable the generalized JIT path (all opcodes, static helpers). Without this flag, only int-only JIT runs. |
| `native-transpile` | Enable the C transpiler for ObjectIR-to-native compilation. |

Multiple features can be combined:

```powershell
# JIT + heap + manual malloc
lattice program.oir --experimental jit heap manual-malloc

# Heap only (no JIT, bytecode interpreter only)
lattice program.oir --experimental heap

# Just JIT (default interpreter + native code for hot paths)
lattice program.oir --experimental jit
```

## Runtime API

### `ExperimentalFeature` enum

```csharp
using lattice.Runtime;

[Flags]
public enum ExperimentalFeature
{
    None             = 0,
    Jit              = 1 << 0,
    Heap             = 1 << 1,
    ManualMalloc     = 1 << 2,
    GeneralizedJit   = 1 << 3,
    NativeTranspile  = 1 << 4,
}
```

### `Experimental` static class

```csharp
using lattice.Runtime;

// Set active features (replaces any previous set)
Experimental.Set(ExperimentalFeature.Jit | ExperimentalFeature.Heap);

// Check if a feature is enabled
bool jitOn = Experimental.IsEnabled(ExperimentalFeature.Jit);   // true
bool heapOn = Experimental.IsEnabled(ExperimentalFeature.Heap); // true

// Toggle individual features
Experimental.Enable(ExperimentalFeature.ManualMalloc);
Experimental.Disable(ExperimentalFeature.Jit);

// Parse from CLI-style strings (case-insensitive)
var features = Experimental.Parse(new[] { "jit", "heap" });
// Equivalent to ExperimentalFeature.Jit | ExperimentalFeature.Heap
```

When using the CLI, `--experimental` calls `Experimental.Parse()` and `Experimental.Set()` automatically before the program runs.

## Feature details

### `jit` — JIT compilation

When enabled, methods that execute 1000 times are queued for background compilation via `System.Reflection.Emit`. Compiled methods are cached and called directly on subsequent invocations.

When disabled (default), `CompiledExecutor` still runs bytecode-compiled methods, but the JIT path is never entered. The execution order becomes:

```
AST interpreter → BytecodeCompiler → CompiledExecutor (always)
                                     JitCompiler (only with --experimental jit)
```

### `heap` — Arena-based heap allocator

Enables `HeapAllocator` on the CPU. When active:

- `NewObj` instructions allocate objects in a contiguous memory arena instead of using `ConcurrentDictionary`
- Field access becomes offset arithmetic (one dictionary lookup per type, then direct memory read/write)
- `ManagedObject.GetField`/`SetField` use arena-backed storage when `Heap != null`

The heap is per-CPU (per-thread). Each CPU gets its own `MemoryArena` (1MB default). Cross-thread object sharing is not yet supported.

```csharp
// Programmatic setup (without CLI)
using lattice.Runtime;
using lattice.Runtime.Memory;

Experimental.Set(ExperimentalFeature.Heap);

var cpu = new CPU();
cpu.Heap = new HeapAllocator();  // 1MB arena
cpu.LoadModule(module);
```

### `manual-malloc` — Malloc stdlib hook

Enables the `Malloc` native hook, which is automatically registered when `NativeRegistry` scans the Lattice.Runtime assembly. Provides ObjectIR-visible memory management:

```textir
// In ObjectIR code:
Malloc.Alloc("ClassName")    // allocate a heap object, returns ManagedObject
Malloc.GetUsedMemory()       // returns arena used bytes (int)
Malloc.GetFreeMemory()       // returns arena free bytes (int)
Malloc.Reset()               // reset arena (frees all allocations)
```

### C# API

```csharp
using lattice.Runtime.Memory;

// Create an allocator
var heap = new HeapAllocator(arenaCapacity: 1024 * 1024);

// Allocate objects
var obj = heap.NewObject("Point", new Dictionary<string, object?>
{
    ["x"] = 10,
    ["y"] = 20,
});

// Read fields back via offset arithmetic
var fieldMap = heap.GetFieldMap("Point");
int xIndex = fieldMap["x"];  // 0
var raw = new HeapObject(heap.Arena, obj.Handle);
int x = raw.GetField(xIndex).AsInt;  // 10

// Allocate strings
var str = heap.NewString("hello");
string value = str.Value;  // "hello"

// Allocate arrays
var arr = heap.NewArray(HeapArrayElementKind.Int, 10);
arr.Count = 3;
arr.SetElement(0, FieldValue.FromInt(42));
int elem = arr.GetElement(0).AsInt;  // 42
```

## Memory layout

### HeapObject

```
[typeId: int32] [fieldCount: int32] [field0: FieldValue] [field1: FieldValue] ...
```

Each `FieldValue` is 8 bytes: 1 byte kind + 3 bytes padding + 4 bytes data (or 1 + 4 with 3 bytes alignment padding).

Field access: `objectHandle + 8 + (fieldIndex * 8)`

### HeapString

```
[charCount: int32] [byteCount: int32] [UTF-8 bytes...]
```

### HeapArray

```
[count: int32] [capacity: int32] [elementKind: byte] [padding: 1 byte] [elements...]
```

Each element is one `FieldValue` (8 bytes).

## Type system

The `TypeRegistry` assigns a unique integer ID to each type name. The `HeapAllocator` maintains a per-type field map (`Dictionary<string, int>`) that maps field names to their index. Field indices are stable for a given type — `GetFieldIndex("Point", "x")` always returns the same index for a given allocator instance.

```csharp
var heap = new HeapAllocator();
int pointId = heap.GetOrCreateTypeId("Point");  // 1
int vectorId = heap.GetOrCreateTypeId("Vector"); // 2
int pointId2 = heap.GetOrCreateTypeId("Point");  // 1 (same)

var map = heap.GetFieldMap("Point");
map["x"] = 0;
map["y"] = 1;
```

## Caveats

- **No garbage collection.** The arena is bump-allocated. `Malloc.Reset()` reclaims everything at once. There is no per-object free.
- **No cross-thread sharing.** Each CPU gets its own arena. Objects cannot be shared between threads (future: copy-on-share).
- **Field values are unboxed.** Fields store `FieldValue` (tagged union), not boxed objects. Complex .NET reference types are not supported — only `int`, `float`, `bool`, and `HeapHandle`.
- **Backward compatible.** When `Heap` is null on a `ManagedObject`, `GetField`/`SetField` fall back to the existing `ConcurrentDictionary`. All existing code works unchanged.
