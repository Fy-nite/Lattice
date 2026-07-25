using lattice.Runtime.Memory;
using Xunit;

namespace Lattice.Runtime.Tests;

public class MemoryArenaTests
{
    [Fact]
    public void Malloc_ReturnsSequentialOffsets()
    {
        using var arena = new MemoryArena(1024);
        int h1 = arena.Malloc(16);
        int h2 = arena.Malloc(32);
        Assert.Equal(0, h1);
        Assert.Equal(16, h2); // aligned to 4
    }

    [Fact]
    public void Malloc_ThrowsOnFull()
    {
        using var arena = new MemoryArena(16);
        arena.Malloc(16);
        Assert.Throws<OutOfMemoryException>(() => arena.Malloc(1));
    }

    [Fact]
    public void ReadWriteInt32()
    {
        using var arena = new MemoryArena(64);
        int h = arena.Malloc(4);
        arena.WriteInt32(h, 42);
        Assert.Equal(42, arena.ReadInt32(h));
    }

    [Fact]
    public void ReadWriteSingle()
    {
        using var arena = new MemoryArena(64);
        int h = arena.Malloc(4);
        arena.WriteSingle(h, 3.14f);
        Assert.Equal(3.14f, arena.ReadSingle(h));
    }

    [Fact]
    public void ReadWriteByte()
    {
        using var arena = new MemoryArena(64);
        int h = arena.Malloc(4);
        arena.WriteByte(h, 0xAB);
        Assert.Equal(0xAB, arena.ReadByte(h));
    }

    [Fact]
    public void ReadWriteBytes()
    {
        using var arena = new MemoryArena(256);
        int h = arena.Malloc(16);
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8];
        arena.WriteBytes(h, data);
        var result = arena.ReadBytes(h, 8);
        Assert.Equal(data, result.ToArray());
    }

    [Fact]
    public void Reset_ReclaimsAllMemory()
    {
        using var arena = new MemoryArena(1024);
        arena.Malloc(512);
        Assert.Equal(512, arena.UsedBytes);
        arena.Reset();
        Assert.Equal(0, arena.UsedBytes);
        Assert.Equal(1024, arena.FreeBytes);
    }

    [Fact]
    public void Dispose_PreventsUse()
    {
        var arena = new MemoryArena(64);
        arena.Dispose();
        Assert.Throws<ObjectDisposedException>(() => arena.Malloc(4));
    }

    [Fact]
    public void ReadWriteRoundtrip_MultipleValues()
    {
        using var arena = new MemoryArena(1024);
        int h1 = arena.Malloc(4);
        int h2 = arena.Malloc(4);
        int h3 = arena.Malloc(4);
        arena.WriteInt32(h1, -1);
        arena.WriteInt32(h2, int.MaxValue);
        arena.WriteInt32(h3, 0);
        Assert.Equal(-1, arena.ReadInt32(h1));
        Assert.Equal(int.MaxValue, arena.ReadInt32(h2));
        Assert.Equal(0, arena.ReadInt32(h3));
    }
}

public class FieldValueTests
{
    [Fact]
    public void IntRoundtrip()
    {
        var fv = FieldValue.FromInt(42);
        Assert.Equal(FieldValueKind.Int, fv.Kind);
        Assert.Equal(42, fv.AsInt);
    }

    [Fact]
    public void FloatRoundtrip()
    {
        var fv = FieldValue.FromFloat(2.71f);
        Assert.Equal(FieldValueKind.Float, fv.Kind);
        Assert.Equal(2.71f, fv.AsFloat, 5);
    }

    [Fact]
    public void BoolRoundtrip()
    {
        var t = FieldValue.FromBool(true);
        var f = FieldValue.FromBool(false);
        Assert.True(t.AsBool);
        Assert.False(f.AsBool);
    }

    [Fact]
    public void HandleRoundtrip()
    {
        var fv = FieldValue.FromHandle(new HeapHandle(1234));
        Assert.Equal(FieldValueKind.Handle, fv.Kind);
        Assert.Equal(1234, fv.AsHandle.Offset);
    }

    [Fact]
    public void Null_IsDefault()
    {
        Assert.True(FieldValue.Null.IsNull);
        Assert.Equal(FieldValueKind.None, FieldValue.Null.Kind);
    }

    [Fact]
    public void Equality()
    {
        Assert.Equal(FieldValue.FromInt(5), FieldValue.FromInt(5));
        Assert.NotEqual(FieldValue.FromInt(5), FieldValue.FromInt(6));
        Assert.Equal(FieldValue.Null, FieldValue.Null);
        Assert.NotEqual(FieldValue.Null, FieldValue.FromInt(0));
    }

