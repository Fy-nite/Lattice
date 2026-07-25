using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class CompilationCacheTests
{
    private static MethodNode MakeMethod(string name, TypeRef returnType, List<ParameterNode>? parameters = null)
    {
        return new MethodNode(
            name,
            parameters ?? new List<ParameterNode>(),
            returnType,
            isStatic: true,
            implements: null,
            body: new BlockStatement(new List<Statement>
            {
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcI4, "0")),
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ret))
            })
        );
    }

    [Fact]
    public void GetCompiled_EmptyCache_ReturnsNull()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Int32);
        Assert.Null(cache.GetCompiled(method));
    }

    [Fact]
    public void CompileAndStore_ReturnsCompiledMethod()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("Add", TypeRef.Int32, new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        });
        method.Body = new BlockStatement(new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ldarg, "a")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ldarg, "b")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Add)),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ret)),
        });

        var cm = cache.CompileAndStore(method);
        Assert.NotNull(cm);
        Assert.Equal("Add", cm.Name);
        Assert.Equal(2, cm.ArgCount);
        Assert.True(cm.ReturnsValue);
    }

    [Fact]
    public void CompileAndStore_CachesResult()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Int32);
        var cm1 = cache.CompileAndStore(method);
        var cm2 = cache.CompileAndStore(method);
        Assert.Same(cm1, cm2);
    }

    [Fact]
    public void CompileAndStore_DifferentMethods_GetDifferentResults()
    {
        var cache = new CompilationCache();
        var m1 = MakeMethod("A", TypeRef.Int32);
        var m2 = MakeMethod("B", TypeRef.Void);
        var cm1 = cache.CompileAndStore(m1);
        var cm2 = cache.CompileAndStore(m2);
        Assert.NotSame(cm1, cm2);
    }

    [Fact]
    public void GetJit_EmptyCache_ReturnsNull()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.Null(cache.GetJit(method));
    }

    [Fact]
    public void SetJit_ThenGetJit_ReturnsDelegate()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        JittedMethod del = (args, cpu, cm) => null;
        cache.SetJit(method, del);
        Assert.Same(del, cache.GetJit(method));
    }

    [Fact]
    public void TryAddJit_FirstTime_ReturnsTrue()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.True(cache.TryAddJit(method));
    }

    [Fact]
    public void TryAddJit_SecondTime_ReturnsFalse()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.True(cache.TryAddJit(method));
        Assert.False(cache.TryAddJit(method));
    }

    [Fact]
    public void RemoveJit_RemovesEntry_AllowsReAdd()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.True(cache.TryAddJit(method));
        cache.RemoveJit(method);
        Assert.True(cache.TryAddJit(method));
    }

    [Fact]
    public void RemoveJit_NonExistent_DoesNotThrow()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        var ex = Record.Exception(() => cache.RemoveJit(method));
        Assert.Null(ex);
    }

    [Fact]
    public void IncrementExecutionCount_FirstCall_Returns1()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.Equal(1, cache.IncrementExecutionCount(method));
    }

    [Fact]
    public void IncrementExecutionCount_MultipleCalls_Increments()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Assert.Equal(1, cache.IncrementExecutionCount(method));
        Assert.Equal(2, cache.IncrementExecutionCount(method));
        Assert.Equal(3, cache.IncrementExecutionCount(method));
    }

    [Fact]
    public void IncrementExecutionCount_DifferentMethods_Independent()
    {
        var cache = new CompilationCache();
        var m1 = MakeMethod("A", TypeRef.Void);
        var m2 = MakeMethod("B", TypeRef.Void);
        Assert.Equal(1, cache.IncrementExecutionCount(m1));
        Assert.Equal(1, cache.IncrementExecutionCount(m2));
        Assert.Equal(2, cache.IncrementExecutionCount(m1));
    }

    [Fact]
    public void ConcurrentIncrementExecutionCount_IsThreadSafe()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        Parallel.For(0, 1000, _ => cache.IncrementExecutionCount(method));
        int finalCount = cache.IncrementExecutionCount(method);
        Assert.Equal(1001, finalCount);
    }

    [Fact]
    public void ConcurrentTryAddJit_Idempotent()
    {
        var cache = new CompilationCache();
        var method = MakeMethod("X", TypeRef.Void);
        int successCount = 0;
        Parallel.For(0, 100, _ =>
        {
            if (cache.TryAddJit(method))
                Interlocked.Increment(ref successCount);
        });
        Assert.Equal(1, successCount);
    }
}
