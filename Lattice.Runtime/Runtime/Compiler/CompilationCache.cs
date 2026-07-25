using System.Collections.Concurrent;
using ObjectIR.Core.AST;

namespace lattice.Runtime.Compiler;

/// <summary>
/// Shared, thread-safe compilation state used by all CPU instances.
/// Holds bytecode-compiled methods, JIT-compiled delegates, and per-method execution counts.
/// </summary>
public class CompilationCache
{
    private readonly ConcurrentDictionary<MethodNode, CompiledMethod> _compiled = new();
    private readonly ConcurrentDictionary<MethodNode, JittedMethod?> _jitDelegates = new();
    private readonly ConcurrentDictionary<MethodNode, int> _executionCounts = new();

    // Cache synthesized constructor MethodNodes so the same instance is reused
    // across all CPU instances and resolution calls.
    private readonly ConcurrentDictionary<ConstructorNode, MethodNode> _ctorMethodNodes = new();

    public CompiledMethod? GetCompiled(MethodNode method)
    {
        _compiled.TryGetValue(method, out var cm);
        return cm;
    }

    public CompiledMethod CompileAndStore(MethodNode method)
    {
        var cm = BytecodeCompiler.Compile(method);
        _compiled.TryAdd(method, cm);
        return _compiled[method];
    }

    public void SetCompiled(MethodNode method, CompiledMethod cm)
    {
        _compiled[method] = cm;
    }

    public JittedMethod? GetJit(MethodNode method)
    {
        _jitDelegates.TryGetValue(method, out var jit);
        return jit;
    }

    public void SetJit(MethodNode method, JittedMethod? del)
    {
        _jitDelegates[method] = del;
    }

    public bool TryAddJit(MethodNode method)
    {
        return _jitDelegates.TryAdd(method, null);
    }

    public void RemoveJit(MethodNode method)
    {
        _jitDelegates.TryRemove(method, out _);
    }

    public int IncrementExecutionCount(MethodNode method)
    {
        return _executionCounts.AddOrUpdate(method, 1, (_, c) => c + 1);
    }

    /// <summary>
    /// Get or create a stable MethodNode for a ConstructorNode.
    /// Returns the same MethodNode instance every time, so it can be used
    /// as a cache key for compiled constructors.
    /// </summary>
    public MethodNode GetOrCreateCtorMethodNode(ConstructorNode ctor)
    {
        return _ctorMethodNodes.GetOrAdd(ctor, c =>
            new MethodNode("constructor", c.Parameters, TypeRef.Void, false, null, c.Body));
    }
}
