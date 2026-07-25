# Lattice Runtime API

## Module loading

ObjectIR modules can be loaded from TextIR source, JSON, BIR/BSON binary, or FOB/IR v3 binary. The runtime
accepts modules as `ModuleNode` AST objects (from the `ObjectIR.Core` library).

### From TextIR (`.oir`)

```csharp
using ObjectIR.Core.AST;

var module = TextIrParser.ParseModule(@"
  module HelloWorld version 1.0.0

  class Program {
    static method Main() -> void {
      ldstr ""Hello, ObjectIR!""
      call IO.Println(object) -> void
      ret
    }
  }
");
```

### From JSON (`.jir`)

```csharp
using ObjectIR.Core.Serialization;

var json = File.ReadAllText("module.jir");
var module = ModuleSerializer.LoadFromJson(json);
```

### From BIR/BSON binary (`.bir`)

```csharp
using ObjectIR.Core.Serialization;

var bytes = File.ReadAllBytes("module.bir");
var module = ModuleSerializer.LoadFromBson(bytes);
```

### From FOB/IR v3 binary (`.fob`)

```csharp
using ObjectIR.Core.Fob;
using lattice.Runtime.Compiler;

var fobBinary = FobIrReader.ReadFromFile("module.fob");
var module = ModuleBinaryReader.Read(fobBinary.Payload);
```

### Via the CLI

```powershell
lattice program.oir              # parse TextIR and run
lattice program.bir              # load BIR binary and run
lattice program.fob              # load FOB/IR v3 binary and run
lattice --compile program.oir    # compile → .fob + .bir + .jir
lattice --dump-ir program.oir    # print IR and exit
lattice --module-info program.oir  # print method/instruction summary
lattice --summary program.oir    # detailed summary report
lattice --experimental jit heap program.oir  # enable experimental features
```

See [EXPERIMENTAL.md](EXPERIMENTAL.md) for the full experimental feature system.

## The `CPU` class

The `CPU` class is the core execution engine. It is **not** in a namespace (global scope).

```csharp
// Type: global::CPU
// Namespace: (global / none)

// Construction
var cpu = new CPU();
cpu.Debug = false;          // enable instruction-level logging
cpu.MaxStackDepth = 1000;   // max frames before StackOverflowException
cpu.Scheduler = scheduler;  // attach a Scheduler (required for SpawnThread)
```

### Loading a module

```csharp
cpu.LoadModule(module);  // ModuleNode → becomes the active program + triggers CompileAll()
```

```csharp
cpu.LoadProgram("path/to/program.oir");  // shortcut: parse from file and load
```

### Invoking methods

```csharp
// Static call with return value (generic)
cpu.CallMethod<int>("Program.GetFortyTwo");
cpu.CallMethod<int>("Program.Add", 3, 7);
cpu.CallMethod<object?>("Program.Main");

// The method path is "ClassName.MethodName"
// Extra arguments are passed positionally
```

`CallMethod<T>` resolves the method by walking the class list, then tries:

1. **JIT path** (if already compiled to native IL via `System.Reflection.Emit`)
2. **Compiled interpreter path** (bytecode-compiled with `CompiledExecutor`)
3. **Fallback AST interpreter path** (interpreted statement-by-statement)

After 1000 executions of a compiled method, it is queued for background JIT compilation.

### Entry point invocation

```csharp
cpu.InitializeMain(args);  // prepares Program.Main(args) as the entry frame
```

This sets up `CurrentFrame` pointing at `Program.Main`. The scheduler then calls
`cpu.Step()` in a loop to execute.

### Single-stepping

```csharp
bool keepGoing = cpu.Step();  // execute one instruction, returns false when done
```

## The `CallStack` class

Namespace: `lattice.Core`

