using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime;
using lattice.Runtime.Compiler;
using AstOpCode = global::ObjectIR.Core.Ast.OpCode;

namespace Lattice.Runtime.Tests;

public class JitCompilerTests
{
    private static CompiledMethod BuildMethod(
        string name,
        List<ParameterNode> parameters,
        TypeRef returnType,
        List<CompactInstr> code,
        string[]? stringTable = null,
        float[]? floatTable = null,
        CallInstruction?[]? callTargets = null,
        NewObjInstruction?[]? newObjTargets = null,
        List<LocalDeclarationStatement>? locals = null)
    {
        var method = new MethodNode(
            name,
            parameters,
            returnType,
            isStatic: true,
            implements: null,
            body: new BlockStatement(new List<Statement>())
        );
        if (locals != null) method.Locals.AddRange(locals);

        return new CompiledMethod
        {
            Name = name,
            LocalCount = locals?.Count ?? 0,
            ArgCount = parameters.Count,
            ReturnsValue = !string.Equals(returnType.Name, "void", StringComparison.Ordinal),
            SourceMethod = method,
            Code = code.ToArray(),
            StringTable = stringTable ?? [],
            FloatTable = floatTable ?? [],
            LocalNames = locals?.Select(l => l.Name).ToArray() ?? [],
            ArgNames = parameters.Select(p => p.Name).ToArray(),
            LocalNameToIndex = locals?.Select((_, i) => i).ToArray() ?? [],
            CallTargets = callTargets ?? [],
            NewObjTargets = newObjTargets ?? [],
        };
    }

    private static object? JitAndRun(CompiledMethod cm, params object?[] args)
    {
        var jitted = JitCompiler.GetOrCompile(cm);
        Assert.NotNull(jitted);
        var cpu = new CPU();
        return jitted(args, cpu, cm);
    }

