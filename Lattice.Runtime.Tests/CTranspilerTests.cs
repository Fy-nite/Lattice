using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class CTranspilerTests
{
    private static CompiledMethod BuildSimpleMethod(string name, bool returnsValue, CompactInstr[] code)
    {
        var method = new ObjectIR.Core.AST.MethodNode(
            name,
            new List<ObjectIR.Core.AST.ParameterNode>(),
            returnsValue ? ObjectIR.Core.AST.TypeRef.Int32 : ObjectIR.Core.AST.TypeRef.Void,
            isStatic: true,
            implements: null,
            body: new ObjectIR.Core.AST.BlockStatement(new List<ObjectIR.Core.AST.Statement>())
        );

        return new CompiledMethod
        {
            Name = name,
            ArgCount = 0,
            LocalCount = 0,
            ReturnsValue = returnsValue,
            SourceMethod = method,
            Code = code,
            StringTable = [],
            FloatTable = [],
            ArgNames = [],
            LocalNames = [],
            LocalNameToIndex = [],
            CallTargets = [],
            NewObjTargets = [],
        };
    }

    [Fact]
    public void TranspileMethod_VoidNoOps_ContainsReturn()
    {
        var cm = BuildSimpleMethod("Empty", false, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });

        var c = CTranspiler.TranspileMethod(cm);
        Assert.Contains("static void", c);
        Assert.Contains("return;", c);
    }

    [Fact]
    public void TranspileMethod_WithReturn_ContainsExpression()
    {
        var cm = BuildSimpleMethod("Five", true, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.LdcI4, 5),
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });

        var c = CTranspiler.TranspileMethod(cm);
        Assert.Contains("static int", c);
        Assert.Contains("return 5;", c);
    }

    [Fact]
    public void TranspileAll_IncludesStdlibHeaders()
    {
        var cm = BuildSimpleMethod("X", false, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });

        var c = CTranspiler.TranspileAll(new[] { cm });
        Assert.Contains("#include <stdint.h>", c);
    }

    [Fact]
    public void TranspileAll_ProcessesMultipleMethods()
    {
        var cm1 = BuildSimpleMethod("A", false, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });
        var cm2 = BuildSimpleMethod("B", true, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.LdcI4, 1),
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });

        var c = CTranspiler.TranspileAll(new[] { cm1, cm2 });
        Assert.Contains("A", c);
        Assert.Contains("B", c);
    }

    [Fact]
    public void TranspileMethod_Arithmetic_Operators()
    {
        var cm = BuildSimpleMethod("Calc", true, new CompactInstr[]
        {
            new(ObjectIR.Core.Ast.OpCode.LdcI4, 10),
            new(ObjectIR.Core.Ast.OpCode.LdcI4, 5),
            new(ObjectIR.Core.Ast.OpCode.Add),
            new(ObjectIR.Core.Ast.OpCode.Ret),
        });

        var c = CTranspiler.TranspileMethod(cm);
        Assert.Contains("+", c);
    }

    [Fact]
    public void TranspileMethod_While_GeneratesGoto()
    {
        var method = new ObjectIR.Core.AST.MethodNode(
            "Loop",
            new List<ObjectIR.Core.AST.ParameterNode>(),
            ObjectIR.Core.AST.TypeRef.Void,
            isStatic: true,
            implements: null,
            body: new ObjectIR.Core.AST.BlockStatement(new List<ObjectIR.Core.AST.Statement>())
        );

        var cm = new CompiledMethod
        {
            Name = "Loop",
            ArgCount = 0,
            LocalCount = 0,
            ReturnsValue = false,
            SourceMethod = method,
            Code =
            [
                new(ObjectIR.Core.Ast.OpCode.LdcI4, 0),
                new(ObjectIR.Core.Ast.OpCode.Brfalse, 3),
                new(ObjectIR.Core.Ast.OpCode.Br, 0),
                new(ObjectIR.Core.Ast.OpCode.Ret),
            ],
        };

        var c = CTranspiler.TranspileMethod(cm);
        Assert.Contains("goto L0", c);
    }

    [Fact]
    public void TranspileMethod_MangledNames()
    {
        var method = new ObjectIR.Core.AST.MethodNode(
            "My.Method",
            new List<ObjectIR.Core.AST.ParameterNode>
            {
                new ObjectIR.Core.AST.ParameterNode("my.arg", ObjectIR.Core.AST.TypeRef.Int32),
            },
            ObjectIR.Core.AST.TypeRef.Int32,
            isStatic: true,
            implements: null,
            body: new ObjectIR.Core.AST.BlockStatement(new List<ObjectIR.Core.AST.Statement>())
        );

        var cm = new CompiledMethod
        {
            Name = "My.Method",
            ArgCount = 1,
            LocalCount = 0,
            ReturnsValue = true,
            SourceMethod = method,
            Code =
            [
                new(ObjectIR.Core.Ast.OpCode.Ldarg, 0),
                new(ObjectIR.Core.Ast.OpCode.Ret),
            ],
            ArgNames = ["my.arg"],
        };

        var c = CTranspiler.TranspileMethod(cm);
        Assert.Contains("__My_Method", c);
        Assert.Contains("__my_arg", c);
    }
}
