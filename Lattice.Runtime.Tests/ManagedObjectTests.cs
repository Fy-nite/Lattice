using lattice.Core;
using ObjectIR.Core.AST;

namespace Lattice.Runtime.Tests;

public class ManagedObjectTests
{
    [Fact]
    public void Constructor_SetsTypeName()
    {
        var obj = new ManagedObject("MyClass");
        Assert.Equal("MyClass", obj.TypeName);
    }

    [Fact]
    public void Constructor_GeneratesUniqueGuid()
    {
        var a = new ManagedObject("X");
        var b = new ManagedObject("X");
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void SetField_ThenGetField_ReturnsValue()
    {
        var obj = new ManagedObject("T");
        obj.SetField("x", 42);
        Assert.Equal(42, obj.GetField("x"));
    }

    [Fact]
    public void GetField_MissingField_ReturnsNull()
    {
        var obj = new ManagedObject("T");
        Assert.Null(obj.GetField("nonexistent"));
    }

    [Fact]
    public void SetField_OverwritesExistingValue()
    {
        var obj = new ManagedObject("T");
        obj.SetField("x", 1);
        obj.SetField("x", 2);
        Assert.Equal(2, obj.GetField("x"));
    }

    [Fact]
    public void SetField_NullValue()
    {
        var obj = new ManagedObject("T");
        obj.SetField("x", null);
        Assert.Null(obj.GetField("x"));
    }

    [Fact]
    public void SetField_DifferentTypes()
    {
        var obj = new ManagedObject("T");
        obj.SetField("intField", 10);
        obj.SetField("strField", "hello");
        obj.SetField("boolField", true);
        obj.SetField("floatField", 3.14f);

        Assert.Equal(10, obj.GetField("intField"));
        Assert.Equal("hello", obj.GetField("strField"));
        Assert.Equal(true, obj.GetField("boolField"));
        Assert.Equal(3.14f, obj.GetField("floatField"));
    }

    [Fact]
    public void HasMethod_True()
    {
        var obj = new ManagedObject("T");
        obj.Methods["doSomething"] = new MethodDTO { Name = "doSomething" };
        Assert.True(obj.HasMethod("doSomething"));
    }

    [Fact]
    public void HasMethod_False()
    {
        var obj = new ManagedObject("T");
        Assert.False(obj.HasMethod("nope"));
    }

    [Fact]
    public void GetMethod_Existing()
    {
        var obj = new ManagedObject("T");
        var dto = new MethodDTO { Name = "Foo", ReturnType = TypeRef.Int32 };
        obj.Methods["Foo"] = dto;
        Assert.Same(dto, obj.GetMethod("Foo"));
    }

    [Fact]
    public void GetMethod_Missing_ReturnsNull()
    {
        var obj = new ManagedObject("T");
        Assert.Null(obj.GetMethod("Missing"));
    }

    [Fact]
    public void ToString_Format()
    {
        var obj = new ManagedObject("Worker");
        var str = obj.ToString();
        Assert.StartsWith("Worker#", str);
        Assert.Equal(obj.Id.ToString(), str.Split('#')[1]);
    }

    [Fact]
    public void Fields_ConcurrentAccess()
    {
        var obj = new ManagedObject("T");
        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => obj.SetField($"field_{i}", i)));
        Task.WaitAll(tasks.ToArray());

        for (int i = 0; i < 100; i++)
            Assert.Equal(i, obj.GetField($"field_{i}"));
    }

    [Fact]
    public void MethodDTO_DefaultValues()
    {
        var dto = new MethodDTO();
        Assert.Equal("", dto.Name);
        Assert.Empty(dto.Parameters);
        Assert.Equal(TypeRef.Void, dto.ReturnType);
    }

    [Fact]
    public void MethodDTO_CustomValues()
    {
        var param = new ParameterNode("x", TypeRef.Int32);
        var dto = new MethodDTO
        {
            Name = "DoStuff",
            Parameters = new List<ParameterNode> { param },
            ReturnType = TypeRef.Bool
        };
        Assert.Equal("DoStuff", dto.Name);
        Assert.Single(dto.Parameters);
        Assert.Equal(TypeRef.Bool, dto.ReturnType);
    }
}
