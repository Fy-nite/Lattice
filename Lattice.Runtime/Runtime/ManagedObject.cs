using System.Collections.Concurrent;
using ObjectIR.Core.AST;

namespace lattice.Core;

public class ManagedObject
{
    public string TypeName { get; set; }
    public ConcurrentDictionary<string, object> Fields { get; set; } = new();
    public Dictionary<string, MethodDTO> Methods { get; set; } = new();
    public Guid Id { get; } = Guid.NewGuid();

    public ManagedObject(string typeName)
    {
        TypeName = typeName;
    }

    public object? GetField(string name)
    {
        return Fields.TryGetValue(name, out var val) ? val : null;
    }

    public void SetField(string name, object? value)
    {
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
