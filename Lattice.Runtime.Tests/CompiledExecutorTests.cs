using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class CompiledExecutorTests
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

    [Fact]
    public void Execute_ConstantInt_ReturnsValue()
    {
        var cm = BuildMethod("Five", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.LdcI4, 5),
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(5, result.AsInt);
    }

    [Fact]
    public void Execute_Add_TwoIntegers()
    {
        var cm = BuildMethod("Add", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Add),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromInt(3), StackValue.FromInt(7) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(10, result.AsInt);
    }

    [Fact]
    public void Execute_Sub_TwoIntegers()
    {
        var cm = BuildMethod("Sub", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Sub),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromInt(10), StackValue.FromInt(3) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(7, result.AsInt);
    }

    [Fact]
    public void Execute_Mul_TwoIntegers()
    {
        var cm = BuildMethod("Mul", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Mul),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromInt(4), StackValue.FromInt(5) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(20, result.AsInt);
    }

    [Fact]
    public void Execute_Div_TwoIntegers()
    {
        var cm = BuildMethod("Div", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Div),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromInt(10), StackValue.FromInt(3) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(3, result.AsInt);
    }

    [Fact]
    public void Execute_Rem_TwoIntegers()
    {
        var cm = BuildMethod("Rem", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Rem),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromInt(10), StackValue.FromInt(3) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(1, result.AsInt);
    }

    [Fact]
    public void Execute_FloatArithmetic()
    {
        var cm = BuildMethod("Add", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Float32),
            new ParameterNode("b", TypeRef.Float32),
        }, TypeRef.Float32, new List<CompactInstr>
        {
            new(OpCode.Ldarg, 0),
            new(OpCode.Ldarg, 1),
            new(OpCode.Add),
            new(OpCode.Ret),
        });

        var args = new[] { StackValue.FromFloat(1.5f), StackValue.FromFloat(2.5f) };
        var result = CompiledExecutor.Execute(cm, args, new CPU());
        Assert.Equal(4.0f, result.AsFloat, 4);
    }

    [Fact]
    public void Execute_LdcR4_PushesFloat()
    {
        var cm = BuildMethod("GetPi", new List<ParameterNode>(), TypeRef.Float32, new List<CompactInstr>
        {
            new(OpCode.LdcR4, 0),
            new(OpCode.Ret),
        }, floatTable: [3.14f]);

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(3.14f, result.AsFloat, 2);
    }

    [Fact]
    public void Execute_Dup_CopiesTopOfStack()
    {
        var cm = BuildMethod("Dup", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.LdcI4, 42),
            new(OpCode.Dup),
            new(OpCode.Pop),
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(42, result.AsInt);
    }

    [Fact]
    public void Execute_Pop_RemovesTop()
    {
        var cm = BuildMethod("Pop", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.LdcI4, 10),
            new(OpCode.LdcI4, 99),
            new(OpCode.Pop),
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(10, result.AsInt);
    }

    [Fact]
    public void Execute_Ldstr_LoadsString()
    {
        var cm = BuildMethod("Str", new List<ParameterNode>(), TypeRef.String, new List<CompactInstr>
        {
            new(OpCode.Ldstr, 0),
            new(OpCode.Ret),
        }, stringTable: new[] { "hello world" });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal("hello world", result.AsObject);
    }

    [Fact]
    public void Execute_LdcI4_Constants()
    {
        var cm = BuildMethod("Const", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.LdcI4, 0),
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(0, result.AsInt);
    }

    [Fact]
    public void Execute_LdcI4_NegativeConstant()
    {
        var cm = BuildMethod("Neg", new List<ParameterNode>(), TypeRef.Int32, new List<CompactInstr>
        {
            new(OpCode.LdcI4, -42),
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(-42, result.AsInt);
    }

    [Fact]
    public void Execute_Void_ReturnsDefault()
    {
        var cm = BuildMethod("Void", new List<ParameterNode>(), TypeRef.Void, new List<CompactInstr>
        {
            new(OpCode.Ret),
        });

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(StackValueKind.None, result.Kind);
    }

    [Fact]
    public void Execute_Ldloc_Stloc_RoundTrip()
    {
        var locals = new List<LocalDeclarationStatement> { new LocalDeclarationStatement("x", TypeRef.Int32) };
        var methodNode = new MethodNode("X", new List<ParameterNode>(), TypeRef.Int32, true, null,
            new BlockStatement(new List<Statement> { locals[0] }));

        var cm = new CompiledMethod
        {
            Name = "X",
            LocalCount = 1,
            ArgCount = 0,
            ReturnsValue = true,
            SourceMethod = methodNode,
            Code =
            [
                new(OpCode.LdcI4, 42),
                new(OpCode.Stloc, 0),
                new(OpCode.Ldloc, 0),
                new(OpCode.Ret),
            ],
            LocalNames = ["x"],
            LocalNameToIndex = [0],
        };

        var result = CompiledExecutor.Execute(cm, Array.Empty<StackValue>(), new CPU());
        Assert.Equal(42, result.AsInt);
    }
}