```csharp
var frame = new CallStack(method, thisObj);

frame.IP                         // instruction pointer (0-based)
frame.Method                     // the MethodNode being executed
frame.EvaluationStack            // Stack<object> for operand values
frame.Locals                     // Dictionary<string, object> for local variables
frame.Args                       // Dictionary<string, object> for method arguments
frame.This                       // ManagedObject? (the 'this' reference)
frame.Previous                   // CallStack? (parent frame, null = bottom)

frame.PushFrame(method, thisObj) // create a child frame
frame.PopFrame()                 // return to parent
frame.GetStackTrace()            // multi-line string trace
```

## The `ManagedObject` class

Namespace: `lattice.Core`

```csharp
var obj = new ManagedObject("ClassName");
obj.TypeName                          // string
obj.Fields                            // ConcurrentDictionary<string, object>
obj.Id                                // Guid (unique instance id)

obj.GetField("fieldName")             // object?
obj.SetField("fieldName", value)
```

## The `Scheduler` class

Namespace: `lattice.Runtime`

The scheduler manages concurrent execution of multiple `CPU` instances (one per thread).

```csharp
var scheduler = new Scheduler();
scheduler.ThreadCount                 // number of active threads

var cpu = new CPU();
cpu.Scheduler = scheduler;
cpu.LoadModule(module);
cpu.InitializeMain(args);

scheduler.AddThread(cpu);             // starts execution on a background thread
scheduler.Run();                      // blocks until all threads complete
scheduler.Run(timeoutMs);             // blocks with timeout, returns false if timed out
```

Thread spawning from ObjectIR code:

```textir
// ObjectIR code can spawn threads via the Thread.Spawn native binding
// which calls IProgramLoader.SpawnThread() on the current CPU
```

## The `Debugger` class

Namespace: `lattice.Runtime.Debugging`

Interactive command-line debugger that hooks into the execution loop.

```csharp
var debugger = new lattice.Runtime.Debugging.Debugger();
debugger.Step(cpu, instruction);  // pauses before executing instruction
```

When paused, the debugger accepts these commands:

| Command | Description |
|---------|-------------|
| `s` | Step to next instruction |
| `n [count]` | Skip `count` steps |
| `c` | Continue (run until break) |
| `i <var>` | Inspect a local variable |
| `w <var>` | Watch a variable (reports changes) |
| `p` | Print current debugger context |

Enable instruction-level logging without the interactive debugger:

```csharp
cpu.Debug = true;
```

## Host interoperability

### Native hooks via `[NativeHook]` attribute

The preferred way to expose .NET code to ObjectIR. Create a class that implements
`INativeHook` and decorate it with `[NativeHook("IRClassName")]`.

```csharp
using ObjectIR.StdLib.Core.Memory;
using ObjectIR.Core.AST;

[NativeHook("IO")]
public class IOHook : INativeHook
{
    public ClassNode GetClassNode()
    {
        var print = new MethodNode("Print",
            new[] { new ParameterNode("value", "object") },
            TypeRef.Void, isStatic: true,
            nativeImpl: new NativeMethod(args =>
            {
                Console.Write(args[0].Data);
                return new Value<object>(null);
            }));

        var println = new MethodNode("Println",
            new[] { new ParameterNode("value", "object") },
            TypeRef.Void, isStatic: true,
            nativeImpl: new NativeMethod(args =>
            {
                Console.WriteLine(args.Length > 0 ? args[0].Data : "");
                return new Value<object>(null);
            }));

        return new ClassNode("IO", methods: new[] { print, println });
    }
}
```

Register hooks before executing any ObjectIR code:

```csharp
NativeRegistry.RegisterFromAssembly(typeof(IOHook).Assembly);
```

The `NativeRegistry` scans for all `[NativeHook]`-attributed classes in the
assembly and makes them available for on-demand resolution. When ObjectIR code
references a class like `IO`, `CPU.ResolveMethod` calls `NativeRegistry.TryRegister`,
which instantiates the hook and merges its `ClassNode` into the program AST.

### Call resolution order

When the CPU encounters a method call, it resolves in this order:

