using lattice.Runtime.Compiler;

namespace Lattice.Runtime.Tests;

public class StackValueTests
{
    [Fact]
    public void FromInt_RoundTrips()
    {
        var sv = StackValue.FromInt(42);
        Assert.Equal(StackValueKind.Int, sv.Kind);
        Assert.Equal(42, sv.AsInt);
    }

    [Fact]
    public void FromInt_NegativeValue()
    {
        var sv = StackValue.FromInt(-7);
        Assert.Equal(-7, sv.AsInt);
    }

    [Fact]
    public void FromInt_Zero()
    {
        var sv = StackValue.FromInt(0);
        Assert.Equal(0, sv.AsInt);
    }

    [Fact]
    public void FromInt_MaxValue()
    {
        var sv = StackValue.FromInt(int.MaxValue);
        Assert.Equal(int.MaxValue, sv.AsInt);
    }

    [Fact]
    public void FromFloat_RoundTrips()
    {
        var sv = StackValue.FromFloat(3.14f);
        Assert.Equal(StackValueKind.Float, sv.Kind);
        Assert.Equal(3.14f, sv.AsFloat, 4);
    }

    [Fact]
    public void FromFloat_Negative()
    {
        var sv = StackValue.FromFloat(-2.5f);
        Assert.Equal(-2.5f, sv.AsFloat, 4);
    }

    [Fact]
    public void FromFloat_Zero()
    {
        var sv = StackValue.FromFloat(0.0f);
        Assert.Equal(0.0f, sv.AsFloat);
    }

    [Fact]
    public void FromBool_True()
    {
        var sv = StackValue.FromBool(true);
        Assert.Equal(StackValueKind.Bool, sv.Kind);
        Assert.True(sv.AsBool);
    }

    [Fact]
    public void FromBool_False()
    {
        var sv = StackValue.FromBool(false);
        Assert.False(sv.AsBool);
    }

    [Fact]
    public void FromObject_String()
    {
        var sv = StackValue.FromObject("hello");
        Assert.Equal(StackValueKind.Object, sv.Kind);
        Assert.Equal("hello", sv.AsObject);
    }

    [Fact]
    public void FromObject_Null()
    {
        var sv = StackValue.FromObject(null);
        Assert.Equal(StackValueKind.Object, sv.Kind);
        Assert.Null(sv.AsObject);
    }

    [Fact]
    public void IsTruthy_Int_NonZero_True()
    {
        Assert.True(StackValue.FromInt(1).IsTruthy);
        Assert.True(StackValue.FromInt(-5).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Int_Zero_False()
    {
        Assert.False(StackValue.FromInt(0).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Bool_True()
    {
        Assert.True(StackValue.FromBool(true).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Bool_False()
    {
        Assert.False(StackValue.FromBool(false).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Float_NonZero_True()
    {
        Assert.True(StackValue.FromFloat(1.0f).IsTruthy);
        Assert.True(StackValue.FromFloat(-0.5f).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Float_Zero_False()
    {
        Assert.False(StackValue.FromFloat(0.0f).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Object_NonNull_True()
    {
        Assert.True(StackValue.FromObject("x").IsTruthy);
        Assert.True(StackValue.FromObject(0).IsTruthy);
    }

    [Fact]
    public void IsTruthy_Object_Null_False()
    {
        Assert.False(StackValue.FromObject(null).IsTruthy);
    }

    [Fact]
    public void IsTruthy_None_False()
    {
        var sv = default(StackValue);
        Assert.False(sv.IsTruthy);
    }

    [Fact]
    public void ToObject_Int_Boxes()
    {
        var sv = StackValue.FromInt(42);
        Assert.Equal(42, sv.ToObject());
    }

    [Fact]
    public void ToObject_Float_Boxes()
    {
        var sv = StackValue.FromFloat(1.5f);
        Assert.Equal(1.5f, sv.ToObject());
    }

    [Fact]
    public void ToObject_Bool_Boxes()
    {
        var sv = StackValue.FromBool(true);
        Assert.Equal(true, sv.ToObject());
    }

    [Fact]
    public void ToObject_Object_PassesThrough()
    {
        var sv = StackValue.FromObject("test");
        Assert.Equal("test", sv.ToObject());
    }

    [Fact]
    public void ToObject_None_ReturnsNull()
    {
        var sv = default(StackValue);
        Assert.Null(sv.ToObject());
    }

    [Fact]
    public void ToString_Int_FormatsCorrectly()
    {
        Assert.Equal("42", StackValue.FromInt(42).ToString());
    }

    [Fact]
    public void ToString_Bool_FormatsCorrectly()
    {
        Assert.Equal("True", StackValue.FromBool(true).ToString());
        Assert.Equal("False", StackValue.FromBool(false).ToString());
    }

    [Fact]
    public void ToString_Object_ShowsValue()
    {
        Assert.Equal("hello", StackValue.FromObject("hello").ToString());
    }

    [Fact]
    public void ToString_Object_Null_ShowsNull()
    {
        Assert.Equal("null", StackValue.FromObject(null).ToString());
    }

    [Fact]
    public void ToString_None_ShowsNone()
    {
        Assert.Equal("none", default(StackValue).ToString());
    }

    [Fact]
    public void IsTruthyValue_DelegatesToIsTruthy()
    {
        Assert.True(StackValue.IsTruthyValue(StackValue.FromInt(1)));
        Assert.False(StackValue.IsTruthyValue(StackValue.FromInt(0)));
    }

    [Fact]
    public void Default_HasNoneKind()
    {
        var sv = default(StackValue);
        Assert.Equal(StackValueKind.None, sv.Kind);
    }
}
