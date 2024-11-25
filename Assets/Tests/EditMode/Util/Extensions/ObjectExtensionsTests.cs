using System;
using System.Collections.Generic;
using NUnit.Framework;
using ObjectExtensions;

public class SimpleTestClass
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }
}

public class TestClassWithIgnore
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }

    [CloneIgnore]
    public int IgnoredIntProperty { get; set; }

    [CloneIgnore]
    public string IgnoredStringProperty { get; set; }

    [CloneIgnore]
    public List<int> IgnoredListProperty { get; set; }
}

public class ComplexTestClass
{
    public int IntProperty { get; set; }
    public string StringProperty { get; set; }
    public List<int> ListProperty { get; set; }
    public SimpleTestClass NestedObject { get; set; }
    public Dictionary<string, List<int>> DictionaryProperty { get; set; }
}

public class TestClassWithNestedCollections
{
    public List<List<int>> NestedList { get; set; }
    public Dictionary<string, Dictionary<int, string>> NestedDictionary { get; set; }
}

public class TestClassWithArrays
{
    public int[] IntArray { get; set; }
    public string[] StringArray { get; set; }
    public SimpleTestClass[] ObjectArray { get; set; }
}

public class TestClassWithReadOnlyProperty
{
    public int ReadOnlyProperty { get; } = 42;
    public int WritableProperty { get; set; }
}

public class TestClassWithPrivateSetters
{
    public int PublicGetPrivateSet { get; private set; }
    private string PrivateProperty { get; set; }

    public TestClassWithPrivateSetters() { }

    public TestClassWithPrivateSetters(int value, string text)
    {
        PublicGetPrivateSet = value;
        PrivateProperty = text;
    }

    public string GetPrivateProperty() => PrivateProperty;
}

public class BaseTestClass
{
    public string BaseProperty { get; set; }
}

public class DerivedTestClass : BaseTestClass
{
    public string DerivedProperty { get; set; }
}

public struct StructWithReferenceTypes
{
    public int IntValue { get; set; }
    public string StringValue { get; set; }
    public SimpleTestClass ObjectValue { get; set; }
}

public class ClassWithStruct
{
    public StructWithReferenceTypes StructProperty { get; set; }
}

public class TestClassWithoutDefaultConstructor
{
    public int Value { get; }

    public TestClassWithoutDefaultConstructor(int value)
    {
        Value = value;
    }
}

public class GenericTestClass<T1, T2>
{
    public T1 GenericProperty1 { get; set; }
    public T2 GenericProperty2 { get; set; }
    public List<Tuple<T1, T2>> GenericList { get; set; }

    public GenericTestClass() { }
}

[TestFixture]
public class ObjectExtensionsTests
{
    [Test]
    public void GetDeepCopy_NullObject_ReturnsNull()
    {
        SimpleTestClass original = null;
        SimpleTestClass copy = original.GetDeepCopy();
        Assert.IsNull(copy);
    }

    [Test]
    public void GetShallowCopy_NullObject_ReturnsNull()
    {
        SimpleTestClass original = null;
        SimpleTestClass copy = original.GetShallowCopy();
        Assert.IsNull(copy);
    }

