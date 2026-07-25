using ObjectIR.Core;
using ObjectIR.Core.Ast;
using ObjectIR.Core.AST;
using ObjectIR.StdLib.Core.Memory;
using lattice.Core;
using lattice.Runtime.Memory;
using lattice.Throwables;

namespace lattice.Runtime.Stdlib;

[NativeHook("Malloc")]
public class MallocHook : INativeHook
{
    public ClassNode GetClassNode()
    {
        var methods = new List<MethodNode>();

        methods.Add(new MethodNode("Alloc",
            new[] { new ParameterNode("typeName", "string") },
            new TypeRef("object"), true,
            new NativeMethod(args =>
            {
                var loader = ProgramLoader.Current;
                if (loader is not CPU cpu || cpu.Heap == null)
                    throw new RuntimeException("Malloc.Alloc requires an active CPU with a heap allocator", "");

                string typeName = args[0].Data?.ToString() ?? "object";
                var heapObj = cpu.Heap.NewObject(typeName);
                var managed = new ManagedObject(typeName)
                {
                    Heap = cpu.Heap,
                    HeapHandle = heapObj.Handle
                };
                return new Value<object>(managed);
            })));

        methods.Add(new MethodNode("GetUsedMemory",
            new List<ParameterNode>(),
            new TypeRef("int"), true,
            new NativeMethod(args =>
            {
                var loader = ProgramLoader.Current;
                if (loader is not CPU cpu || cpu.Heap == null)
                    return new Value<object>(0);
                return new Value<object>(cpu.Heap.UsedBytes);
            })));

        methods.Add(new MethodNode("GetFreeMemory",
            new List<ParameterNode>(),
            new TypeRef("int"), true,
            new NativeMethod(args =>
            {
                var loader = ProgramLoader.Current;
                if (loader is not CPU cpu || cpu.Heap == null)
                    return new Value<object>(0);
                return new Value<object>(cpu.Heap.FreeBytes);
            })));

        methods.Add(new MethodNode("Reset",
            new List<ParameterNode>(),
            TypeRef.Void, true,
            new NativeMethod(args =>
            {
                var loader = ProgramLoader.Current;
                if (loader is CPU cpu && cpu.Heap != null)
                    cpu.Heap.Reset();
                return new Value<object>(null);
            })));

        var node = new ClassNode("Malloc", new List<string>(), new List<FieldNode>(), new List<ConstructorNode>(), methods);
        node.IsStatic = true;
        return node;
    }
}