    [Fact]
    public void Jit_ConstantInt_ReturnsValue()
    {
        var cm = BuildMethod("GetFortyTwo", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.LdcI4, 42),
            new(AstOpCode.Ret),
        });
        Assert.Equal(42, JitAndRun(cm));
    }

    [Fact]
    public void Jit_AddTwoArgs()
    {
        var cm = BuildMethod("Add", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Add),
            new(AstOpCode.Ret),
        });
        Assert.Equal(7, JitAndRun(cm, 3, 4));
    }

    [Fact]
    public void Jit_SubtractTwoArgs()
    {
        var cm = BuildMethod("Sub", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Sub),
            new(AstOpCode.Ret),
        });
        Assert.Equal(2, JitAndRun(cm, 5, 3));
    }

    [Fact]
    public void Jit_MultiplyTwoArgs()
    {
        var cm = BuildMethod("Mul", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Mul),
            new(AstOpCode.Ret),
        });
        Assert.Equal(15, JitAndRun(cm, 3, 5));
    }

    [Fact]
    public void Jit_DivideTwoArgs()
    {
        var cm = BuildMethod("Div", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Div),
            new(AstOpCode.Ret),
        });
        Assert.Equal(3, JitAndRun(cm, 10, 3));
    }

    [Fact]
    public void Jit_RemainderTwoArgs()
    {
        var cm = BuildMethod("Rem", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Rem),
            new(AstOpCode.Ret),
        });
        Assert.Equal(1, JitAndRun(cm, 10, 3));
    }

    [Fact]
    public void Jit_Locals_StoreLoad()
    {
        var cm = BuildMethod("Swap", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Stloc, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Stloc, 1),
            new(AstOpCode.Ldloc, 1),
            new(AstOpCode.Ldloc, 0),
            new(AstOpCode.Add),
            new(AstOpCode.Ret),
        }, locals: new List<LocalDeclarationStatement>
        {
            new("x", TypeRef.Int32),
            new("y", TypeRef.Int32),
        });
        Assert.Equal(7, JitAndRun(cm, 3, 4));
    }

    [Fact]
    public void Jit_Dup_PushesDuplicate()
    {
        var cm = BuildMethod("DupAdd", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Dup),
            new(AstOpCode.Add),
            new(AstOpCode.Ret),
        });
        Assert.Equal(10, JitAndRun(cm, 5));
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(5, 3, false)]
    public void Jit_Ceq_IntOnly(int a, int b, bool expected)
    {
        var cm = BuildMethod("Ceq", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Bool, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Ceq),
            new(AstOpCode.Ret),
        });
        Assert.Equal(expected ? 1 : 0, JitAndRun(cm, a, b));
    }

    [Theory]
    [InlineData(5, 3, true)]
    [InlineData(3, 5, false)]
    public void Jit_Cgt_IntOnly(int a, int b, bool expected)
    {
        var cm = BuildMethod("Cgt", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Bool, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Cgt),
            new(AstOpCode.Ret),
        });
        Assert.Equal(expected ? 1 : 0, JitAndRun(cm, a, b));
    }

    [Theory]
    [InlineData(3, 5, true)]
    [InlineData(5, 3, false)]
    public void Jit_Clt_IntOnly(int a, int b, bool expected)
    {
        var cm = BuildMethod("Clt", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Bool, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Clt),
            new(AstOpCode.Ret),
        });
        Assert.Equal(expected ? 1 : 0, JitAndRun(cm, a, b));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(42, 0)]
    public void Jit_Not_IntOnly(int input, int expected)
    {
        var cm = BuildMethod("Not", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Not),
            new(AstOpCode.Ret),
        });
        Assert.Equal(expected, JitAndRun(cm, input));
    }

    [Fact]
    public void Jit_Brtrue_SkipsBlock()
    {
        var cm = BuildMethod("IfNeg", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.LdcI4, 0),
            new(AstOpCode.Clt),
            new(AstOpCode.Brtrue, 6),
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ret),
            new(AstOpCode.LdcI4, -1),
            new(AstOpCode.Ret),
        });
        Assert.Equal(5, JitAndRun(cm, 5));
        Assert.Equal(-1, JitAndRun(cm, -3));
    }

    [Fact]
    public void Jit_General_FloatArithmetic()
    {
        var cm = BuildMethod("FloatAdd", new List<ParameterNode>
        {
            new("a", TypeRef.Float32), new("b", TypeRef.Float32),
        }, TypeRef.Float32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new(AstOpCode.Add),
            new(AstOpCode.Ret),
        });
        var result = JitAndRun(cm, 3.5f, 2.5f);
        Assert.Equal(6.0f, (float)result!);
    }

    [Fact]
    public void Jit_General_Ldstr()
    {
        var cm = BuildMethod("GetStr", new List<ParameterNode>(), TypeRef.String, new List<CompactInstr>
        {
            new(AstOpCode.Ldstr, 0),
            new(AstOpCode.Ret),
        }, stringTable: ["hello"]);
        var result = JitAndRun(cm);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Jit_General_Ldnull()
    {
        var cm = BuildMethod("GetNull", new List<ParameterNode>(), TypeRef.Void, new List<CompactInstr>
        {
            new(AstOpCode.Ldnull),
            new(AstOpCode.Pop),
            new(AstOpCode.Ret),
        });
        var result = JitAndRun(cm);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(5, 5, (int)AstOpCode.Ceq, 1)]
    [InlineData(5, 3, (int)AstOpCode.Ceq, 0)]
    [InlineData(5, 3, (int)AstOpCode.Cgt, 1)]
    [InlineData(3, 5, (int)AstOpCode.Clt, 1)]
    public void Jit_General_Comparison(int a, int b, int opcode, int expected)
    {
        var cm = BuildMethod("Cmp", new List<ParameterNode>
        {
            new("a", TypeRef.Int32), new("b", TypeRef.Int32),
        }, TypeRef.Bool, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ldarg, 1),
            new((AstOpCode)opcode),
            new(AstOpCode.Ret),
        });
        Assert.Equal(expected, JitAndRun(cm, a, b));
    }

    [Fact]
    public void Jit_General_Neg()
    {
        var cm = BuildMethod("Neg", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Neg),
            new(AstOpCode.Ret),
        });
        Assert.Equal(-5, JitAndRun(cm, 5));
        Assert.Equal(5, JitAndRun(cm, -5));
    }

    [Fact]
    public void Jit_General_Not()
    {
        var cm = BuildMethod("Not", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Not),
            new(AstOpCode.Ret),
        });
        Assert.Equal(0, JitAndRun(cm, 1));
        Assert.Equal(1, JitAndRun(cm, 0));
    }

    [Fact]
    public void Jit_General_LdcR4()
    {
        var cm = BuildMethod("GetPi", new List<ParameterNode>(), TypeRef.Float32, new List<CompactInstr>
        {
            new(AstOpCode.LdcR4, 0),
            new(AstOpCode.Ret),
        }, floatTable: [3.14f]);
        var result = JitAndRun(cm);
        Assert.Equal(3.14f, (float)result!, 2);
    }

    [Fact]
    public void Jit_General_Dup()
    {
        var cm = BuildMethod("DupAdd", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Dup),
            new(AstOpCode.Add),
            new(AstOpCode.Ret),
        });
        Assert.Equal(10, JitAndRun(cm, 5));
    }

    [Fact]
    public void Jit_General_Branches()
    {
        var cm = BuildMethod("Abs", new List<ParameterNode>
        {
            new("a", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.LdcI4, 0),
            new(AstOpCode.Clt),
            new(AstOpCode.Brtrue, 6),
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Ret),
            new(AstOpCode.Ldarg, 0),
            new(AstOpCode.Neg),
            new(AstOpCode.Ret),
        });
        Assert.Equal(5, JitAndRun(cm, 5));
        Assert.Equal(5, JitAndRun(cm, -5));
    }

    [Fact]
    public void Jit_RecursiveFib_Correctness()
    {
        var fibRef = new MethodReference(
            new TypeRef("Program"), "fib", TypeRef.Int32,
            new List<TypeRef> { TypeRef.Int32 });

        var fibMethod = new MethodNode(
            "fib",
            new List<ParameterNode> { new("n", TypeRef.Int32) },
            TypeRef.Int32, isStatic: true, implements: null,
            body: new BlockStatement(new List<Statement>
            {
                new LocalDeclarationStatement("result", TypeRef.Int32),
            }));
        fibMethod.Locals.Add(new LocalDeclarationStatement("result", TypeRef.Int32));

        var cm = new CompiledMethod
        {
            Name = "fib",
            LocalCount = 1,
            ArgCount = 1,
            ReturnsValue = true,
            SourceMethod = fibMethod,
            Code =
            [
                new(AstOpCode.Ldarg, 0),    // 0: push n
                new(AstOpCode.LdcI4, 2),     // 1: push 2
                new(AstOpCode.Clt),           // 2: n < 2?
                new(AstOpCode.Brtrue, 16),    // 3: if n < 2, goto 16 (return n)
                new(AstOpCode.Ldarg, 0),     // 4: push n
                new(AstOpCode.LdcI4, 1),     // 5: push 1
                new(AstOpCode.Sub),           // 6: n - 1
                new(AstOpCode.Call, 0),       // 7: fib(n-1)
                new(AstOpCode.Stloc, 0),     // 8: local0 = fib(n-1)
                new(AstOpCode.Ldarg, 0),     // 9: push n
                new(AstOpCode.LdcI4, 2),     // 10: push 2
                new(AstOpCode.Sub),           // 11: n - 2
                new(AstOpCode.Call, 0),       // 12: fib(n-2)
                new(AstOpCode.Ldloc, 0),     // 13: push local0 (fib(n-1))
                new(AstOpCode.Add),           // 14: fib(n-2) + fib(n-1)
                new(AstOpCode.Ret),           // 15: return
                new(AstOpCode.Ldarg, 0),     // 16: push n (base case)
                new(AstOpCode.Ret),           // 17: return n
            ],
            StringTable = [],
            FloatTable = [],
            LocalNames = ["result"],
            ArgNames = ["n"],
            LocalNameToIndex = [0],
            CallTargets = [new CallInstruction(fibRef, new List<TypeRef> { TypeRef.Int32 }, false)],
            NewObjTargets = [],
        };

        var module = new ModuleNode("TestFib");
        var programClass = new ClassNode("Program");
        programClass.Methods.Add(fibMethod);
        module.Classes.Add(programClass);

        var cpu = new CPU();
        cpu.Cache = new CompilationCache();
        cpu.Cache.SetCompiled(fibMethod, cm);
        cpu.program = module;

        var jitDel = JitCompiler.GetOrCompile(cm);
        Assert.NotNull(jitDel);

        Assert.Equal(0, jitDel!(new object?[] { 0 }, cpu, cm));
        Assert.Equal(1, jitDel(new object?[] { 1 }, cpu, cm));
        Assert.Equal(1, jitDel(new object?[] { 2 }, cpu, cm));
        Assert.Equal(2, jitDel(new object?[] { 3 }, cpu, cm));
        Assert.Equal(3, jitDel(new object?[] { 4 }, cpu, cm));
        Assert.Equal(5, jitDel(new object?[] { 5 }, cpu, cm));
        Assert.Equal(8, jitDel(new object?[] { 6 }, cpu, cm));
        Assert.Equal(13, jitDel(new object?[] { 7 }, cpu, cm));
        Assert.Equal(55, jitDel(new object?[] { 10 }, cpu, cm));
    }

    [Fact]
    public void Jit_RecursiveFib_Performance()
    {
        var fibRef = new MethodReference(
            new TypeRef("Program"), "fib", TypeRef.Int32,
            new List<TypeRef> { TypeRef.Int32 });

        var fibMethod = new MethodNode(
            "fib",
            new List<ParameterNode> { new("n", TypeRef.Int32) },
            TypeRef.Int32, isStatic: true, implements: null,
            body: new BlockStatement(new List<Statement>
            {
                new LocalDeclarationStatement("result", TypeRef.Int32),
            }));
        fibMethod.Locals.Add(new LocalDeclarationStatement("result", TypeRef.Int32));

        var cm = new CompiledMethod
        {
            Name = "fib",
            LocalCount = 1,
            ArgCount = 1,
            ReturnsValue = true,
            SourceMethod = fibMethod,
            Code =
            [
                new(AstOpCode.Ldarg, 0),
                new(AstOpCode.LdcI4, 2),
                new(AstOpCode.Clt),
                new(AstOpCode.Brtrue, 16),
                new(AstOpCode.Ldarg, 0),
                new(AstOpCode.LdcI4, 1),
                new(AstOpCode.Sub),
                new(AstOpCode.Call, 0),
                new(AstOpCode.Stloc, 0),
                new(AstOpCode.Ldarg, 0),
                new(AstOpCode.LdcI4, 2),
                new(AstOpCode.Sub),
                new(AstOpCode.Call, 0),
                new(AstOpCode.Ldloc, 0),
                new(AstOpCode.Add),
                new(AstOpCode.Ret),
                new(AstOpCode.Ldarg, 0),
                new(AstOpCode.Ret),
            ],
            StringTable = [],
            FloatTable = [],
            LocalNames = ["result"],
            ArgNames = ["n"],
            LocalNameToIndex = [0],
            CallTargets = [new CallInstruction(fibRef, new List<TypeRef> { TypeRef.Int32 }, false)],
            NewObjTargets = [],
        };

        var module = new ModuleNode("TestFibPerf");
        var programClass = new ClassNode("Program");
        programClass.Methods.Add(fibMethod);
        module.Classes.Add(programClass);

        var cpu = new CPU();
        cpu.Cache = new CompilationCache();
        cpu.Cache.SetCompiled(fibMethod, cm);
        cpu.program = module;

        var jitDel = JitCompiler.GetOrCompile(cm)!;
        Assert.NotNull(jitDel);

        // Verify correctness first
        Assert.Equal(6765, jitDel(new object?[] { 20 }, cpu, cm));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 100;
        for (int i = 0; i < iterations; i++)
        {
            jitDel(new object?[] { 20 }, cpu, cm);
        }
        sw.Stop();
        Console.WriteLine($"Recursive fib(20) x{iterations} (JIT): {sw.ElapsedMilliseconds}ms");
    }
}
