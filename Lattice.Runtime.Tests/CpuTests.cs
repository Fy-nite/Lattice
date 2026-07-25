using lattice.Core;
using lattice.Runtime.Compiler;
using lattice.Throwables;
using ObjectIR.Core.AST;
using ObjectIR.StdLib.Core.Memory;

namespace Lattice.Runtime.Tests;

public class CpuTests
{
    private static CPU CreateCpu(string oirFileName)
    {
        var cpu = new CPU();
        var module = TextIrParser.ParseModule(File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..",
                "Lattice.Runtime", "demos", oirFileName)));
        cpu.LoadModule(module);
        return cpu;
    }

    private static CPU CreateCpuFromOir(string oirText)
    {
        var cpu = new CPU();
        var module = TextIrParser.ParseModule(oirText);
        cpu.LoadModule(module);
        return cpu;
    }

    [Fact]
    public void CallMethod_Five_ReturnsFive()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Equal(5, cpu.CallMethod<int>("Program.Five"));
    }

    [Fact]
    public void CallMethod_Add_ReturnsSum()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Equal(10, cpu.CallMethod<int>("Program.Add", 3, 7));
    }

    [Fact]
    public void CallMethod_Add_LargeNumbers()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Equal(2000000000, cpu.CallMethod<int>("Program.Add", 1000000000, 1000000000));
    }

    [Fact]
    public void CallMethod_Add_NegativeNumbers()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Equal(-2, cpu.CallMethod<int>("Program.Add", 3, -5));
    }

    [Fact]
    public void CallMethod_Add_Zero()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Equal(7, cpu.CallMethod<int>("Program.Add", 0, 7));
    }

    [Fact]
    public void CallMethod_Factorial_5()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(120, cpu.CallMethod<int>("Program.Factorial", 5));
    }

    [Fact]
    public void CallMethod_Factorial_1()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(1, cpu.CallMethod<int>("Program.Factorial", 1));
    }

    [Fact]
    public void CallMethod_Factorial_10()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(3628800, cpu.CallMethod<int>("Program.Factorial", 10));
    }

    [Fact]
    public void CallMethod_Fibonacci_1()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(1, cpu.CallMethod<int>("Program.Fibonacci", 1));
    }

    [Fact]
    public void CallMethod_Fibonacci_10()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(55, cpu.CallMethod<int>("Program.Fibonacci", 10));
    }

    [Fact]
    public void CallMethod_SumRange_10()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(55, cpu.CallMethod<int>("Program.SumRange", 10));
    }

    [Fact]
    public void CallMethod_SumRange_1()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(1, cpu.CallMethod<int>("Program.SumRange", 1));
    }

    [Fact]
    public void CallMethod_FloatAdd()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        var result = cpu.CallMethod<float>("Program.Add", 3.5f, 2.5f);
        Assert.Equal(6.0f, result, 2);
    }

    [Fact]
    public void CallMethod_ClassNotFound_Throws()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Throws<MethodResolutionException>(() => cpu.CallMethod<int>("Nonexistent.Method"));
    }

    [Fact]
    public void CallMethod_MethodNotFound_Throws()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Throws<MethodResolutionException>(() => cpu.CallMethod<int>("Program.Nonexistent"));
    }

    [Fact]
    public void CallMethod_InvalidFormat_Throws()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        Assert.Throws<ArgumentException>(() => cpu.CallMethod<int>("NoDot"));
    }

    [Fact]
    public void InitializeMain_WithoutMain_Throws()
    {
        var cpu = CreateCpuFromOir(@"
module NoMain version 1.0.0
class Foo {
    static method Bar() -> int32 {
        ldc.i4 1
        ret
    }
}");
        Assert.Throws<EntrypointNotFoundException>(() => cpu.InitializeMain(Array.Empty<string>()));
    }

    [Fact]
    public void LoadModule_CompilesAllMethods()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        var programClass = cpu.program.Classes.First(c => c.Name == "Program");
        foreach (var method in programClass.Methods)
        {
            Assert.NotNull(cpu.GetCompiled(method));
        }
    }

    [Fact]
    public void Cache_IsShared()
    {
        var cpu = new CPU();
        Assert.NotNull(cpu.Cache);
    }

    [Fact]
    public void DefaultDebug_IsFalse()
    {
        var cpu = new CPU();
        Assert.False(cpu.Debug);
    }

    [Fact]
    public void DefaultMaxStackDepth_Is1000()
    {
        var cpu = new CPU();
        Assert.Equal(1000, cpu.MaxStackDepth);
    }

    [Fact]
    public void MaxStackDepth_IsSettable()
    {
        var cpu = new CPU { MaxStackDepth = 500 };
        Assert.Equal(500, cpu.MaxStackDepth);
    }

    [Fact]
    public void Step_NoFrame_ReturnsFalse()
    {
        var cpu = new CPU();
        Assert.False(cpu.Step());
    }

    [Fact]
    public void Step_WithFrame_ExecutesInstruction()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        var cls = cpu.program.Classes.First(c => c.Name == "Program");
        var method = cls.Methods.First(m => m.Name == "Five");
        cpu.PushFrame(method);
        Assert.NotNull(cpu.CurrentFrame);

        while (cpu.Step()) { }
        Assert.False(cpu.Step());
    }

    [Fact]
    public void CompileAll_CompilesMethods()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        var cls = cpu.program.Classes.First(c => c.Name == "Program");
        foreach (var method in cls.Methods)
        {
            var compiled = cpu.GetCompiled(method);
            Assert.NotNull(compiled);
            Assert.NotEmpty(compiled.Code);
        }
    }

    [Fact]
    public void CallMethod_ExecutesThroughCompiledPath()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        // CallMethod should work via the compiled executor
        var result = cpu.CallMethod<int>("Program.Add", 2, 3);
        Assert.Equal(5, result);
    }

    [Fact]
    public void CallMethod_VoidMethod_DoesNotThrow()
    {
        var cpu = CreateCpuFromOir(@"
module VoidTest version 1.0.0
class Program {
    static method DoNothing() -> void {
        ret
    }
}");
        var ex = Record.Exception(() => cpu.CallMethod<object>("Program.DoNothing"));
        Assert.Null(ex);
    }

    [Fact]
    public void PushFrame_CreatesCallStack()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        var cls = cpu.program.Classes.First(c => c.Name == "Program");
        var method = cls.Methods.First(m => m.Name == "Five");
        cpu.PushFrame(method);
        Assert.NotNull(cpu.CurrentFrame);
        Assert.Equal("Five", cpu.CurrentFrame.Method.Name);
    }

    [Fact]
    public void BenchmarkOIR_FloatLoop()
    {
        var module = TextIrParser.ParseModule(BenchmarkModuleSource);
        var cpu = new CPU();
        cpu.LoadModule(module);
        float result = cpu.CallMethod<float>("Program.FloatLoop", 1000);
        Assert.Equal(1500f, result, 0.01f);
    }

    [Fact]
    public void BenchmarkOIR_StringLoop()
    {
        var module = TextIrParser.ParseModule(BenchmarkModuleSource);
        var cpu = new CPU();
        cpu.LoadModule(module);
        int result = cpu.CallMethod<int>("Program.StringLoop", 1000);
        Assert.Equal(1000, result);
    }

    [Fact]
    public void BenchmarkOIR_FieldLoop()
    {
        var module = TextIrParser.ParseModule(BenchmarkModuleSource);
        var cpu = new CPU();
        cpu.LoadModule(module);
        int result = cpu.CallMethod<int>("BenchObj.FieldLoop", 100);
        Assert.Equal(100 * 42, result);
    }

    private const string BenchmarkModuleSource = """
        module Bench

        class Program {
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
}
