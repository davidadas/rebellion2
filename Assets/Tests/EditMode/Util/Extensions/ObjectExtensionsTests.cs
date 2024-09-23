using NUnit.Framework;
using System.Collections.Generic;
using ObjectExtensions;

[TestFixture]
public class ObjectExtensionsTests
{
    private class TestClass
    {
        public int IntProperty { get; set; }
        public string StringProperty { get; set; }
        public List<int> IntList { get; set; }
        public Dictionary<string, string> StringDictionary { get; set; }

        [CloneIgnore]
        public string IgnoredField;

        public string NonIgnoredField;
    }

    [Test]
    public void TestCloneWithPropertiesAndFields()
    {
        var source = new TestClass
        {
            IntProperty = 42,
            StringProperty = "Hello",
            IntList = new List<int> { 1, 2, 3 },
            StringDictionary = new Dictionary<string, string> { { "key", "value" } },
            IgnoredField = "Ignore me",
            NonIgnoredField = "Don't ignore me"
        };

        var target = source.CloneWithoutAttribute();

        // Check that all properties and fields are copied
        Assert.AreEqual(source.IntProperty, target.IntProperty);
        Assert.AreEqual(source.StringProperty, target.StringProperty);
        CollectionAssert.AreEqual(source.IntList, target.IntList);
        CollectionAssert.AreEqual(source.StringDictionary, target.StringDictionary);
        Assert.AreEqual(source.NonIgnoredField, target.NonIgnoredField);
        Assert.IsNull(target.IgnoredField);
    }

    [Test]
    public void TestCloneWithNullCollectionT()
    {
        var source = new TestClass
        {
            IntList = null
        };

        var target = source.CloneWithoutAttribute();

        Assert.IsNull(target.IntList);
    }

    [Test]
    public void TestCloneWithEmptyCollection()
    {
        var source = new TestClass
        {
            IntList = new List<int>()
        };

        var target = source.CloneWithoutAttribute();

        // Check that the collection is not null
        Assert.IsNotNull(target.IntList);

        // Check that the collection is empty
        Assert.AreEqual(0, target.IntList.Count);
    }

    [Test]
    public void TestCloneIgnoreFields()
    {
        var source = new TestClass
        {
            IgnoredField = "Ignore me",
            NonIgnoredField = "Don't ignore me"
        };

        var target = source.CloneWithoutAttribute();

        // Check that only the non-ignored field is copied
        Assert.AreEqual(source.NonIgnoredField, target.NonIgnoredField);

        // Check that the ignored field is not copied
        Assert.IsNull(target.IgnoredField);
    }
}
