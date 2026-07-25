namespace lattice.Runtime.Memory;

public sealed class TypeRegistry
{
    private readonly Dictionary<string, int> _nameToId = new();
    private readonly Dictionary<int, string> _idToName = new();
    private int _nextId = 1;

    public int GetOrCreateTypeId(string typeName)
    {
        if (_nameToId.TryGetValue(typeName, out var id)) return id;
        id = _nextId++;
        _nameToId[typeName] = id;
        _idToName[id] = typeName;
        return id;
    }

    public string? GetTypeName(int typeId)
    {
        _idToName.TryGetValue(typeId, out var name);
        return name;
    }

    public bool HasType(string typeName) => _nameToId.ContainsKey(typeName);
    public int Count => _nameToId.Count;
}