1. **Local AST resolution** — search the loaded module's class hierarchy
2. **Native hook registration** — on-demand via `NativeRegistry.TryRegister`
3. **Fallback** — `MethodResolutionException` if nothing matches

### Native methods

Each method on a native hook is backed by a `NativeMethod` delegate:

```csharp
new NativeMethod(args =>
{
    // args is Value<object>[]
    // args[i].Data contains the unwrapped object
    return new Value<object>(result);
});
```

## Compilation pipeline

Lattice compiles ObjectIR bytecode in three tiers:

```
TextIR/JSON/BIR
    ↓
TextIrParser.ParseModule / ModuleSerializer
    ↓
ModuleNode (AST)
    ↓
CPU.LoadModule
    ↓
BytecodeCompiler (transforms AST → CompiledMethod)
    ↓
CompiledExecutor (interprets CompiledMethod)
    ↓  (after 1000 executions, background thread)
JitCompiler (System.Reflection.Emit → native IL delegate)
```

### Key types

| Type | Namespace | Role |
|------|-----------|------|
| `BytecodeCompiler` | `lattice.Runtime.Compiler` | Transforms `ModuleNode` AST → `CompiledMethod` |
| `CompiledMethod` | `lattice.Runtime.Compiler` | Optimized instruction representation |
| `CompiledExecutor` | `lattice.Runtime.Compiler` | Interprets `CompiledMethod` |
| `CompilationCache` | `lattice.Runtime.Compiler` | Stores compiled/JIT methods, tracks execution counts |
| `JitCompiler` | `lattice.Runtime.Compiler` | Emits native IL via `System.Reflection.Emit` |
| `StackValue` | `lattice.Runtime.Compiler` | Typed value wrapper used in compiled execution |
| `ModuleBinaryWriter` | `lattice.Runtime.Compiler` | Serializes `ModuleNode` → FOB payload bytes |
| `ModuleBinaryReader` | `lattice.Runtime.Compiler` | Deserializes FOB payload → `ModuleNode` |

```csharp
// Access the compilation cache
cpu.Cache.GetCompiled(method);   // CompiledMethod?
cpu.Cache.GetJit(method);        // JittedMethod? (native delegate)
```

## Exception hierarchy

All Lattice exceptions derive from `LatticeException` (namespace `lattice.Throwables`):

| Exception | Description |
|-----------|-------------|
| `LatticeException` | Base class for all Lattice errors |
| `RuntimeException` | General runtime error during execution |
| `OpCodeNotFoundException` | Unknown opcode encountered |
| `MethodResolutionException` | Method reference could not be resolved |
| `EntrypointNotFoundException` | `Program.Main` not found |
| `LatticeStackOverflowException` | Call stack exceeded `MaxStackDepth` |

```csharp
try
{
    cpu.CallMethod<int>("Program.Run");
}
catch (LatticeException ex)
{
    Console.Error.WriteLine($"Lattice error: {ex.Message}");
}
```

## Reference

### CPU members

| Member | Description |
|--------|-------------|
| `LoadModule(ModuleNode)` | Load a module, triggers `CompileAll()` |
| `LoadProgram(string path)` | Parse a `.oir` file and load it |
| `CallMethod<T>(string path, params object[] args)` | Invoke a method by `"ClassName.MethodName"` path |
| `InitializeMain(string[] args)` | Set up `Program.Main(args)` as entry frame |
| `Step()` | Execute one instruction, returns false when frame stack is empty |
| `PushFrame(MethodNode, ManagedObject?, object[]?)` | Push a new call frame |
| `Debug` | Enable instruction-level console logging |
| `MaxStackDepth` | Maximum call stack depth (default 1000) |
| `Scheduler` | Attached `Scheduler` for thread support |
| `Cache` | `CompilationCache` for compiled/JIT methods |
| `Heap` | `HeapAllocator?` — arena-based heap (null = disabled, default) |
| `Modules` | `List<ModuleNode>` of all loaded modules |
| `program` | `ModuleNode` — the currently active module |
| `CurrentFrame` | `CallStack?` — the top of the call stack |

