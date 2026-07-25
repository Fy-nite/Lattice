using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class JitCompilerHelperTests
{
    // ── JitAdd ──

    [Theory]
    [InlineData(3, 4, 7)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    public void JitAdd_IntInt_ReturnsSum(int a, int b, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitAdd(a, b));
    }

    [Fact]
    public void JitAdd_FloatFloat_ReturnsSum()
    {
        var result = JitCompiler.JitAdd(1.5f, 2.5f);
        Assert.Equal(4.0f, result);
    }

    [Fact]
    public void JitAdd_MixedTypes_FloatDominates()
    {
        var result = JitCompiler.JitAdd(3, 1.5f);
        Assert.Equal(4.5f, result);
    }

    [Fact]
    public void JitAdd_MixedTypes_Reversed()
    {
        var result = JitCompiler.JitAdd(1.5f, 3);
        Assert.Equal(4.5f, result);
    }

    // ── JitSub ──

    [Theory]
    [InlineData(10, 3, 7)]
    [InlineData(0, 5, -5)]
    [InlineData(-3, -3, 0)]
    public void JitSub_IntInt_ReturnsDifference(int a, int b, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitSub(a, b));
    }

    [Fact]
    public void JitSub_FloatFloat_ReturnsDifference()
    {
        var result = JitCompiler.JitSub(5.0f, 2.0f);
        Assert.Equal(3.0f, result);
    }

    // ── JitMul ──

    [Theory]
    [InlineData(3, 4, 12)]
    [InlineData(7, 0, 0)]
    [InlineData(-2, 5, -10)]
    public void JitMul_IntInt_ReturnsProduct(int a, int b, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitMul(a, b));
    }

    [Fact]
    public void JitMul_FloatFloat_ReturnsProduct()
    {
        var result = JitCompiler.JitMul(2.5f, 4.0f);
        Assert.Equal(10.0f, result);
    }

    // ── JitDiv ──

    [Theory]
    [InlineData(10, 2, 5)]
    [InlineData(7, 2, 3)]
    [InlineData(0, 5, 0)]
    public void JitDiv_IntInt_ReturnsQuotient(int a, int b, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitDiv(a, b));
    }

    [Fact]
    public void JitDiv_FloatFloat_ReturnsQuotient()
    {
        var result = JitCompiler.JitDiv(10.0f, 4.0f);
        Assert.Equal(2.5f, result);
    }

    // ── JitRem ──

    [Theory]
    [InlineData(10, 3, 1)]
    [InlineData(7, 2, 1)]
    [InlineData(9, 3, 0)]
    public void JitRem_IntInt_ReturnsRemainder(int a, int b, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitRem(a, b));
    }

    [Fact]
    public void JitRem_FloatFloat_ReturnsRemainder()
    {
        var result = JitCompiler.JitRem(7.0f, 3.0f);
        Assert.Equal(1.0f, result);
    }

    // ── JitNeg ──

    [Theory]
    [InlineData(5, -5)]
    [InlineData(-3, 3)]
    [InlineData(0, 0)]
    public void JitNeg_Int_Negates(int input, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitNeg(input));
    }

    [Fact]
    public void JitNeg_Float_Negates()
    {
        Assert.Equal(-3.14f, JitCompiler.JitNeg(3.14f));
    }

    // ── JitCompare ──

    private const int OpCeq = 41;
    private const int OpCne = 42;
    private const int OpCgt = 44;
    private const int OpClt = 46;
    private const int OpCgtUn = 43;
    private const int OpCgeUn = 45;

    [Theory]
    [InlineData(5, 5, OpCeq, 1)]
    [InlineData(5, 3, OpCeq, 0)]
    [InlineData(5, 5, OpCne, 0)]
    [InlineData(5, 3, OpCne, 1)]
    [InlineData(5, 3, OpCgt, 1)]
    [InlineData(3, 5, OpCgt, 0)]
    [InlineData(3, 5, OpClt, 1)]
    [InlineData(5, 3, OpClt, 0)]
    public void JitCompare_IntInt(int a, int b, int opcode, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitCompare(a, b, opcode));
    }

    [Theory]
    [InlineData(5.0f, 5.0f, OpCeq, 1)]
    [InlineData(5.0f, 3.0f, OpCgt, 1)]
    [InlineData(3.0f, 5.0f, OpClt, 1)]
    public void JitCompare_FloatFloat(float a, float b, int opcode, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitCompare(a, b, opcode));
    }

    [Theory]
    [InlineData(null, null, OpCeq, 1)]
    [InlineData(null, 5, OpCne, 1)]
    [InlineData(5, null, OpCne, 1)]
    public void JitCompare_NullHandling(object? a, object? b, int opcode, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitCompare(a, b, opcode));
    }

    // ── JitIsTruthy ──

    [Theory]
    [InlineData(null, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-5, 1)]
    public void JitIsTruthy_Various(object? value, int expected)
    {
        Assert.Equal(expected, JitCompiler.JitIsTruthy(value));
    }

    [Fact]
    public void JitIsTruthy_Bool_True()
    {
        Assert.Equal(1, JitCompiler.JitIsTruthy(true));
    }

    [Fact]
    public void JitIsTruthy_Bool_False()
    {
        Assert.Equal(0, JitCompiler.JitIsTruthy(false));
    }

    [Fact]
    public void JitIsTruthy_Float_NonZero()
    {
        Assert.Equal(1, JitCompiler.JitIsTruthy(1.0f));
    }

    [Fact]
    public void JitIsTruthy_Float_Zero()
    {
        Assert.Equal(0, JitCompiler.JitIsTruthy(0.0f));
    }

    [Fact]
    public void JitIsTruthy_Object_NonNull()
    {
        Assert.Equal(1, JitCompiler.JitIsTruthy("hello"));
    }

    // ── JitNot ──

    [Fact]
    public void JitNot_Bool_True_ReturnsFalse()
    {
        Assert.Equal(false, JitCompiler.JitNot(true));
    }

    [Fact]
    public void JitNot_Bool_False_ReturnsTrue()
    {
        Assert.Equal(true, JitCompiler.JitNot(false));
    }

    [Fact]
    public void JitNot_Int_Zero_ReturnsOne()
    {
        Assert.Equal(1, JitCompiler.JitNot(0));
    }

    [Fact]
    public void JitNot_Int_NonZero_ReturnsZero()
    {
        Assert.Equal(0, JitCompiler.JitNot(42));
    }

    [Fact]
    public void JitNot_Float_Zero_ReturnsOne()
    {
        Assert.Equal(1, JitCompiler.JitNot(0.0f));
    }

    [Fact]
    public void JitNot_Null_ReturnsOne()
    {
        Assert.Equal(1, JitCompiler.JitNot(null));
    }

    // ── JitLdfld ──

    [Fact]
    public void JitLdfldManagedObject_ReturnsFieldValue()
    {
        var obj = new lattice.Core.ManagedObject("Worker");
        obj.SetField("health", 100);
        Assert.Equal(100, JitCompiler.JitLdfld(obj, "health"));
    }

    [Fact]
    public void JitLdfldManagedObject_DottedField()
    {
        var obj = new lattice.Core.ManagedObject("Worker");
        obj.SetField("name", "Alice");
        Assert.Equal("Alice", JitCompiler.JitLdfld(obj, "Worker.name"));
    }

    [Fact]
    public void JitLdfldManagedObject_MissingField_ReturnsNull()
    {
        var obj = new lattice.Core.ManagedObject("Worker");
        Assert.Null(JitCompiler.JitLdfld(obj, "nonexistent"));
    }

    [Fact]
    public void JitLdfld_NonManagedObject_ReturnsNull()
    {
        Assert.Null(JitCompiler.JitLdfld("not a managed object", "field"));
    }

    // ── JitStfld ──

    [Fact]
    public void JitStfldManagedObject_SetsFieldValue()
    {
        var obj = new lattice.Core.ManagedObject("Worker");
        JitCompiler.JitStfld(42, obj, "health");
        Assert.Equal(42, obj.GetField("health"));
    }

    [Fact]
    public void JitStfldManagedObject_DottedField()
    {
        var obj = new lattice.Core.ManagedObject("Worker");
        JitCompiler.JitStfld("Alice", obj, "Worker.name");
        Assert.Equal("Alice", obj.GetField("name"));
    }

    [Fact]
    public void JitStfld_NonManagedObject_DoesNotThrow()
    {
        var ex = Record.Exception(() => JitCompiler.JitStfld(42, "not an object", "field"));
        Assert.Null(ex);
    }

    // ── GetOrCompile ──

    [Fact]
    public void GetOrCompile_NativeMethod_ReturnsNull()
    {
        var method = new MethodNode("Native", new List<ParameterNode>(), TypeRef.Void, true, new NativeMethod(args => new Value<object>(null)));
        var cm = new CompiledMethod
        {
            Name = "Native",
            LocalCount = 0,
            ArgCount = 0,
            ReturnsValue = false,
            SourceMethod = method,
            Code = [],
        };
        Assert.Null(JitCompiler.GetOrCompile(cm));
    }

    [Fact]
    public void GetOrCompile_SimpleMethod_ReturnsDelegate()
    {
        var method = new MethodNode(
            "Five",
            new List<ParameterNode>(),
            TypeRef.Int32,
            true,
            null,
            new BlockStatement(new List<Statement>
            {
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcI4, "5")),
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ret)),
            })
        );
        var cm = BytecodeCompiler.Compile(method);
        var jit = JitCompiler.GetOrCompile(cm);
        Assert.NotNull(jit);
    }

    [Fact]
    public void GetOrCompile_SimpleMethod_ExecuteReturnsCorrectValue()
    {
        var method = new MethodNode(
            "Five",
            new List<ParameterNode>(),
            TypeRef.Int32,
            true,
            null,
            new BlockStatement(new List<Statement>
            {
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.LdcI4, "5")),
                new InstructionStatement(new SimpleInstruction(global::ObjectIR.Core.Ast.OpCode.Ret)),
            })
        );
        var cm = BytecodeCompiler.Compile(method);
        var jit = JitCompiler.GetOrCompile(cm);
        Assert.NotNull(jit);

        var cpu = new CPU();
        var result = jit(Array.Empty<object?>(), cpu, cm);
        Assert.Equal(5, result);
    }
}
