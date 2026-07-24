using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using lattice;
using ObjectIR.Core.AST;

namespace ObjectIR.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<LatticeBenchmarks>(args: args);
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[SimpleJob]
public class LatticeBenchmarks
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
        }
        """;

    private CPU _cpu = null!;
    private MoonSharp.Interpreter.Script _luaScript = null!;
    private MoonSharp.Interpreter.DynValue _luaSumFn = null!;
    private MoonSharp.Interpreter.DynValue _luaBranchFn = null!;
    private MoonSharp.Interpreter.DynValue _luaFibFn = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cpu = new CPU();
        var module = TextIrParser.ParseModule(ModuleSource);
        _cpu.LoadModule(module);

        _luaScript = new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.None);
        _luaScript.DoString("""
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
        _luaSumFn = _luaScript.Globals.Get("sumloop")!;
        _luaBranchFn = _luaScript.Globals.Get("branchloop")!;
        _luaFibFn = _luaScript.Globals.Get("fib")!;

        ValidateResults();
    }

    private void ValidateResults()
    {
        int irSum = _cpu.CallMethod<int>("Program.SumLoop", SumN);
        long csSum = SumLoopCs(SumN);
        long luaSum = (long)_luaScript.Call(_luaSumFn, SumN).Number;
        if (irSum != csSum || csSum != luaSum)
            throw new Exception($"SumLoop mismatch: ObjectIR={irSum}, C#={csSum}, Lua={luaSum}");

        int irBranch = _cpu.CallMethod<int>("Program.BranchLoop", BranchN);
        long csBranch = BranchLoopCs(BranchN);
        long luaBranch = (long)_luaScript.Call(_luaBranchFn, BranchN).Number;
        if (irBranch != csBranch || csBranch != luaBranch)
            throw new Exception($"BranchLoop mismatch: ObjectIR={irBranch}, C#={csBranch}, Lua={luaBranch}");

        int irFib = _cpu.CallMethod<int>("Program.Fib", FibN);
        long csFib = FibCs(FibN);
        long luaFib = (long)_luaScript.Call(_luaFibFn, FibN).Number;
        if (irFib != csFib || csFib != luaFib)
            throw new Exception($"Fib mismatch: ObjectIR={irFib}, C#={csFib}, Lua={luaFib}");
    }

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Description = "ObjectIR")]
    public int SumLoop_ObjectIR() => _cpu.CallMethod<int>("Program.SumLoop", SumN);

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long SumLoop_CSharp() => SumLoopCs(SumN);

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Description = "Lua (MoonSharp)")]
    public double SumLoop_Lua() => _luaScript.Call(_luaSumFn, SumN).Number;

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Description = "ObjectIR")]
    public int BranchLoop_ObjectIR() => _cpu.CallMethod<int>("Program.BranchLoop", BranchN);

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long BranchLoop_CSharp() => BranchLoopCs(BranchN);

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Description = "Lua (MoonSharp)")]
    public double BranchLoop_Lua() => _luaScript.Call(_luaBranchFn, BranchN).Number;

    [BenchmarkCategory("Fib")]
    [Benchmark(Description = "ObjectIR")]
    public int Fib_ObjectIR() => _cpu.CallMethod<int>("Program.Fib", FibN);

    [BenchmarkCategory("Fib")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long Fib_CSharp() => FibCs(FibN);

    [BenchmarkCategory("Fib")]
    [Benchmark(Description = "Lua (MoonSharp)")]
    public double Fib_Lua() => _luaScript.Call(_luaFibFn, FibN).Number;

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
}
