using System.Diagnostics;
using System.Globalization;
using lattice;
using ObjectIR.Core.AST;

namespace ObjectIR.Benchmark;

public static class Program
{
    private const int SumN = 30_000;
    private const int BranchN = 100_000;
    private const int FibN = 22;

    private const string ModuleSource = """
        module Bench

        class Program {
            static method SumLoop(n: int32) -> int32 {
                local sum: int32
                local i: int32
                ldc.i4 0
                stloc sum
                ldc.i4 0
                stloc i
                ldloc i
                ldarg n
                clt
                while (stack) {
                    ldloc sum
                    ldloc i
                    add
                    stloc sum
                    ldloc i
                    ldc.i4 1
                    add
                    stloc i
                    ldloc i
                    ldarg n
                    clt
                }
                ldloc sum
                ret
            }

            static method BranchLoop(n: int32) -> int32 {
                local count: int32
                local i: int32
                local r: int32
                ldc.i4 0
                stloc count
                ldc.i4 0
                stloc i
                ldloc i
                ldarg n
                clt
                while (stack) {
                    ldloc i
                    ldc.i4 2
                    rem
                    stloc r
                    ldloc r
                    ldc.i4 0
                    ceq
                    if (stack) {
                        ldloc count
                        ldc.i4 1
                        add
                        stloc count
                    }
                    ldloc i
                    ldc.i4 1
                    add
                    stloc i
                    ldloc i
                    ldarg n
                    clt
                }
                ldloc count
                ret
            }

            static method Fib(n: int32) -> int32 {
                ldarg n
                ldc.i4 2
                clt
                if (stack) {
                    ldarg n
                    ret
                }
                ldarg n
                ldc.i4 1
                sub
                call Program.Fib(int32) -> int32
                ldarg n
                ldc.i4 2
                sub
                call Program.Fib(int32) -> int32
                add
                ret
            }

            static method Five() -> int32 {
                ldc.i4 5
                ret
            }

            static method CallInsideIf() -> int32 {
                local x: int32
                ldc.i4 0
                stloc x
                ldloc x
                ldc.i4 0
                ceq
                if (stack) {
                    call Program.Five() -> int32
                    stloc x
                }
                ldloc x
                ret
            }
        }
        """;

    public static void Main()
    {
        Console.WriteLine("ObjectIR / Lattice interpreter benchmark");
        Console.WriteLine($"Runtime: .NET {Environment.Version}, {(Environment.Is64BitProcess ? "x64" : "x86")}, release={IsRelease()}");
        Console.WriteLine();

        var rt = Silently(() =>
        {
            var r = new CPU();
            var module = TextIrParser.ParseModule(ModuleSource);
            r.LoadModule(module);
            return r;
        });

        // Untimed warmups
        Silently(() => rt.CallMethod<int>("Program.SumLoop", 1000));
        Silently(() => rt.CallMethod<int>("Program.BranchLoop", 1000));
        Silently(() => rt.CallMethod<int>("Program.Fib", 10));

        var lua = SetupLua();

        var rows = new List<Row>
        {
            Bench("SumLoop (300k iterations)",
                ops: SumN, opUnit: "iteration",
                objectIr: () => rt.CallMethod<int>("Program.SumLoop", SumN),
                csharp: () => SumLoopCs(SumN),
                luaFn: lua.sum, luaArg: SumN),

            Bench("BranchLoop (100k iterations, if in loop)",
                ops: BranchN, opUnit: "iteration",
                objectIr: () => rt.CallMethod<int>("Program.BranchLoop", BranchN),
                csharp: () => BranchLoopCs(BranchN),
                luaFn: lua.branch, luaArg: BranchN),

            Bench($"Fib({FibN}) (57,313 recursive calls)",
                ops: CallCount(FibN), opUnit: "call",
                objectIr: () => rt.CallMethod<int>("Program.Fib", FibN),
                csharp: () => FibCs(FibN),
                luaFn: lua.fib, luaArg: FibN),
        };

        PrintTable(rows);
    }

    private sealed record Row(
        string Name, long Ops, string OpUnit,
        double IrMs, long IrAlloc, long IrResult,
        double CsMs, long CsResult,
        double? LuaMs, double? LuaResult);

    private static Row Bench(string name, long ops, string opUnit,
        Func<long> objectIr, Func<long> csharp,
        MoonSharp.Interpreter.DynValue? luaFn, int luaArg)
    {
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        long irResult = Silently(objectIr);
        sw.Stop();
        long irAlloc = GC.GetAllocatedBytesForCurrentThread() - a0;
        double irMs = sw.Elapsed.TotalMilliseconds;

        long csResult = csharp();
        double csMs = MeasurePerInvocation(() => csharp());

        double? luaMs = null;
        double? luaResult = null;
        if (luaFn != null)
        {
            var script = _luaScript!;
            luaResult = script.Call(luaFn, luaArg).Number;
            luaMs = MeasurePerInvocation(() => script.Call(luaFn, luaArg));
        }

        return new Row(name, ops, opUnit, irMs, irAlloc, irResult, csMs, csResult, luaMs, luaResult);
    }

