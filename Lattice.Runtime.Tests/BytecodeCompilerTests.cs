using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class BytecodeCompilerTests
{
    private static MethodNode MakeMethod(string name, List<ParameterNode> parameters, TypeRef returnType, List<Statement> body)
    {
        return new MethodNode(name, parameters, returnType, true, null, new BlockStatement(body));
    }

    [Fact]
    public void Compile_SimpleConstant_ReturnsValue()
    {
        var method = MakeMethod("Five", new List<ParameterNode>(), TypeRef.Int32, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.LdcI4, "5")),
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);

        Assert.Equal("Five", cm.Name);
        Assert.Equal(0, cm.ArgCount);
        Assert.Equal(0, cm.LocalCount);
        Assert.True(cm.ReturnsValue);
        Assert.Equal(2, cm.Code.Length);
        Assert.Equal(OpCode.LdcI4, cm.Code[0].Opcode);
        Assert.Equal(5, cm.Code[0].Operand);
        Assert.Equal(OpCode.Ret, cm.Code[1].Opcode);
    }

    [Fact]
    public void Compile_WithArgs_ArgCountCorrect()
    {
        var method = MakeMethod("Add", new List<ParameterNode>
        {
            new ParameterNode("a", TypeRef.Int32),
            new ParameterNode("b", TypeRef.Int32),
        }, TypeRef.Int32, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.Ldarg, "a")),
            new InstructionStatement(new SimpleInstruction(OpCode.Ldarg, "b")),
            new InstructionStatement(new SimpleInstruction(OpCode.Add)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);

        Assert.Equal(2, cm.ArgCount);
        Assert.Equal(new[] { "a", "b" }, cm.ArgNames);
        Assert.Equal(4, cm.Code.Length);
    }

    [Fact]
    public void Compile_WithLocals_LocalCountCorrect()
    {
        var local1 = new LocalDeclarationStatement("x", TypeRef.Int32);
        var local2 = new LocalDeclarationStatement("y", TypeRef.Int32);
        var method = MakeMethod("Foo", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            local1,
            local2,
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcI4, "10")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Stloc, "x")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ret)),
        });
        method.Locals.Add(local1);
        method.Locals.Add(local2);

        var cm = BytecodeCompiler.Compile(method);

        Assert.Equal(2, cm.LocalCount);
        Assert.Contains("x", cm.LocalNames);
        Assert.Contains("y", cm.LocalNames);
    }

    [Fact]
    public void Compile_StringTable_Deduplicates()
    {
        var method = MakeMethod("X", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.Ldstr, "\"hello\"")),
            new InstructionStatement(new SimpleInstruction(OpCode.Pop)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ldstr, "\"hello\"")),
            new InstructionStatement(new SimpleInstruction(OpCode.Pop)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ldstr, "\"world\"")),
            new InstructionStatement(new SimpleInstruction(OpCode.Pop)),
        });

        var cm = BytecodeCompiler.Compile(method);

        Assert.Equal(2, cm.StringTable.Length);
        Assert.Equal("hello", cm.StringTable[0]);
        Assert.Equal("world", cm.StringTable[1]);
    }

    [Fact]
    public void Compile_FloatTable_Deduplicates()
    {
        var method = MakeMethod("X", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcR4, "3.5")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Pop)),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcR4, "3.5")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Pop)),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcR4, "2.5")),
            new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Pop)),
        });

        var cm = BytecodeCompiler.Compile(method);

        Assert.Equal(2, cm.FloatTable.Length);
        Assert.Equal(3.5f, cm.FloatTable[0]);
        Assert.Equal(2.5f, cm.FloatTable[1]);
    }

    [Fact]
    public void Compile_CallInstruction_CapturesCallTarget()
    {
        var target = new MethodReference("IO", "Println", TypeRef.Void, new List<TypeRef> { TypeRef.String });
        var method = MakeMethod("Main", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.Ldstr, "\"hi\"")),
            new InstructionStatement(new CallInstruction(target, new List<TypeRef> { TypeRef.String }, false)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);

        Assert.Single(cm.CallTargets);
        Assert.Equal("Println", cm.CallTargets[0]!.Target.Name);
        Assert.Equal(OpCode.Call, cm.Code[1].Opcode);
    }

    [Fact]
    public void Compile_VoidMethod_ReturnsValueIsFalse()
    {
        var method = MakeMethod("Do", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);
        Assert.False(cm.ReturnsValue);
    }

    [Fact]
    public void Compile_Opcodes_Preserved()
    {
        var method = MakeMethod("X", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.LdcI4, "42")),
            new InstructionStatement(new SimpleInstruction(OpCode.Dup)),
            new InstructionStatement(new SimpleInstruction(OpCode.Pop)),
            new InstructionStatement(new SimpleInstruction(OpCode.Not)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ceq)),
            new InstructionStatement(new SimpleInstruction(OpCode.Cne)),
            new InstructionStatement(new SimpleInstruction(OpCode.Cgt)),
            new InstructionStatement(new SimpleInstruction(OpCode.Clt)),
            new InstructionStatement(new SimpleInstruction(OpCode.Sub)),
            new InstructionStatement(new SimpleInstruction(OpCode.Mul)),
            new InstructionStatement(new SimpleInstruction(OpCode.Div)),
            new InstructionStatement(new SimpleInstruction(OpCode.Rem)),
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);
        Assert.Equal(13, cm.Code.Length);
        Assert.Equal(OpCode.LdcI4, cm.Code[0].Opcode);
        Assert.Equal(OpCode.Dup, cm.Code[1].Opcode);
        Assert.Equal(OpCode.Pop, cm.Code[2].Opcode);
        Assert.Equal(OpCode.Not, cm.Code[3].Opcode);
        Assert.Equal(OpCode.Ret, cm.Code[12].Opcode);
    }

    [Fact]
    public void Compile_SourceMethod_IsPreserved()
    {
        var method = MakeMethod("X", new List<ParameterNode>(), TypeRef.Void, new List<Statement>
        {
            new InstructionStatement(new SimpleInstruction(OpCode.Ret)),
        });

        var cm = BytecodeCompiler.Compile(method);
        Assert.Same(method, cm.SourceMethod);
    }
}