    [Fact]
    public void FromObject_Boxes()
    {
        Assert.Equal(FieldValue.FromInt(7), FieldValue.FromObject(7));
        Assert.Equal(FieldValue.FromFloat(1.5f), FieldValue.FromObject(1.5f));
        Assert.Equal(FieldValue.FromBool(true), FieldValue.FromObject(true));
        Assert.True(FieldValue.FromObject(null).IsNull);
    }

    [Fact]
    public void ToObject_Roundtrip()
    {
        Assert.Equal(42, FieldValue.FromInt(42).ToObject());
        Assert.Equal(true, FieldValue.FromBool(true).ToObject());
        Assert.True(FieldValue.Null.ToObject() == null);
    }
}

public class HeapObjectTests
{
    [Fact]
    public void Allocate_ReadBack()
    {
        using var arena = new MemoryArena(1024);
        var obj = HeapObject.Allocate(arena, typeId: 1, fieldCount: 3);
        Assert.Equal(1, obj.TypeId);
        Assert.Equal(3, obj.FieldCount);
    }

    [Fact]
    public void SetGet_Field()
    {
        using var arena = new MemoryArena(1024);
        var obj = HeapObject.Allocate(arena, typeId: 1, fieldCount: 2);
        obj.SetField(0, FieldValue.FromInt(100));
        obj.SetField(1, FieldValue.FromFloat(3.14f));

        Assert.Equal(100, obj.GetField(0).AsInt);
        Assert.Equal(3.14f, obj.GetField(1).AsFloat, 5);
    }

    [Fact]
    public void Fields_Independent()
    {
        using var arena = new MemoryArena(1024);
        var obj = HeapObject.Allocate(arena, typeId: 1, fieldCount: 4);
        obj.SetField(0, FieldValue.FromInt(1));
        obj.SetField(1, FieldValue.FromInt(2));
        obj.SetField(2, FieldValue.FromInt(3));
        obj.SetField(3, FieldValue.FromInt(4));

        Assert.Equal(1, obj.GetField(0).AsInt);
        Assert.Equal(2, obj.GetField(1).AsInt);
        Assert.Equal(3, obj.GetField(2).AsInt);
        Assert.Equal(4, obj.GetField(3).AsInt);
    }

    [Fact]
    public void AllocationSize_Correct()
    {
        Assert.Equal(8 + 3 * 8, HeapObject.AllocationSize(3));
        Assert.Equal(8 + 0, HeapObject.AllocationSize(0));
    }
}

public class HeapStringTests
{
    [Fact]
    public void Allocate_ReadBack()
    {
        using var arena = new MemoryArena(1024);
        var str = HeapString.Allocate(arena, "hello");
        Assert.Equal("hello", str.Value);
        Assert.Equal(5, str.Length);
    }

    [Fact]
    public void Allocate_Empty()
    {
        using var arena = new MemoryArena(1024);
        var str = HeapString.Allocate(arena, "");
        Assert.Equal("", str.Value);
        Assert.Equal(0, str.Length);
    }

    [Fact]
    public void Allocate_Unicode()
    {
        using var arena = new MemoryArena(1024);
        var str = HeapString.Allocate(arena, "cafe\u0301");
        Assert.Equal("cafe\u0301", str.Value);
    }

    [Fact]
    public void AllocationSize_Correct()
    {
        Assert.True(HeapString.AllocationSize("abc") > HeapString.HeaderSize);
    }
}

public class HeapArrayTests
{
    [Fact]
    public void Allocate_ReadBack()
    {
        using var arena = new MemoryArena(1024);
        var arr = HeapArray.Allocate(arena, HeapArrayElementKind.Int, 5);
        Assert.Equal(0, arr.Count);
        Assert.Equal(5, arr.Capacity);
    }

    [Fact]
    public void SetGet_Element()
    {
        using var arena = new MemoryArena(1024);
        var arr = HeapArray.Allocate(arena, HeapArrayElementKind.Int, 4);
        arr.Count = 4;
        arr.SetElement(0, FieldValue.FromInt(10));
        arr.SetElement(1, FieldValue.FromInt(20));
        arr.SetElement(2, FieldValue.FromInt(30));
        arr.SetElement(3, FieldValue.FromInt(40));

        Assert.Equal(10, arr.GetElement(0).AsInt);
        Assert.Equal(20, arr.GetElement(1).AsInt);
        Assert.Equal(30, arr.GetElement(2).AsInt);
        Assert.Equal(40, arr.GetElement(3).AsInt);
    }

