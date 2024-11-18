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

public class CircularReferenceClass
{
    public CircularReferenceClass Reference { get; set; }
}

public class ComplexTestClass
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }
    public DateTime DateTimeProperty { get; set; }
    public List<int> ListProperty { get; set; }
    public TestClass NestedObject { get; set; }
}

[TestFixture]
public class ObjectExtensionsTests
{
    [Test]
    public void GetShallowCopy_NullObject_ReturnsNull()
    {
        TestClass original = null;
        var copy = original.GetShallowCopy();
        Assert.IsNull(copy);
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
        Assert.IsNull(copy.IgnoredProperty);
    }

    [Test]
    public void GetShallowCopy_ObjectWithReferenceType_CreatesShallowCopyOfReference()
    {
        var list = new List<int> { 1, 2, 3 };
        var original = new TestClassWithList { ListProperty = list };
        var copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreSame(original.ListProperty, copy.ListProperty);
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
    public void GetShallowCopy_ReadOnlyProperty_IsNotModified()
    {
        var original = new TestClassWithReadOnlyProperty { WritableProperty = 42 };
        var copy = original.GetShallowCopy();

        Assert.AreEqual(42, copy.ReadOnlyProperty); // This will be copied as it's not marked with CloneIgnore
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
    public void GetShallowCopy_CircularReference_DoesNotCauseStackOverflow()
    {
        var obj1 = new CircularReferenceClass();
        var obj2 = new CircularReferenceClass();
        obj1.Reference = obj2;
        obj2.Reference = obj1;

        var copy = obj1.GetShallowCopy();

        Assert.AreNotSame(obj1, copy);
        Assert.AreSame(obj2, copy.Reference);
        Assert.AreSame(obj1, copy.Reference.Reference);
    }

    [Test]
    public void GetShallowCopy_ComplexObject_CreatesCorrectShallowCopy()
    {
        var complexObject = new ComplexTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
            DateTimeProperty = DateTime.Now,
            ListProperty = new List<int> { 1, 2, 3 },
            NestedObject = new TestClass { IntProperty = 10, StringProperty = "Nested" },
        };

        var copy = complexObject.GetShallowCopy();

        Assert.AreNotSame(complexObject, copy);
        Assert.AreEqual(complexObject.IntProperty, copy.IntProperty);
        Assert.AreEqual(complexObject.StringProperty, copy.StringProperty);
        Assert.AreEqual(complexObject.DateTimeProperty, copy.DateTimeProperty);
        Assert.AreSame(complexObject.ListProperty, copy.ListProperty);
        Assert.AreSame(complexObject.NestedObject, copy.NestedObject);
    }
}
