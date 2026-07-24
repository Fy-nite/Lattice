using Xunit;
using lattice.Runtime;
using ObjectIR.Core.AST;

namespace Lattice.Runtime.Tests;

public class CallMethodTests
{
    private static CPU CreateCpu()
    {
        var cpu = new CPU();
        var module = TextIrParser.ParseModule(File.ReadAllText(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..",
                "Lattice.Runtime", "demos", "test_callmethod.oir")));
        cpu.LoadModule(module);
        return cpu;
    }

    [Fact]
    public void CallMethod_NoArgs_ReturnsInt()
    {
        var cpu = CreateCpu();
        var result = cpu.CallMethod<int>("Program.Five");
        Assert.Equal(5, result);
    }

    [Fact]
    public void CallMethod_WithArgs_ReturnsSum()
    {
        var cpu = CreateCpu();
        var result = cpu.CallMethod<int>("Program.Add", 3, 7);
        Assert.Equal(10, result);
    }
}
