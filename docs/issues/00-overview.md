# Lattice Runtime — Issue Inventory

Auto-generated audit of the Lattice runtime codebase. Organized by component,
then by severity within each file.

## Severity Scale

| Tag | Meaning |
|-----|---------|
| **CRIT** | Silent data corruption, wrong results, or frame/stack corruption |
| **HIGH** | Crashes, stack leaks, or resource leaks in normal usage |
| **MED** | Edge-case failures, semantic mismatches, or missing error handling |
| **LOW** | Performance, code quality, or cosmetic issues |

## Files

| Component | File | Issues |
|-----------|------|--------|
| [CPU / Interpreter](cpu.md) | `Runtime/CPU.cs` | 25 |
| [JIT Compiler](jit-compiler.md) | `Runtime/Compiler/JitCompiler.cs` | 13 |
| [Compiled Executor](compiled-executor.md) | `Runtime/Compiler/CompiledExecutor.cs` | 13 |
| [Bytecode Compiler](bytecode-compiler.md) | `Runtime/Compiler/BytecodeCompiler.cs` | 8 |
| [Scheduler](scheduler.md) | `Runtime/Scheduler.cs` | 9 |
| **Total** | | **68** |

## Priority Fix Order

Based on blast radius and severity:

1. CPU-01 (fast path pushes to wrong frame) — every compiled call is broken
2. CPU-02 (Not opcode stack corruption) — any boolean logic in bytecode
3. JIT-03 (And/Or/Xor/Shl/Shr silent no-op) — bitwise ops silently broken
4. CE-01 (locals initialized from args) — every method with locals
5. JIT-05 (ComputeMaxStack wrong for Call) — can crash at JIT time
6. JIT-06 (JitNewobj drops constructor args) — all parameterized ctors broken
7. CE-02/03 (Call stack leaks on resolution failure) — silent corruption
8. CPU-08 (no depth check on JIT/compiled paths) — stack overflow risk
9. SCHED-01 (Run hangs with no threads) — easy fix, blocks callers
10. The rest by severity.