    private static double MeasurePerInvocation(Action body)
    {
        body();
        int reps = 1;
        while (true)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < reps; i++) body();
            sw.Stop();
            if (sw.Elapsed.TotalMilliseconds >= 250)
                return sw.Elapsed.TotalMilliseconds / reps;
            reps *= 4;
        }
    }

    private static long SumLoopCs(int n)
    {
        long sum = 0;
        long i = 0;
        while (i < n) { sum += i; i += 1; }
        return sum;
    }

    private static long BranchLoopCs(int n)
    {
        long count = 0;
        long i = 0;
        while (i < n)
        {
            long r = i % 2;
            if (r == 0) count += 1;
            i += 1;
        }
        return count;
    }

    private static long FibCs(int n) => n < 2 ? n : FibCs(n - 1) + FibCs(n - 2);

    private static long CallCount(int n) => n < 2 ? 1 : CallCount(n - 1) + CallCount(n - 2) + 1;

    private static MoonSharp.Interpreter.Script? _luaScript;

    private static (MoonSharp.Interpreter.DynValue? sum, MoonSharp.Interpreter.DynValue? branch, MoonSharp.Interpreter.DynValue? fib) SetupLua()
    {
        var script = new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.None);
        script.DoString("""
            function sumloop(n)
                local s = 0
                local i = 0
                while i < n do s = s + i i = i + 1 end
                return s
            end
            function branchloop(n)
                local c = 0
                local i = 0
                while i < n do
                    if i % 2 == 0 then c = c + 1 end
                    i = i + 1
                end
                return c
            end
            function fib(n)
                if n < 2 then return n end
                return fib(n - 1) + fib(n - 2)
            end
            """);
        _luaScript = script;
        return (script.Globals.Get("sumloop"), script.Globals.Get("branchloop"), script.Globals.Get("fib"));
    }

    private static void PrintTable(List<Row> rows)
    {
        var ci = CultureInfo.InvariantCulture;
        foreach (var r in rows)
        {
            double irPerOpUs = r.IrMs * 1000.0 / r.Ops;
            double csPerOpNs = r.CsMs * 1_000_000.0 / r.Ops;
            double slowdown = r.IrMs / r.CsMs;
            double allocPerOp = (double)r.IrAlloc / r.Ops;

            Console.WriteLine(r.Name);
            Console.WriteLine($"  ObjectIR : {r.IrMs.ToString("F1", ci),10} ms   ({irPerOpUs.ToString("F2", ci)} us per {r.OpUnit}, {FormatBytes(allocPerOp)} allocated per {r.OpUnit})");
            Console.WriteLine($"  C#       : {r.CsMs.ToString("F4", ci),10} ms   ({csPerOpNs.ToString("F2", ci)} ns per {r.OpUnit})");
            if (r.LuaMs is double lm)
            {
                double luaSlow = lm > 0 ? r.IrMs / lm : 0;
                Console.WriteLine($"  Lua      : {lm.ToString("F3", ci),10} ms   (MoonSharp, pure C# Lua interpreter)");
                Console.WriteLine($"  ObjectIR is {slowdown.ToString("N0", ci)}x slower than C#, {luaSlow.ToString("N0", ci)}x slower than Lua");
            }
            else
            {
                Console.WriteLine($"  ObjectIR is {slowdown.ToString("N0", ci)}x slower than C#");
            }

            bool match = r.IrResult == r.CsResult && (!r.LuaResult.HasValue || (long)r.LuaResult.Value == r.CsResult);
            Console.WriteLine(match ? "  results match" : $"  RESULT MISMATCH (ObjectIR={r.IrResult}, C#={r.CsResult}, Lua={r.LuaResult})");
            Console.WriteLine();
        }
    }

    private static string FormatBytes(double b)
    {
        var ci = CultureInfo.InvariantCulture;
        if (b >= 1024 * 1024) return (b / (1024 * 1024)).ToString("F1", ci) + " MB";
        if (b >= 1024) return (b / 1024).ToString("F1", ci) + " KB";
        return b.ToString("F0", ci) + " B";
    }

    private static T Silently<T>(Func<T> f)
    {
        var old = Console.Out;
        Console.SetOut(TextWriter.Null);
        try { return f(); }
        finally { Console.SetOut(old); }
    }

    private static bool IsRelease()
    {
#if DEBUG
        return false;
#else
        return true;
#endif
    }
}
