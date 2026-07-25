namespace lattice.Runtime.Memory;

public sealed class HeapAllocator : IDisposable
{
    private readonly MemoryArena _arena;
    private readonly TypeRegistry _types;

    private readonly Dictionary<string, Dictionary<string, int>> _typeFieldMaps = new();

    public MemoryArena Arena => _arena;
    public TypeRegistry Types => _types;

    public HeapAllocator(int arenaCapacity = 1024 * 1024)
    {
        _arena = new MemoryArena(arenaCapacity);
        _types = new TypeRegistry();
    }

    public int GetOrCreateTypeId(string typeName) => _types.GetOrCreateTypeId(typeName);

    public Dictionary<string, int> GetFieldMap(string typeName)
    {
        if (_typeFieldMaps.TryGetValue(typeName, out var map)) return map;
        map = new Dictionary<string, int>();
        _typeFieldMaps[typeName] = map;
        return map;
    }

    public int GetFieldIndex(string typeName, string fieldName)
    {
        var map = GetFieldMap(typeName);
        if (map.TryGetValue(fieldName, out var idx)) return idx;
        idx = map.Count;
        map[fieldName] = idx;
        return idx;
    }

    public HeapObject NewObject(string typeName, Dictionary<string, object?>? initialFields = null)
    {
        int typeId = GetOrCreateTypeId(typeName);
        var map = GetFieldMap(typeName);
        int fieldCount = map.Count;

        if (fieldCount == 0 && initialFields != null)
        {
            foreach (var kv in initialFields)
            {
                GetFieldIndex(typeName, kv.Key);
            }
            fieldCount = map.Count;
        }

        var obj = HeapObject.Allocate(_arena, typeId, fieldCount);

        if (initialFields != null)
        {
            foreach (var kv in initialFields)
            {
                if (kv.Value == null) continue;
                int fi = GetFieldIndex(typeName, kv.Key);
                obj.SetField(fi, FieldValue.FromObject(kv.Value));
            }
        }

        return obj;
    }

    public HeapString NewString(string value) => HeapString.Allocate(_arena, value);

    public HeapArray NewArray(HeapArrayElementKind elementKind, int capacity)
        => HeapArray.Allocate(_arena, elementKind, capacity);

    public void Reset()
    {
        _arena.Reset();
    }

    public int UsedBytes => _arena.UsedBytes;
    public int FreeBytes => _arena.FreeBytes;

    public void Dispose()
    {
        _arena.Dispose();
    }
}
