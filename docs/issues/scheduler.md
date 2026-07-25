# Scheduler.cs — Issues

File: `Lattice.Runtime/Runtime/Scheduler.cs`

---

## HIGH-01: Run() hangs forever if called with no active/finished threads

**Lines:** 13, 61

`_allDone` is initialized non-signaled (`false`). If `Run()` is called when
no threads exist (or before any added thread has finished), it blocks forever.
No thread will ever call `_allDone.Set()`.

```csharp
private readonly ManualResetEventSlim _allDone = new ManualResetEventSlim(false);

public void Run()
{
    _allDone.Wait();  // blocks forever if nobody will ever set it
}
```

**Fix:**
```csharp
public void Run()
{
    lock (_lock)
    {
        if (_threads.Count == 0)
            return;
    }
    _allDone.Wait();
}
```

---

## HIGH-02: Zombie entry if thread start fails

**Lines:** 24–25, 56

The `cpu` is added to `_threads` and `_allDone` is reset **inside the lock**,
but the thread is created and started **outside the lock**. If `new Thread(...)`
or `t.Start()` throws (e.g., `OutOfMemoryException`, `ThreadStateException`),
the cpu remains in the list with no thread running it. Nobody will ever remove
it or signal `_allDone`, so `Run()` hangs forever.

```csharp
lock (_lock)
{
    _threads.Add(cpu);      // committed
    _allDone.Reset();       // committed
}
// ← if anything below throws, cpu is a zombie
Thread t = new Thread(() => { ... });
t.IsBackground = true;
t.Start();                  // can throw ThreadStateException
```

**Fix:** Move thread creation inside the lock, or wrap `t.Start()` in try/catch
that removes the cpu on failure.

---

## HIGH-03: Thread exceptions silently lost to stderr

**Lines:** 38–41, 52

When `cpu.Step()` throws, the exception is caught and stored, but only written
to `Console.Error`. `Run()` returns normally as if all threads completed
successfully. The caller has no way to know a thread crashed.

```csharp
catch (Exception ex)
{
    threadException = ex;
}
// ...
if (threadException != null)
    Console.Error.WriteLine($"[Scheduler] Thread crashed: {threadException}");
    // lost — never surfaces to caller
```

**Fix:** Collect thread exceptions and expose them:
```csharp
private readonly ConcurrentBag<Exception> _threadExceptions = new();

// In thread:
catch (Exception ex) { _threadExceptions.Add(ex); }

// In Run():
public void Run()
{
    _allDone.Wait();
    if (!_threadExceptions.IsEmpty)
        throw new AggregateException(_threadExceptions);
}
```

---

## HIGH-04: Timeout doesn't stop threads

**Lines:** 33, 66

When `Run(timeoutMs)` returns `false` (timeout expired), the worker threads
keep running. There is no `CancellationToken`, no `Thread.Interrupt()`, no way
to join or stop them. They become orphaned background threads.

```csharp
public bool Run(int timeoutMs)
{
    return _allDone.Wait(timeoutMs);
    // threads at line 33 are still spinning in while(cpu.Step())
}
```

**Fix:** Pass a `CancellationToken` into the thread loop and cancel it on
timeout:
```csharp
private CancellationTokenSource? _cts;

public bool Run(int timeoutMs)
{
    _cts = new CancellationTokenSource();
    // ... start threads with _cts.Token ...
    bool done = _allDone.Wait(timeoutMs);
    if (!done) _cts.Cancel();
    return done;
}
```

---

## MED-01: ManualResetEventSlim never disposed

**Lines:** 13

`ManualResetEventSlim` implements `IDisposable` and holds a kernel wait
handle (allocated lazily on first `Wait`). The `Scheduler` class never
disposes it, leaking the underlying kernel object.

**Fix:** Implement `IDisposable` on `Scheduler`:
```csharp
public class Scheduler : IDisposable
{
    private readonly ManualResetEventSlim _allDone = new(false);

    public void Dispose()
    {
        _allDone.Dispose();
        _cts?.Dispose();
    }
}
```

---

## MED-02: No external thread removal mechanism

**Entire file**

There is no `RemoveThread` method. The only way a thread leaves `_threads`
is by exiting its own loop in the `finally` block. There is no way to
externally stop, interrupt, or remove a specific running thread.

Combined with the timeout issue (HIGH-04), threads outlive any timeout.

---

## LOW-01: Thread.Yield() ineffective for CPU-bound work

**Line:** 35

`Thread.Yield()` only hints to the OS scheduler to give time to other threads
**on the same processor**. On a multi-core system, it has essentially no
effect — the thread just continues on its core.

**Fix:** Use `Thread.Sleep(0)` or `Thread.SpinWait()`, or better, use
cooperative cancellation.

---

## LOW-02: _allDone.Reset() in AddThread can surprise callers

**Line:** 25

If `Run()` returned (all threads done), and then `AddThread` is called, the
event is reset. A subsequent `Run()` blocks again. The API gives no indication
that `Run()` can return and then require calling again.

**Fix:** Document this behavior, or provide an `IsRunning` property.

---

## LOW-03: No state reset between Run() calls

**Line:** 61

There's no way to "reset" the scheduler between `Run()` calls except by
calling `AddThread`, which couples thread management with event management.

**Fix:** Add a `Reset()` method, or document that `AddThread` + `Run()` is
the intended pattern.
