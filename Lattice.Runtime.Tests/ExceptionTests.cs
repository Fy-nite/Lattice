using lattice.Runtime;
using lattice.Throwables;
using ObjectIR.Core.AST;

namespace Lattice.Runtime.Tests;

public class ExceptionTests
{
    [Fact]
    public void LatticeException_FormatsMessage()
    {
        var ex = new LatticeException("Something went wrong", "E001", "Try doing X", "line 5");
        var msg = ex.Message;
        Assert.Contains("error[E001]: Something went wrong", msg);
        Assert.Contains("--> line 5", msg);
        Assert.Contains("= help: Try doing X", msg);
    }

    [Fact]
    public void LatticeException_DefaultErrorCode()
    {
        var ex = new LatticeException("msg");
        Assert.Equal("L000", ex.ErrorCode);
    }

    [Fact]
    public void LatticeException_EmptyHelpText()
    {
        var ex = new LatticeException("msg", "E001", "", "");
        var msg = ex.Message;
        Assert.Contains("error[E001]: msg", msg);
        Assert.DoesNotContain("= help:", msg);
    }

    [Fact]
    public void LatticeException_Notes()
    {
        var ex = new LatticeException("msg", notes: new[] { "note1", "note2" });
        Assert.Contains("= note: note1", ex.Message);
        Assert.Contains("= note: note2", ex.Message);
    }

    [Fact]
    public void RuntimeException_InheritsLatticeException()
    {
        var ex = new RuntimeException("runtime error");
        Assert.IsAssignableFrom<LatticeException>(ex);
    }

    [Fact]
    public void RuntimeException_FormatsMessage()
    {
        var ex = new RuntimeException("bad op", "R001", "fix it", "at Foo");
        Assert.Contains("error[R001]: bad op", ex.Message);
        Assert.Contains("at Foo", ex.Message);
    }

    [Fact]
    public void OpCodeNotFoundException_FormatsCorrectly()
    {
        var ex = new OpCodeNotFoundException("FOOBAR", "at line 10");
        Assert.Contains("Unknown opcode: FOOBAR", ex.Message);
        Assert.Contains("FOOBAR", ex.HelpText);
    }

    [Fact]
    public void MethodResolutionException_FormatsCorrectly()
    {
        var ex = new MethodResolutionException("MyMethod", "at line 20");
        Assert.Contains("Could not resolve method: MyMethod", ex.Message);
        Assert.Contains("MyMethod", ex.HelpText);
    }

    [Fact]
    public void EntrypointNotFoundException_FormatsCorrectly()
    {
        var ex = new EntrypointNotFoundException("No entry", "Add a Main method");
        Assert.Contains("No entry", ex.Message);
        Assert.Contains("Add a Main method", ex.HelpText);
        Assert.Equal("E001", ex.ErrorCode);
    }

    [Fact]
    public void LatticeStackOverflowException_FormatsCorrectly()
    {
        var ex = new LatticeStackOverflowException("at Foo.Bar");
        Assert.Contains("Stack overflow", ex.Message);
        Assert.Contains("R005", ex.ErrorCode);
        Assert.Contains("infinite recursion", ex.HelpText);
    }

    [Fact]
    public void LatticeException_MessageWithoutLocation()
    {
        var ex = new LatticeException("Simple error", "E002");
        var msg = ex.Message;
        Assert.Contains("error[E002]: Simple error", msg);
        Assert.DoesNotContain("-->", msg);
    }
}