    [Test]
    public void GetDeepCopy_SimpleObject_CreatesDeepCopy()
    {
        SimpleTestClass original = new SimpleTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
        };
        SimpleTestClass copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
    }

    [Test]
    public void GetShallowCopy_SimpleObject_CreatesShallowCopy()
    {
        SimpleTestClass original = new SimpleTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
        };
        SimpleTestClass copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
    }

    [Test]
    public void GetDeepCopy_ObjectWithCloneIgnore_IgnoresMarkedProperties()
    {
        TestClassWithIgnore original = new TestClassWithIgnore
        {
            IntProperty = 42,
            StringProperty = "Hello",
            IgnoredIntProperty = 100,
            IgnoredStringProperty = "Ignore me",
            IgnoredListProperty = new List<int> { 1, 2, 3 },
        };

        TestClassWithIgnore copy = original.GetDeepCopy();

        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
        Assert.AreEqual(0, copy.IgnoredIntProperty);
        Assert.IsNull(copy.IgnoredStringProperty);
        Assert.IsNull(copy.IgnoredListProperty);
    }

    [Test]
    public void GetShallowCopy_ObjectWithCloneIgnore_IgnoresMarkedProperties()
    {
        TestClassWithIgnore original = new TestClassWithIgnore
        {
            IntProperty = 42,
            StringProperty = "Hello",
            IgnoredIntProperty = 100,
            IgnoredStringProperty = "Ignore me",
            IgnoredListProperty = new List<int> { 1, 2, 3 },
        };

        TestClassWithIgnore copy = original.GetShallowCopy();

        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
        Assert.AreEqual(0, copy.IgnoredIntProperty);
        Assert.IsNull(copy.IgnoredStringProperty);
        Assert.IsNull(copy.IgnoredListProperty);
    }

    [Test]
    public void GetDeepCopy_ComplexObject_CreatesDeepCopy()
    {
        ComplexTestClass original = new ComplexTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
            ListProperty = new List<int> { 1, 2, 3 },
            NestedObject = new SimpleTestClass { IntProperty = 10, StringProperty = "Nested" },
            DictionaryProperty = new Dictionary<string, List<int>>
            {
                {
                    "Key",
                    new List<int> { 4, 5, 6 }
                },
            },
        };

        ComplexTestClass copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
        Assert.AreNotSame(original.ListProperty, copy.ListProperty);
        CollectionAssert.AreEqual(original.ListProperty, copy.ListProperty);
        Assert.AreNotSame(original.NestedObject, copy.NestedObject);
        Assert.AreEqual(original.NestedObject.IntProperty, copy.NestedObject.IntProperty);
        Assert.AreEqual(original.NestedObject.StringProperty, copy.NestedObject.StringProperty);
        Assert.AreNotSame(original.DictionaryProperty, copy.DictionaryProperty);
        CollectionAssert.AreEqual(
            original.DictionaryProperty["Key"],
            copy.DictionaryProperty["Key"]
        );
    }

    [Test]
    public void GetShallowCopy_ComplexObject_SharesReferences()
    {
        ComplexTestClass original = new ComplexTestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
            ListProperty = new List<int> { 1, 2, 3 },
            NestedObject = new SimpleTestClass { IntProperty = 10, StringProperty = "Nested" },
            DictionaryProperty = new Dictionary<string, List<int>>
            {
                {
                    "Key",
                    new List<int> { 4, 5, 6 }
                },
            },
        };

        ComplexTestClass copy = original.GetShallowCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.IntProperty, copy.IntProperty);
        Assert.AreEqual(original.StringProperty, copy.StringProperty);
        Assert.AreSame(original.ListProperty, copy.ListProperty);
        Assert.AreSame(original.NestedObject, copy.NestedObject);
        Assert.AreSame(original.DictionaryProperty, copy.DictionaryProperty);
    }

    [Test]
    public void GetDeepCopy_NestedCollections_AreDeeplyCopied()
    {
        TestClassWithNestedCollections original = new TestClassWithNestedCollections
        {
            NestedList = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 },
            },
            NestedDictionary = new Dictionary<string, Dictionary<int, string>>
            {
                {
                    "outer",
                    new Dictionary<int, string> { { 1, "one" }, { 2, "two" } }
                },
            },
        };

        TestClassWithNestedCollections copy = original.GetDeepCopy();

        Assert.AreNotSame(original.NestedList, copy.NestedList);
        Assert.AreNotSame(original.NestedList[0], copy.NestedList[0]);
        Assert.AreNotSame(original.NestedList[1], copy.NestedList[1]);
        CollectionAssert.AreEqual(original.NestedList, copy.NestedList);

        Assert.AreNotSame(original.NestedDictionary, copy.NestedDictionary);
        Assert.AreNotSame(original.NestedDictionary["outer"], copy.NestedDictionary["outer"]);
        CollectionAssert.AreEqual(
            original.NestedDictionary["outer"],
            copy.NestedDictionary["outer"]
        );
    }

    [Test]
    public void GetDeepCopy_ArrayProperties_AreDeeplyCopied()
    {
        TestClassWithArrays original = new TestClassWithArrays
        {
            IntArray = new int[] { 1, 2, 3 },
            StringArray = new string[] { "one", "two", "three" },
            ObjectArray = new SimpleTestClass[]
            {
                new SimpleTestClass { IntProperty = 1, StringProperty = "one" },
                new SimpleTestClass { IntProperty = 2, StringProperty = "two" },
            },
        };

        TestClassWithArrays copy = original.GetDeepCopy();

        Assert.AreNotSame(original.IntArray, copy.IntArray);
        Assert.AreNotSame(original.StringArray, copy.StringArray);
        Assert.AreNotSame(original.ObjectArray, copy.ObjectArray);

        CollectionAssert.AreEqual(original.IntArray, copy.IntArray);
        CollectionAssert.AreEqual(original.StringArray, copy.StringArray);

        for (int i = 0; i < original.ObjectArray.Length; i++)
        {
            Assert.AreNotSame(original.ObjectArray[i], copy.ObjectArray[i]);
            Assert.AreEqual(original.ObjectArray[i].IntProperty, copy.ObjectArray[i].IntProperty);
            Assert.AreEqual(
                original.ObjectArray[i].StringProperty,
                copy.ObjectArray[i].StringProperty
            );
        }
    }

    [Test]
    public void GetDeepCopy_ReadOnlyProperties_AreCopied()
    {
        TestClassWithReadOnlyProperty original = new TestClassWithReadOnlyProperty
        {
            WritableProperty = 42,
        };

        TestClassWithReadOnlyProperty copy = original.GetDeepCopy();

        Assert.AreEqual(original.ReadOnlyProperty, copy.ReadOnlyProperty);
        Assert.AreEqual(original.WritableProperty, copy.WritableProperty);
    }

    [Test]
    public void GetDeepCopy_PrivateSetters_AreCopied()
    {
        TestClassWithPrivateSetters original = new TestClassWithPrivateSetters(42, "Hello");

        TestClassWithPrivateSetters copy = original.GetDeepCopy();

        Assert.AreEqual(original.PublicGetPrivateSet, copy.PublicGetPrivateSet);
        Assert.AreEqual(original.GetPrivateProperty(), copy.GetPrivateProperty());
    }

    [Test]
    public void GetDeepCopy_DerivedClass_CopiesBaseAndDerivedProperties()
    {
        DerivedTestClass original = new DerivedTestClass
        {
            BaseProperty = "Base",
            DerivedProperty = "Derived",
        };

        DerivedTestClass copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.IsInstanceOf<DerivedTestClass>(copy);
        Assert.AreEqual(original.BaseProperty, copy.BaseProperty);
        Assert.AreEqual(original.DerivedProperty, copy.DerivedProperty);
    }

    [Test]
    public void GetDeepCopy_ClassWithStruct_IsDeeplyCopied()
    {
        ClassWithStruct original = new ClassWithStruct
        {
            StructProperty = new StructWithReferenceTypes
            {
                IntValue = 42,
                StringValue = "Hello",
                ObjectValue = new SimpleTestClass { IntProperty = 10, StringProperty = "World" },
            },
        };

        ClassWithStruct copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.StructProperty.IntValue, copy.StructProperty.IntValue);
        Assert.AreEqual(original.StructProperty.StringValue, copy.StructProperty.StringValue);
        Assert.AreNotSame(original.StructProperty.ObjectValue, copy.StructProperty.ObjectValue);
        Assert.AreEqual(
            original.StructProperty.ObjectValue.IntProperty,
            copy.StructProperty.ObjectValue.IntProperty
        );
        Assert.AreEqual(
            original.StructProperty.ObjectValue.StringProperty,
            copy.StructProperty.ObjectValue.StringProperty
        );
    }

    [Test]
    public void GetDeepCopy_ObjectWithoutDefaultConstructor_ThrowsException()
    {
        TestClassWithoutDefaultConstructor original = new TestClassWithoutDefaultConstructor(42);

        Assert.Throws<InvalidOperationException>(() => original.GetDeepCopy());
    }

    [Test]
    public void GetDeepCopy_GenericClass_IsDeeplyCopied()
    {
        GenericTestClass<int, string> original = new GenericTestClass<int, string>
        {
            GenericProperty1 = 42,
            GenericProperty2 = "Hello",
            GenericList = new List<Tuple<int, string>>
            {
                new Tuple<int, string>(1, "One"),
                new Tuple<int, string>(2, "Two"),
            },
        };

        GenericTestClass<int, string> copy = original.GetDeepCopy();

        Assert.AreNotSame(original, copy);
        Assert.AreEqual(original.GenericProperty1, copy.GenericProperty1);
        Assert.AreEqual(original.GenericProperty2, copy.GenericProperty2);
        Assert.AreNotSame(original.GenericList, copy.GenericList);
        CollectionAssert.AreEqual(original.GenericList, copy.GenericList);
    }
}
