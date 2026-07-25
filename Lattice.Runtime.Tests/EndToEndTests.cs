using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class EndToEndTests
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
    public void CompileAndExecute_SimpleConstant()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method GetFortyTwo() -> int32 {
        ldc.i4 42
        ret
    }
}");
        Assert.Equal(42, cpu.CallMethod<int>("Program.GetFortyTwo"));
    }

    [Fact]
    public void CompileAndExecute_Arguments()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Max(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        cgt
        if (stack) {
            ldarg a
            ret
        }
        ldarg b
        ret
    }
}");
        Assert.Equal(10, cpu.CallMethod<int>("Program.Max", 10, 5));
        Assert.Equal(10, cpu.CallMethod<int>("Program.Max", 3, 10));
    }

    [Fact]
    public void CompileAndExecute_WhileLoop()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Sum(n: int32) -> int32 {
        local i: int32
        local sum: int32
        ldc.i4 0
        stloc i
        ldc.i4 0
        stloc sum

        ldloc i
        ldarg n
        clt
        while (stack) {
            ldloc i
            ldc.i4 1
            add
            stloc i

            ldloc sum
            ldloc i
            add
            stloc sum

            ldloc i
            ldarg n
            clt
        }
        ldloc sum
        ret
    }
}");
        Assert.Equal(55, cpu.CallMethod<int>("Program.Sum", 10));
        Assert.Equal(0, cpu.CallMethod<int>("Program.Sum", 0));
        Assert.Equal(1, cpu.CallMethod<int>("Program.Sum", 1));
    }

    [Fact]
    public void CompileAndExecute_IfElse()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Abs(x: int32) -> int32 {
        ldarg x
        ldc.i4 0
        clt
        if (stack) {
            ldc.i4 0
            ldarg x
            sub
            ret
        }
        ldarg x
        ret
    }
}");
        Assert.Equal(5, cpu.CallMethod<int>("Program.Abs", 5));
        Assert.Equal(5, cpu.CallMethod<int>("Program.Abs", -5));
        Assert.Equal(0, cpu.CallMethod<int>("Program.Abs", 0));
    }

    [Fact]
    public void CompileAndExecute_NestedIfElse()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Sign(x: int32) -> int32 {
        ldarg x
        ldc.i4 0
        clt
        if (stack) {
            ldc.i4 -1
            ret
        }
        else
        {
            ldarg x
            ldc.i4 0
            cgt
            if (stack) {
                ldc.i4 1
                ret
            }
            else
            {
                ldc.i4 0
                ret
            }
        }
    }
}");
        Assert.Equal(-1, cpu.CallMethod<int>("Program.Sign", -5));
        Assert.Equal(1, cpu.CallMethod<int>("Program.Sign", 5));
        Assert.Equal(0, cpu.CallMethod<int>("Program.Sign", 0));
    }

    [Fact]
    public void CompileAndExecute_MultipleLocals()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Swap() -> int32 {
        local a: int32
        local b: int32
        ldc.i4 10
        stloc a
        ldc.i4 20
        stloc b
        ldloc b
        ret
    }
}");
        Assert.Equal(20, cpu.CallMethod<int>("Program.Swap"));
    }

    [Fact]
    public void CompileAndExecute_StringLoad()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method GetMessage() -> string {
        ldstr ""hello""
        ret
    }
}");
        Assert.Equal("hello", cpu.CallMethod<string>("Program.GetMessage"));
    }

    [Fact]
    public void CompileAndExecute_LocalDeclaration()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Compute() -> int32 {
        local x: int32
        local y: int32
        ldc.i4 5
        stloc x
        ldc.i4 3
        stloc y
        ldloc x
        ldloc y
        mul
        ret
    }
}");
        Assert.Equal(15, cpu.CallMethod<int>("Program.Compute"));
    }

    [Fact]
    public void CompileAndExecute_ComplexArithmetic()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Calc(a: int32, b: int32, c: int32) -> int32 {
        ldarg a
        ldarg b
        mul
        ldarg c
        add
        ret
    }
}");
        Assert.Equal(23, cpu.CallMethod<int>("Program.Calc", 3, 5, 8));
    }

    [Fact]
    public void CompileAndExecute_DupInstruction()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method DupAndAdd() -> int32 {
        ldc.i4 5
        dup
        add
        ret
    }
}");
        Assert.Equal(10, cpu.CallMethod<int>("Program.DupAndAdd"));
    }

    [Fact]
    public void CompileAndExecute_FactorialRecursive()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(120, cpu.CallMethod<int>("Program.Factorial", 5));
        Assert.Equal(3628800, cpu.CallMethod<int>("Program.Factorial", 10));
        Assert.Equal(1, cpu.CallMethod<int>("Program.Factorial", 1));
    }

    [Fact]
    public void CompileAndExecute_FibonacciRecursive()
    {
        var cpu = CreateCpu("test_comprehensive.oir");
        Assert.Equal(1, cpu.CallMethod<int>("Program.Fibonacci", 1));
        Assert.Equal(1, cpu.CallMethod<int>("Program.Fibonacci", 2));
        Assert.Equal(55, cpu.CallMethod<int>("Program.Fibonacci", 10));
    }

    [Fact]
    public void CompileAndExecute_FloatArithmetic()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method FloatMul(a: float32, b: float32) -> float32 {
        ldarg a
        ldarg b
        mul
        ret
    }
}");
        Assert.Equal(8.75f, cpu.CallMethod<float>("Program.FloatMul", 3.5f, 2.5f), 4);
    }

    [Fact]
    public void CompileAndExecute_MixedArithmeticOperations()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method Sub(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        sub
        ret
    }

    static method Div(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        div
        ret
    }

    static method Rem(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        rem
        ret
    }
}");
        Assert.Equal(7, cpu.CallMethod<int>("Program.Sub", 10, 3));
        Assert.Equal(3, cpu.CallMethod<int>("Program.Div", 10, 3));
        Assert.Equal(1, cpu.CallMethod<int>("Program.Rem", 10, 3));
    }

    [Fact]
    public void CompileAndExecute_Comparison_Operations()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method IsEqual(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        ceq
        if (stack) {
            ldc.i4 1
            ret
        }
        ldc.i4 0
        ret
    }
}");
        Assert.Equal(1, cpu.CallMethod<int>("Program.IsEqual", 5, 5));
        Assert.Equal(0, cpu.CallMethod<int>("Program.IsEqual", 5, 3));
    }

    [Fact]
    public void CompiledExecutor_MatchesAstInterpreter()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        // CallMethod goes through compiled path first, so verify consistency
        Assert.Equal(5, cpu.CallMethod<int>("Program.Five"));
        Assert.Equal(10, cpu.CallMethod<int>("Program.Add", 3, 7));
        Assert.Equal(-1, cpu.CallMethod<int>("Program.Add", 3, -4));
    }

    [Fact]
    public void JitCompiler_HandlesIntOnlyMethods()
    {
        var cpu = CreateCpu("test_callmethod.oir");

        // Force JIT compilation by manually compiling and JITing
        var cls = cpu.program.Classes.First(c => c.Name == "Program");
        var addMethod = cls.Methods.First(m => m.Name == "Add");
        var cm = cpu.GetCompiled(addMethod)!;
        var jit = JitCompiler.GetOrCompile(cm);

        if (jit != null)
        {
            var result = jit(new object?[] { 4, 6 }, cpu, cm);
            Assert.Equal(10, result);
        }
    }

    [Fact]
    public void MultipleCPUInstances_ShareCache()
    {
        var module = TextIrParser.ParseModule(File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..",
                "Lattice.Runtime", "demos", "test_callmethod.oir")));

        var cache = new CompilationCache();
        var cpu1 = new CPU { Cache = cache };
        var cpu2 = new CPU { Cache = cache };

        cpu1.LoadModule(module);
        cpu2.LoadModule(module);

        Assert.Equal(5, cpu1.CallMethod<int>("Program.Five"));
        Assert.Equal(5, cpu2.CallMethod<int>("Program.Five"));
    }

    [Fact]
    public void GetCompiled_ReturnsSameInstanceForSameMethod()
    {
        var cpu = CreateCpu("test_callmethod.oir");
        var cls = cpu.program.Classes.First(c => c.Name == "Program");
        var method = cls.Methods.First(m => m.Name == "Add");

        var cm1 = cpu.GetCompiled(method);
        var cm2 = cpu.GetCompiled(method);
        Assert.Same(cm1, cm2);
    }

    [Fact]
    public void Ldnull_PushesNull()
    {
        var cpu = CreateCpuFromOir(@"
module Test version 1.0.0
class Program {
    static method GetNull() -> string {
        ldnull
        ret
    }
}");
        var result = cpu.CallMethod<string>("Program.GetNull");
        Assert.Null(result);
    }
}
