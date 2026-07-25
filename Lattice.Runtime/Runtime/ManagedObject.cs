using System.Collections.Concurrent;
using ObjectIR.Core.AST;
using lattice.Runtime.Memory;

namespace lattice.Core;

public class ManagedObject
{
    public string TypeName { get; set; }
    public ConcurrentDictionary<string, object> Fields { get; set; } = new();
    public Dictionary<string, MethodDTO> Methods { get; set; } = new();
    public Guid Id { get; } = Guid.NewGuid();

    public HeapAllocator? Heap { get; set; }
    public int HeapHandle { get; set; } = -1;

    public bool UsesHeap => Heap != null && HeapHandle >= 0;

    public ManagedObject(string typeName)
    {
        TypeName = typeName;
    }

    public object? GetField(string name)
    {
        if (UsesHeap)
        {
            var fieldMap = Heap!.GetFieldMap(TypeName);
            if (fieldMap.TryGetValue(name, out int fi))
            {
                var obj = new HeapObject(Heap.Arena, HeapHandle);
                return obj.GetField(fi).ToObject();
            }
            return null;
        }
        return Fields.TryGetValue(name, out var val) ? val : null;
    }

    public void SetField(string name, object? value)
    {
        if (UsesHeap)
        {
            int fi = Heap!.GetFieldIndex(TypeName, name);
            var obj = new HeapObject(Heap.Arena, HeapHandle);
            obj.SetField(fi, FieldValue.FromObject(value));
            return;
        }
        Fields[name] = value!;
    }

    public bool HasMethod(string name) => Methods.ContainsKey(name);

    public MethodDTO? GetMethod(string name) =>
        Methods.TryGetValue(name, out var m) ? m : null;

    public override string ToString() => $"{TypeName}#{Id}";
}

public class MethodDTO
{
    public string Name { get; set; } = "";
    public List<ParameterNode> Parameters { get; set; } = new();
    public TypeRef ReturnType { get; set; } = TypeRef.Void;
}