### Scheduler members

| Member | Description |
|--------|-------------|
| `AddThread(CPU)` | Start executing a CPU on a background thread |
| `Run()` | Block until all threads complete |
| `Run(int timeoutMs)` | Block with timeout, returns false on timeout |
| `Run(TimeSpan timeout)` | Block with timeout, returns false on timeout |
| `ThreadCount` | Number of currently active threads |

### Debugger members

| Member | Description |
|--------|-------------|
| `Step(CPU, Statement)` | Pause before the given statement |

## Experimental features

New or unstable features are gated behind `--experimental` on the CLI. See [EXPERIMENTAL.md](EXPERIMENTAL.md) for the full reference.

```csharp
using lattice.Runtime;

// Set features programmatically
Experimental.Set(ExperimentalFeature.Jit | ExperimentalFeature.Heap);

// Check at runtime
if (Experimental.IsEnabled(ExperimentalFeature.Heap))
{
    cpu.Heap = new HeapAllocator();
}
```

### CLI

```powershell
lattice --experimental jit heap manual-malloc program.oir
```

| Flag | Effect |
|------|--------|
| `jit` | Enable JIT compilation (background `Reflection.Emit` after 1000 calls) |
| `heap` | Enable arena-based heap allocator on the CPU |
| `manual-malloc` | Enable `Malloc` stdlib hook (implies `heap`) |
| `generalized-jit` | Enable generalized JIT path (all opcodes, static helpers) |
| `native-transpile` | Enable the C transpiler |

Without `--experimental`, the runtime uses the bytecode interpreter only (no JIT, no heap allocator).

## Heap allocator

The heap allocator provides a bump-allocated `MemoryArena` for ObjectIR objects, strings, and arrays. It is opt-in: set `cpu.Heap` to activate.

### Quick start

```csharp
using lattice.Runtime;
using lattice.Runtime.Memory;

Experimental.Set(ExperimentalFeature.Heap);

var cpu = new CPU();
cpu.Heap = new HeapAllocator();  // 1MB arena, default
cpu.Scheduler = new Scheduler();
cpu.LoadModule(module);
cpu.InitializeMain(args);
```

### C# API

```csharp
using lattice.Runtime.Memory;

var heap = new HeapAllocator(arenaCapacity: 1024 * 1024);

// Objects
var obj = heap.NewObject("Point", new Dictionary<string, object?> { ["x"] = 10, ["y"] = 20 });
var map = heap.GetFieldMap("Point");
int xVal = new HeapObject(heap.Arena, obj.Handle).GetField(map["x"]).AsInt;  // 10

// Strings
var str = heap.NewString("hello");
string s = str.Value;  // "hello"

// Arrays
var arr = heap.NewArray(HeapArrayElementKind.Int, 10);
arr.Count = 3;
arr.SetElement(0, FieldValue.FromInt(42));

// Query
heap.UsedBytes   // bytes consumed
heap.FreeBytes   // bytes remaining
heap.Reset();    // reclaim all
```

### ObjectIR (Malloc hook)

With `--experimental manual-malloc`:

```textir
class Program {
  static method Main() -> void {
    call Malloc.Alloc("Point") -> object
    // ... stack has a new ManagedObject
    ret
  }
}
```

### Memory layout

| Type | Layout |
|------|--------|
| HeapObject | `[typeId:4][fieldCount:4][field0:8][field1:8]...` |
| HeapString | `[charLen:4][byteLen:4][UTF-8 bytes]` |
| HeapArray | `[count:4][capacity:4][elementKind:1][pad:1][elements:8*N]` |
| FieldValue | `[kind:1][pad:3][data:4]` = 8 bytes total |

See [EXPERIMENTAL.md](EXPERIMENTAL.md) for the full layout specification and caveats.
