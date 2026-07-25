using ObjectIR.Core.AST;
using ObjectIR.StdLib.Core.Memory;
using lattice.Runtime;
using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class NativeRegistryTests
{
    [Fact]
    public void RegisterFromAssembly_DiscoversHooks()
    {
        NativeRegistry.RegisterFromAssembly(typeof(ObjectIR.Stdlib.System.IO).Assembly);
        var module = new ModuleNode("Test");
        bool result = NativeRegistry.TryRegister("IO", module);
        Assert.True(result);
        Assert.Contains(module.Classes, c => c.Name == "IO");
    }

    [Fact]
    public void TryRegister_AlreadyRegistered_ReturnsTrue()
    {
        var module = new ModuleNode("Test");
        module.Classes.Add(new ClassNode("IO"));
        bool result = NativeRegistry.TryRegister("IO", module);
        Assert.True(result);
    }

    [Fact]
    public void TryRegister_UnknownClass_ReturnsFalse()
    {
        var module = new ModuleNode("Test");
        bool result = NativeRegistry.TryRegister("NonexistentHook", module);
        Assert.False(result);
    }

    [Fact]
    public void TryRegister_ThreadSafe()
    {
        var module = new ModuleNode("Test");
        var results = new bool[100];
        Parallel.For(0, 100, i =>
        {
            results[i] = NativeRegistry.TryRegister("IO", module);
        });
        // At least one should succeed, rest should return true (already registered)
        Assert.Contains(results, r => r);
        // Only one IO class should exist
        Assert.Single(module.Classes.Where(c => c.Name == "IO"));
    }

    [Fact]
    public void ProgramLoader_Activate_SetsCurrent()
    {
        var cpu = new CPU();
        var module = new ModuleNode("Test");
        cpu.LoadModule(module);

        using (ProgramLoader.Activate(cpu))
        {
            Assert.Same(cpu, ProgramLoader.Current);
        }
        Assert.Null(ProgramLoader.Current);
    }

    [Fact]
    public void ProgramLoader_Activate_RestoresPrevious()
    {
        var cpu1 = new CPU();
        var cpu2 = new CPU();
        var module = new ModuleNode("Test");
        cpu1.LoadModule(module);
        cpu2.LoadModule(module);

        using (ProgramLoader.Activate(cpu1))
        {
            Assert.Same(cpu1, ProgramLoader.Current);
            using (ProgramLoader.Activate(cpu2))
            {
                Assert.Same(cpu2, ProgramLoader.Current);
            }
            Assert.Same(cpu1, ProgramLoader.Current);
        }
        Assert.Null(ProgramLoader.Current);
    }
}
