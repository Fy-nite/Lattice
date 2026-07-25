using lattice.Core;
using ObjectIR.Core.AST;

namespace Lattice.Runtime.Tests;

public class CallStackTests
{
    private static MethodNode MakeMethod(string name, TypeRef returnType, List<ParameterNode>? parameters = null)
    {
        return new MethodNode(
            name,
            parameters ?? new List<ParameterNode>(),
            returnType,
            isStatic: true,
            implements: null,
            body: new BlockStatement(new List<Statement>())
        );
    }

    [Fact]
    public void Constructor_InitializesProperties()
    {
        var method = MakeMethod("Foo", TypeRef.Void);
        var frame = new CallStack(method);

        Assert.Same(method, frame.Method);
        Assert.Equal(0, frame.IP);
        Assert.Null(frame.Previous);
        Assert.Null(frame.This);
        Assert.Empty(frame.Locals);
        Assert.Empty(frame.Args);
        Assert.Empty(frame.EvaluationStack);
    }

    [Fact]
    public void Constructor_WithThis_SetsThisArg()
    {
        var method = MakeMethod("Foo", TypeRef.Void);
        var obj = new ManagedObject("T");
        var frame = new CallStack(method, obj);

        Assert.Same(obj, frame.This);
        Assert.Equal(obj, frame.Args["this"]);
    }

    [Fact]
    public void PushFrame_CreatesLinkedFrame()
    {
        var m1 = MakeMethod("Caller", TypeRef.Void);
        var m2 = MakeMethod("Callee", TypeRef.Int32);
        var f1 = new CallStack(m1);

        var f2 = f1.PushFrame(m2);

        Assert.Same(m2, f2.Method);
        Assert.Same(f1, f2.Previous);
    }

    [Fact]
    public void PushFrame_WithThis()
    {
        var method = MakeMethod("Foo", TypeRef.Void);
        var obj = new ManagedObject("T");
        var root = new CallStack(method);
        var child = root.PushFrame(method, obj);

        Assert.Same(obj, child.This);
        Assert.Equal(obj, child.Args["this"]);
    }

    [Fact]
    public void PopFrame_ReturnsPrevious()
    {
        var m1 = MakeMethod("A", TypeRef.Void);
        var m2 = MakeMethod("B", TypeRef.Void);
        var f1 = new CallStack(m1);
        var f2 = f1.PushFrame(m2);

        Assert.Same(f1, f2.PopFrame());
    }

    [Fact]
    public void PopFrame_RootReturnsNull()
    {
        var f = new CallStack(MakeMethod("X", TypeRef.Void));
        Assert.Null(f.PopFrame());
    }

    [Fact]
    public void EvaluationStack_Isolation()
    {
        var m = MakeMethod("X", TypeRef.Void);
        var f1 = new CallStack(m);
        var f2 = f1.PushFrame(m);

        f1.EvaluationStack.Push("from_f1");
        f2.EvaluationStack.Push("from_f2");

        Assert.Equal("from_f2", f2.EvaluationStack.Pop());
        Assert.Equal("from_f1", f1.EvaluationStack.Pop());
    }

    [Fact]
    public void Locals_AreIndependent()
    {
        var m = MakeMethod("X", TypeRef.Void);
        var f1 = new CallStack(m);
        var f2 = f1.PushFrame(m);

        f1.Locals["x"] = 10;
        f2.Locals["x"] = 20;

        Assert.Equal(10, f1.Locals["x"]);
        Assert.Equal(20, f2.Locals["x"]);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var method = MakeMethod("MyMethod", TypeRef.Void);
        var frame = new CallStack(method);
        frame.IP = 5;
        Assert.Equal("at MyMethod @ 5", frame.ToString());
    }

    [Fact]
    public void GetStackTrace_MultipleFrames()
    {
        var m1 = MakeMethod("A", TypeRef.Void);
        var m2 = MakeMethod("B", TypeRef.Void);
        var m3 = MakeMethod("C", TypeRef.Void);

        var f3 = new CallStack(m3);
        var f2 = f3.PushFrame(m2);
        var f1 = f2.PushFrame(m1);

        var trace = f1.GetStackTrace();
        Assert.Contains("at A @ 0", trace);
        Assert.Contains("at B @ 0", trace);
        Assert.Contains("at C @ 0", trace);
    }

    [Fact]
    public void GetStackTrace_SingleFrame()
    {
        var f = new CallStack(MakeMethod("Solo", TypeRef.Void));
        var trace = f.GetStackTrace();
        Assert.Contains("at Solo @ 0", trace);
    }

    [Fact]
    public void DeepChaining()
    {
        var frames = new List<CallStack>();
        CallStack? current = null;
        for (int i = 0; i < 10; i++)
        {
            var method = MakeMethod($"Level{i}", TypeRef.Void);
            current = current == null ? new CallStack(method) : current.PushFrame(method);
            frames.Add(current);
        }

        Assert.Equal(10, frames.Count);
        Assert.Same(frames[8], frames[9].Previous);

        var trace = frames[9].GetStackTrace();
        for (int i = 0; i < 10; i++)
            Assert.Contains($"at Level{i}", trace);
    }
}
