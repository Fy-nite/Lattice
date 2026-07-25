using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using lattice;
using lattice.Core;
using lattice.Runtime;
using lattice.Runtime.Memory;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;

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
    private const int FloatN = 30_000;
    private const int StringN = 100_000;
    private const int FieldN = 10_000;
    private const int ArenaN = 30_000;
    private const int DeepCallN = 30_000;
    private const int NativeCallN = 30_000;
    private const int MixedOpsN = 30_000;

    private const string ModuleSource = """
        module Bench

        class Helper {
            static method Add(a: int32, b: int32) -> int32 {
                ldarg a
                ldarg b
                add
                ret
            }

            static method Identity(x: int32) -> int32 {
                ldarg x
                ret
            }

            static method Compute(a: int32, b: int32) -> int32 {
                ldarg a
                ldarg b
                mul
                ldarg a
                add
                ret
            }
        }

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

            static method FloatLoop(n: int32) -> float32 {
                local sum: float32
                local i: int32
                ldc.r4 0.0
                stloc sum
                ldc.i4 0
                stloc i
                ldloc i
                ldarg n
                clt
                while (stack) {
                    ldloc sum
                    ldc.r4 1.5
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

            static method StringLoop(n: int32) -> int32 {
                local count: int32
                local i: int32
                ldc.i4 0
                stloc count
                ldc.i4 0
                stloc i
                ldloc i
                ldarg n
                clt
                while (stack) {
                    ldstr "hello"
                    ldstr "hello"
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

            static method DeepCall(n: int32) -> int32 {
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
                    call Helper.Add(int32, int32) -> int32
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

            static method MixedOps(n: int32) -> int32 {
                local count: int32
                local i: int32
                local a: int32
                local b: int32
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
                    ldc.i4 3
                    mul
                    stloc a
                    ldloc a
                    ldc.i4 7
                    add
                    stloc b
                    ldloc b
                    ldc.i4 7
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
        }

        class BenchObj {
            public field X : int32

            constructor() {
                ret
            }

            static method FieldLoop(n: int32) -> int32 {
                local sum: int32
                local i: int32
                local obj: BenchObj
                ldc.i4 0
                stloc sum
                ldc.i4 0
                stloc i
                ldloc i
                ldarg n
                clt
                while (stack) {
                    newobj BenchObj.constructor()
                    stloc obj
                    ldloc obj
                    ldc.i4 42
                    stfld BenchObj.X
                    ldloc obj
                    ldfld BenchObj.X
                    ldloc sum
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
        }
        """;

    private CPU _cpu = null!;
    private CPU _cpuJit = null!;
    private MoonSharp.Interpreter.Script _luaScript = null!;
    private MoonSharp.Interpreter.DynValue _luaSumFn = null!;
    private MoonSharp.Interpreter.DynValue _luaBranchFn = null!;
    private MoonSharp.Interpreter.DynValue _luaFibFn = null!;
    private MoonSharp.Interpreter.DynValue _luaFloatFn = null!;
    private MoonSharp.Interpreter.DynValue _luaStringFn = null!;
    private MoonSharp.Interpreter.DynValue _luaFieldFn = null!;
    private MoonSharp.Interpreter.DynValue _luaDeepCallFn = null!;
    private MoonSharp.Interpreter.DynValue _luaMixedOpsFn = null!;
    private MemoryArena _arena = null!;
    private HeapAllocator _heap = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var module = TextIrParser.ParseModule(ModuleSource);

        _cpu = new CPU();
        _cpu.LoadModule(module);

        _cpuJit = new CPU(ExperimentalFeature.Jit);
        _cpuJit.LoadModule(TextIrParser.ParseModule(ModuleSource));
        _cpuJit.ForceJit();

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
            function floatloop(n)
                local s = 0.0
                local i = 0
                while i < n do s = s + 1.5 i = i + 1 end
                return s
            end
            function stringloop(n)
                local c = 0
                local i = 0
                while i < n do
                    if "hello" == "hello" then c = c + 1 end
                    i = i + 1
                end
                return c
            end
            function fieldloop(n)
                local s = 0
                local i = 0
                while i < n do
                    local obj = { X = 42 }
                    s = s + obj.X
                    i = i + 1
                end
                return s
            end
            function deepcall(n)
                local s = 0
                local i = 0
                while i < n do s = s + i i = i + 1 end
                return s
            end
            function mixedops(n)
                local c = 0
                local i = 0
                while i < n do
                    local a = i * 3 + 7
                    if a % 7 == 0 then c = c + 1 end
                    i = i + 1
                end
                return c
            end
            """);
        _luaSumFn = _luaScript.Globals.Get("sumloop")!;
        _luaBranchFn = _luaScript.Globals.Get("branchloop")!;
        _luaFibFn = _luaScript.Globals.Get("fib")!;
        _luaFloatFn = _luaScript.Globals.Get("floatloop")!;
        _luaStringFn = _luaScript.Globals.Get("stringloop")!;
        _luaFieldFn = _luaScript.Globals.Get("fieldloop")!;
        _luaDeepCallFn = _luaScript.Globals.Get("deepcall")!;
        _luaMixedOpsFn = _luaScript.Globals.Get("mixedops")!;

        _arena = new MemoryArena(1024 * 1024);
        _heap = new HeapAllocator(1024 * 1024);

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

        double irFloat = _cpu.CallMethod<float>("Program.FloatLoop", FloatN);
        double csFloat = FloatLoopCs(FloatN);
        double luaFloat = _luaScript.Call(_luaFloatFn, FloatN).Number;
        if (Math.Abs(irFloat - csFloat) > 0.01 || Math.Abs(csFloat - luaFloat) > 0.01)
            throw new Exception($"FloatLoop mismatch: ObjectIR={irFloat}, C#={csFloat}, Lua={luaFloat}");

        int irString = _cpu.CallMethod<int>("Program.StringLoop", StringN);
        long csString = StringLoopCs(StringN);
        long luaString = (long)_luaScript.Call(_luaStringFn, StringN).Number;
        if (irString != csString || csString != luaString)
            throw new Exception($"StringLoop mismatch: ObjectIR={irString}, C#={csString}, Lua={luaString}");

        int irField = _cpu.CallMethod<int>("BenchObj.FieldLoop", FieldN);
        long csField = FieldLoopCs(FieldN);
        long luaField = (long)_luaScript.Call(_luaFieldFn, FieldN).Number;
        if (irField != csField || csField != luaField)
            throw new Exception($"FieldLoop mismatch: ObjectIR={irField}, C#={csField}, Lua={luaField}");

        int irDeep = _cpu.CallMethod<int>("Program.DeepCall", DeepCallN);
        long csDeep = DeepCallCs(DeepCallN);
        long luaDeep = (long)_luaScript.Call(_luaDeepCallFn, DeepCallN).Number;
        if (irDeep != csDeep || csDeep != luaDeep)
            throw new Exception($"DeepCall mismatch: ObjectIR={irDeep}, C#={csDeep}, Lua={luaDeep}");

        int irMixed = _cpu.CallMethod<int>("Program.MixedOps", MixedOpsN);
        long csMixed = MixedOpsCs(MixedOpsN);
        long luaMixed = (long)_luaScript.Call(_luaMixedOpsFn, MixedOpsN).Number;
        if (irMixed != csMixed || csMixed != luaMixed)
            throw new Exception($"MixedOps mismatch: ObjectIR={irMixed}, C#={csMixed}, Lua={luaMixed}");

        ValidateHeap();
    }

    private void ValidateHeap()
    {
        _arena.Reset();
        var h1 = _arena.Malloc(4);
        _arena.WriteInt32(h1, 42);
        if (_arena.ReadInt32(h1) != 42)
            throw new Exception("Arena read/write mismatch");

        var h2 = _arena.Malloc(4);
        _arena.WriteInt32(h2, 99);
        if (_arena.ReadInt32(h2) != 99)
            throw new Exception("Arena sequential alloc mismatch");

        _heap.Reset();
        var fieldIdx = _heap.GetFieldIndex("TestObj", "X");
        var obj = _heap.NewObject("TestObj");
        obj.SetField(fieldIdx, FieldValue.FromInt(123));
        if (obj.GetField(fieldIdx).AsInt != 123)
            throw new Exception("HeapObject field mismatch");

        var str = _heap.NewString("benchmark");
        if (str.Value != "benchmark")
            throw new Exception($"HeapString mismatch: got '{str.Value}'");

        var arr = _heap.NewArray(HeapArrayElementKind.Int, 4);
        arr.SetElement(0, FieldValue.FromInt(10));
        arr.SetElement(1, FieldValue.FromInt(20));
        if (arr.GetElement(0).AsInt != 10 || arr.GetElement(1).AsInt != 20)
            throw new Exception("HeapArray element mismatch");
    }

    // ===== SumLoop =====

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long SumLoop_CSharp() => SumLoopCs(SumN);

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Description = "Interpreter")]
    public int SumLoop_Interpreter() => _cpu.CallMethod<int>("Program.SumLoop", SumN);

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Description = "JIT")]
    public int SumLoop_JIT() => _cpuJit.CallMethod<int>("Program.SumLoop", SumN);

    [BenchmarkCategory("SumLoop")]
    [Benchmark(Description = "Lua")]
    public double SumLoop_Lua() => _luaScript.Call(_luaSumFn, SumN).Number;

    // ===== BranchLoop =====

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long BranchLoop_CSharp() => BranchLoopCs(BranchN);

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Description = "Interpreter")]
    public int BranchLoop_Interpreter() => _cpu.CallMethod<int>("Program.BranchLoop", BranchN);

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Description = "JIT")]
    public int BranchLoop_JIT() => _cpuJit.CallMethod<int>("Program.BranchLoop", BranchN);

    [BenchmarkCategory("BranchLoop")]
    [Benchmark(Description = "Lua")]
    public double BranchLoop_Lua() => _luaScript.Call(_luaBranchFn, BranchN).Number;

    // ===== Fib =====

    [BenchmarkCategory("Fib")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long Fib_CSharp() => FibCs(FibN);

    [BenchmarkCategory("Fib")]
    [Benchmark(Description = "Interpreter")]
    public int Fib_Interpreter() => _cpu.CallMethod<int>("Program.Fib", FibN);

    [BenchmarkCategory("Fib")]
    [Benchmark(Description = "JIT")]
    public int Fib_JIT() => _cpuJit.CallMethod<int>("Program.Fib", FibN);

    [BenchmarkCategory("Fib")]
    [Benchmark(Description = "Lua")]
    public double Fib_Lua() => _luaScript.Call(_luaFibFn, FibN).Number;

    // ===== FloatLoop =====

    [BenchmarkCategory("FloatLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public double FloatLoop_CSharp() => FloatLoopCs(FloatN);

    [BenchmarkCategory("FloatLoop")]
    [Benchmark(Description = "Interpreter")]
    public float FloatLoop_Interpreter() => _cpu.CallMethod<float>("Program.FloatLoop", FloatN);

    [BenchmarkCategory("FloatLoop")]
    [Benchmark(Description = "JIT")]
    public float FloatLoop_JIT() => _cpuJit.CallMethod<float>("Program.FloatLoop", FloatN);

    [BenchmarkCategory("FloatLoop")]
    [Benchmark(Description = "Lua")]
    public double FloatLoop_Lua() => _luaScript.Call(_luaFloatFn, FloatN).Number;

    // ===== StringLoop =====

    [BenchmarkCategory("StringLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long StringLoop_CSharp() => StringLoopCs(StringN);

    [BenchmarkCategory("StringLoop")]
    [Benchmark(Description = "Interpreter")]
    public int StringLoop_Interpreter() => _cpu.CallMethod<int>("Program.StringLoop", StringN);

    [BenchmarkCategory("StringLoop")]
    [Benchmark(Description = "JIT")]
    public int StringLoop_JIT() => _cpuJit.CallMethod<int>("Program.StringLoop", StringN);

    [BenchmarkCategory("StringLoop")]
    [Benchmark(Description = "Lua")]
    public double StringLoop_Lua() => _luaScript.Call(_luaStringFn, StringN).Number;

    // ===== FieldLoop =====

    [BenchmarkCategory("FieldLoop")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long FieldLoop_CSharp() => FieldLoopCs(FieldN);

    [BenchmarkCategory("FieldLoop")]
    [Benchmark(Description = "Interpreter")]
    public int FieldLoop_Interpreter() => _cpu.CallMethod<int>("BenchObj.FieldLoop", FieldN);

    [BenchmarkCategory("FieldLoop")]
    [Benchmark(Description = "JIT")]
    public int FieldLoop_JIT() => _cpuJit.CallMethod<int>("BenchObj.FieldLoop", FieldN);

    [BenchmarkCategory("FieldLoop")]
    [Benchmark(Description = "Lua")]
    public double FieldLoop_Lua() => _luaScript.Call(_luaFieldFn, FieldN).Number;

    // ===== DeepCall (cross-class method dispatch) =====

    [BenchmarkCategory("DeepCall")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long DeepCall_CSharp() => DeepCallCs(DeepCallN);

    [BenchmarkCategory("DeepCall")]
    [Benchmark(Description = "Interpreter")]
    public int DeepCall_Interpreter() => _cpu.CallMethod<int>("Program.DeepCall", DeepCallN);

    [BenchmarkCategory("DeepCall")]
    [Benchmark(Description = "JIT")]
    public int DeepCall_JIT() => _cpuJit.CallMethod<int>("Program.DeepCall", DeepCallN);

    [BenchmarkCategory("DeepCall")]
    [Benchmark(Description = "Lua")]
    public double DeepCall_Lua() => _luaScript.Call(_luaDeepCallFn, DeepCallN).Number;

    // ===== MixedOps (combined arithmetic + compare + branch) =====

    [BenchmarkCategory("MixedOps")]
    [Benchmark(Baseline = true, Description = "C#")]
    public long MixedOps_CSharp() => MixedOpsCs(MixedOpsN);

    [BenchmarkCategory("MixedOps")]
    [Benchmark(Description = "Interpreter")]
    public int MixedOps_Interpreter() => _cpu.CallMethod<int>("Program.MixedOps", MixedOpsN);

    [BenchmarkCategory("MixedOps")]
    [Benchmark(Description = "JIT")]
    public int MixedOps_JIT() => _cpuJit.CallMethod<int>("Program.MixedOps", MixedOpsN);

    [BenchmarkCategory("MixedOps")]
    [Benchmark(Description = "Lua")]
    public double MixedOps_Lua() => _luaScript.Call(_luaMixedOpsFn, MixedOpsN).Number;

    // ===== Memory benchmarks =====

    [BenchmarkCategory("ArenaMalloc")]
    [Benchmark(Baseline = true, Description = "GC Alloc")]
    public int ArenaMalloc_GC()
    {
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var arr = new int[1];
            arr[0] = i;
            sum += arr[0];
        }
        return sum;
    }

    [BenchmarkCategory("ArenaMalloc")]
    [Benchmark(Description = "Arena Alloc")]
    public int ArenaMalloc_Arena()
    {
        _arena.Reset();
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var h = _arena.Malloc(4);
            _arena.WriteInt32(h, i);
            sum += _arena.ReadInt32(h);
        }
        return sum;
    }

    [BenchmarkCategory("HeapObject")]
    [Benchmark(Baseline = true, Description = "ManagedObject")]
    public int HeapObject_ManagedObject()
    {
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var obj = new ManagedObject("TestObj");
            obj.SetField("X", i);
            sum += (int)obj.GetField("X")!;
        }
        return sum;
    }

    [BenchmarkCategory("HeapObject")]
    [Benchmark(Description = "HeapObject")]
    public int HeapObject_HeapObject()
    {
        _heap.Reset();
        _heap.GetFieldIndex("TestObj", "X");
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var obj = _heap.NewObject("TestObj");
            obj.SetField(0, FieldValue.FromInt(i));
            sum += obj.GetField(0).AsInt;
        }
        return sum;
    }

    [BenchmarkCategory("HeapString")]
    [Benchmark(Description = "HeapString")]
    public int HeapString_Create()
    {
        _heap.Reset();
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var str = _heap.NewString($"hello_{i}");
            sum += str.Length;
        }
        return sum;
    }

    [BenchmarkCategory("HeapString")]
    [Benchmark(Description = ".NET string")]
    public int HeapString_DotNet()
    {
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            var s = $"hello_{i}";
            sum += s.Length;
        }
        return sum;
    }

    [BenchmarkCategory("HeapArray")]
    [Benchmark(Description = "HeapArray")]
    public int HeapArray_Access()
    {
        _heap.Reset();
        var arr = _heap.NewArray(HeapArrayElementKind.Int, ArenaN);
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            arr.SetElement(i, FieldValue.FromInt(i * 2));
            sum += arr.GetElement(i).AsInt;
        }
        return sum;
    }

    [BenchmarkCategory("HeapArray")]
    [Benchmark(Baseline = true, Description = "int[]")]
    public int HeapArray_DotNet()
    {
        var arr = new int[ArenaN];
        int sum = 0;
        for (int i = 0; i < ArenaN; i++)
        {
            arr[i] = i * 2;
            sum += arr[i];
        }
        return sum;
    }

    // ===== C# reference implementations =====

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

    private static double FloatLoopCs(int n)
    {
        double sum = 0;
        for (int i = 0; i < n; i++)
            sum += 1.5;
        return sum;
    }

    private static long StringLoopCs(int n)
    {
        long count = 0;
        for (int i = 0; i < n; i++)
        {
            if ("hello" == "hello") count++;
        }
        return count;
    }

    private static long FieldLoopCs(int n)
    {
        long sum = 0;
        for (int i = 0; i < n; i++)
        {
            var obj = new BenchObjCs { X = 42 };
            sum += obj.X;
        }
        return sum;
    }

    private static long DeepCallCs(int n)
    {
        long sum = 0;
        for (int i = 0; i < n; i++)
            sum = BenchHelperCs.Add(sum, i);
        return sum;
    }

    private static long MixedOpsCs(int n)
    {
        long count = 0;
        for (int i = 0; i < n; i++)
        {
            long a = (long)i * 3 + 7;
            if (a % 7 == 0) count++;
        }
        return count;
    }

    private class BenchObjCs
    {
        public int X;
    }

    private static class BenchHelperCs
    {
        public static long Add(long a, long b) => a + b;
    }
}
