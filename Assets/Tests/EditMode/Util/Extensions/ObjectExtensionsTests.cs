using System;
using System.Collections.Generic;
using NUnit.Framework;
using ObjectExtensions;

public class TestClass
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }
}

public class TestClassWithIgnore
{
    public int IntProperty { get; set; }

    [CloneIgnore]
    public string IgnoredProperty { get; set; }
}

public class TestClassWithList
{
    public List<int> ListProperty { get; set; }
}

public class BaseTestClass
{
    public string BaseProperty { get; set; }
}

public class DerivedTestClass : BaseTestClass
{
    public string DerivedProperty { get; set; }
}

public class TestClassWithPrivateField
{
    private int _privateField;

    public void SetPrivateField(int value) => _privateField = value;

    public int GetPrivateField() => _privateField;
}

public class TestClassWithReadOnlyProperty
{
    public int ReadOnlyProperty { get; } = 42;
    public int WritableProperty { get; set; }
}

public class TestClassWithStaticField
{
    public static int StaticField;
    public int InstanceField;
}

public class ComplexTestClass
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }
    public List<int> ListProperty { get; set; }
    public TestClass NestedObject { get; set; }
}

public class ComplexInheritanceClass : DerivedTestClass
{
    public List<string> ListProperty { get; set; }
}

[TestFixture]
public class ObjectExtensionsTests
{
    [Test]
    public void Copy_NullObject_ReturnsNull()
    {
        TestClass original = null;

        var shallowCopy = original.GetShallowCopy();
        var deepCopy = original.GetDeepCopy();

        Assert.IsNull(shallowCopy);
        Assert.IsNull(deepCopy);
    }

    [Test]
    public void Copy_ValueTypeOrString_SkipsCopyForValueType()
    {
        int originalInt = 42;
        string originalString = "Hello";

        // Value types and strings should simply return the original value.
        Assert.AreEqual(originalInt, originalInt); // Value type directly compared.
        Assert.AreEqual(originalString, originalString); // Strings are immutable.
    }

    [Test]
    public void GetShallowCopy_SimpleObject_CreatesShallowCopy()
    {
        var original = new TestClass { IntProperty = 42, StringProperty = "Hello" };
        var copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
    }

    [Test]
    public void GetShallowCopy_ObjectWithCloneIgnore_IgnoresMarkedProperties()
    {
        var original = new TestClassWithIgnore { IntProperty = 42, IgnoredProperty = "Ignore me" };
        var copy = original.GetShallowCopy();

        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreNotEqual(original.IgnoredProperty, copy.IgnoredProperty);
    }

    [Test]
    public void GetDeepCopy_SimpleObject_CreatesDeepCopy()
    {
        var original = new TestClass { IntProperty = 42, StringProperty = "Hello" };
        var copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
    }

    [Test]
    public void GetDeepCopy_ComplexObject_CreatesDeepCopy()
    {
        var complexObject = new ComplexTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
            ListProperty = new List<int> { 1, 2, 3 },
            NestedObject = new TestClass { IntProperty = 10, StringProperty = "Nested" },
        };

        var copy = complexObject.GetDeepCopy();

        Assert.AreNotSame(complexObject, copy);
        Assert.AreEqual(complexObject.IntProperty, copy.IntProperty);
        Assert.AreEqual(complexObject.StringProperty, copy.StringProperty);
        Assert.AreNotSame(complexObject.ListProperty, copy.ListProperty);
        Assert.AreNotSame(complexObject.NestedObject, copy.NestedObject);
        Assert.AreEqual(complexObject.ListProperty, copy.ListProperty);
    }

    [Test]
    public void GetShallowCopy_CollectionObject_CreatesShallowCopy()
    {
        var list = new List<int> { 1, 2, 3 };
        var original = new TestClassWithList { ListProperty = list };
        var copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreSame(original.ListProperty, copy.ListProperty);
    }

    [Test]
    public void GetDeepCopy_CollectionObject_CreatesDeepCopy()
    {
        var list = new List<int> { 1, 2, 3 };
        var original = new TestClassWithList { ListProperty = list };
        var copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreNotSame(original.ListProperty, copy.ListProperty);
        CollectionAssert.AreEqual(original.ListProperty, copy.ListProperty);
    }

    [Test]
    public void GetShallowCopy_DerivedClass_CopiesBaseAndDerivedProperties()
    {
        var original = new DerivedTestClass { BaseProperty = "Base", DerivedProperty = "Derived" };
        var copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.IsInstanceOf<DerivedTestClass>(copy);
        Assert.AreEqual(original.BaseProperty, copy.BaseProperty);
        Assert.AreEqual(original.DerivedProperty, copy.DerivedProperty);
    }

    [Test]
    public void GetShallowCopy_PrivateFields_AreCopied()
    {
        var original = new TestClassWithPrivateField();
        original.SetPrivateField(42);
        var copy = original.GetShallowCopy();

        Assert.AreEqual(42, copy.GetPrivateField());
    }

    [Test]
    public void GetDeepCopy_ReadOnlyProperty_IsCopied()
    {
        var original = new TestClassWithReadOnlyProperty { WritableProperty = 42 };
        var copy = original.GetDeepCopy();

        Assert.AreEqual(42, copy.ReadOnlyProperty);
        Assert.AreEqual(42, copy.WritableProperty);
    }

    [Test]
    public void GetShallowCopy_StaticField_IsNotCopied()
    {
        TestClassWithStaticField.StaticField = 42;
        var original = new TestClassWithStaticField { InstanceField = 10 };
        var copy = original.GetShallowCopy();

        TestClassWithStaticField.StaticField = 100;

        Assert.AreEqual(10, copy.InstanceField);
        Assert.AreEqual(100, TestClassWithStaticField.StaticField);
    }

    [Test]
    public void GetDeepCopy_HandlesInheritanceAndCollectionsCorrectly()
    {
        var original = new ComplexInheritanceClass
        {
            BaseProperty = "Base",
            DerivedProperty = "Derived",
            ListProperty = new List<string> { "A", "B", "C" },
        };

        var copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.BaseProperty, copy.BaseProperty);
        Assert.AreEqual(original.DerivedProperty, copy.DerivedProperty);
        Assert.AreNotSame(original.ListProperty, copy.ListProperty);
        CollectionAssert.AreEqual(original.ListProperty, copy.ListProperty);
    }
}