    [Fact]
    public void MixedTypes_InSameArray()
    {
        using var arena = new MemoryArena(1024);
        var arr = HeapArray.Allocate(arena, HeapArrayElementKind.Handle, 3);
        arr.Count = 3;
        arr.SetElement(0, FieldValue.FromInt(1));
        arr.SetElement(1, FieldValue.FromFloat(2.5f));
        arr.SetElement(2, FieldValue.FromBool(true));

        Assert.Equal(1, arr.GetElement(0).AsInt);
        Assert.Equal(2.5f, arr.GetElement(1).AsFloat, 4);
        Assert.True(arr.GetElement(2).AsBool);
    }
}

public class HeapAllocatorTests
{
    [Fact]
    public void NewObject_AllocatesAndTracksFields()
    {
        using var heap = new HeapAllocator(4096);
        var obj = heap.NewObject("Point", new Dictionary<string, object?>
        {
            ["x"] = 10,
            ["y"] = 20,
        });

        var map = heap.GetFieldMap("Point");
        Assert.Equal(2, map.Count);
        Assert.Equal(0, map["x"]);
        Assert.Equal(1, map["y"]);

        var raw = new HeapObject(heap.Arena, obj.Handle);
        Assert.Equal(10, raw.GetField(0).AsInt);
        Assert.Equal(20, raw.GetField(1).AsInt);
    }

    [Fact]
    public void NewString_OnHeap()
    {
        using var heap = new HeapAllocator(4096);
        var str = heap.NewString("hello world");
        Assert.Equal("hello world", str.Value);
    }

    [Fact]
    public void NewArray_OnHeap()
    {
        using var heap = new HeapAllocator(4096);
        var arr = heap.NewArray(HeapArrayElementKind.Int, 10);
        Assert.Equal(0, arr.Count);
        Assert.Equal(10, arr.Capacity);
    }

    [Fact]
    public void Reset_ReclaimsMemory()
    {
        using var heap = new HeapAllocator(1024);
        heap.NewObject("A");
        heap.NewString("test");
        Assert.True(heap.UsedBytes > 0);
        heap.Reset();
        Assert.Equal(0, heap.UsedBytes);
    }

    [Fact]
    public void TypeRegistry_SameTypeSharesId()
    {
        using var heap = new HeapAllocator(4096);
        int id1 = heap.GetOrCreateTypeId("Point");
        int id2 = heap.GetOrCreateTypeId("Point");
        int id3 = heap.GetOrCreateTypeId("Vector");
        Assert.Equal(id1, id2);
        Assert.NotEqual(id1, id3);
    }

    [Fact]
    public void FieldIndex_StableAcrossObjects()
    {
        using var heap = new HeapAllocator(4096);
        int fi1 = heap.GetFieldIndex("Point", "x");
        int fi2 = heap.GetFieldIndex("Point", "x");
        int fi3 = heap.GetFieldIndex("Point", "y");
        Assert.Equal(fi1, fi2);
        Assert.NotEqual(fi1, fi3);
    }
}

public class TypeRegistryTests
{
    [Fact]
    public void GetOrCreateTypeId_SameTypeReturnsSameId()
    {
        var reg = new TypeRegistry();
        int id1 = reg.GetOrCreateTypeId("int");
        int id2 = reg.GetOrCreateTypeId("int");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GetOrCreateTypeId_DifferentTypesDifferentIds()
    {
        var reg = new TypeRegistry();
        int id1 = reg.GetOrCreateTypeId("int");
        int id2 = reg.GetOrCreateTypeId("string");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GetTypeName_ReturnsCorrectName()
    {
        var reg = new TypeRegistry();
        int id = reg.GetOrCreateTypeId("float");
        Assert.Equal("float", reg.GetTypeName(id));
    }

    [Fact]
    public void HasType()
    {
        var reg = new TypeRegistry();
        Assert.False(reg.HasType("int"));
        reg.GetOrCreateTypeId("int");
        Assert.True(reg.HasType("int"));
    }

    [Fact]
    public void Count_Increments()
    {
        var reg = new TypeRegistry();
        Assert.Equal(0, reg.Count);
        reg.GetOrCreateTypeId("a");
        Assert.Equal(1, reg.Count);
        reg.GetOrCreateTypeId("b");
        Assert.Equal(2, reg.Count);
        reg.GetOrCreateTypeId("a");
        Assert.Equal(2, reg.Count);
    }
}
