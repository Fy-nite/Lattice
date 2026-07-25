# Stdlib Reference

The Lattice runtime exposes native functions through a `[NativeHook]` attribute system.
All functions are registered at startup via `NativeRegistry.RegisterFromAssembly`.

---

## IO — Console input/output

| Method | Parameters | Returns | Description |
|---|---|---|---|
| `IO.Print(object)` | value | void | Writes to console (no newline) |
| `IO.Println(object)` | value | void | Writes to console (with newline) |
| `IO.Readln()` | — | string | Reads a line from stdin |

---

## Malloc — Heap allocator control

| Method | Parameters | Returns | Description |
|---|---|---|---|
| `Malloc.Alloc(string)` | typeName | object | Allocates a managed heap object |
| `Malloc.GetUsedMemory()` | — | int | Bytes currently used on the heap |
| `Malloc.GetFreeMemory()` | — | int | Bytes currently free on the heap |
| `Malloc.Reset()` | — | void | Resets heap allocator to initial state |

---

## Thread — Concurrency

| Method | Parameters | Returns | Description |
|---|---|---|---|
| `Thread.Spawn(IDelagate)` | delegate | void | Spawns a new execution thread |
| `Thread.Sleep(int32)` | ms | void | Yields execution for ms milliseconds |

---

## Action / Func / Delegate — Callable wrappers

| Class | Method | Parameters | Returns | Description |
|---|---|---|---|---|
| `Action` | constructor(object, string) | instance, methodName | void | Wraps an instance method as an Action |
| `Action` | `Invoke()` | — | void | Invokes the wrapped method |
| `Func` | constructor(object, string) | instance, methodName | void | Wraps an instance method as a Func |
| `Func` | `Invoke()` | — | object | Invokes the wrapped method, returns result |
| `Delegate` | constructor(object, string) | target, methodName | void | Stores target + methodName for later resolution by Thread.Spawn |
