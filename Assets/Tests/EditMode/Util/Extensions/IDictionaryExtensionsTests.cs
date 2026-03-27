using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Extensions;

public class ComplexObject
{
    public int Id { get; set; }
    public string Name { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is ComplexObject other)
        {
            return Id == other.Id && Name == other.Name;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name);
    }
}

[TestFixture]
public class IDictionaryExtensionsTests
{
    [Test]
    public void GetOrAddValue_KeyExists_ReturnsExistingValue()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int> { { "key1", 100 } };

        int result = dictionary.GetOrAddValue("key1", 200);

        Assert.AreEqual(100, result, "Should return existing value when key exists.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should still have 1 element.");
        Assert.AreEqual(100, dictionary["key1"], "Original value should remain unchanged.");
    }

    [Test]
    public void GetOrAddValue_KeyDoesNotExist_AddsAndReturnsNewValue()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();

        int result = dictionary.GetOrAddValue("key1", 100);

        Assert.AreEqual(100, result, "Should return the new value when key doesn't exist.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreEqual(100, dictionary["key1"], "New value should be added to dictionary.");
    }

    [Test]
    public void GetOrAddValue_NullKey_ThrowsArgumentNullException()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();

        Assert.Throws<ArgumentNullException>(() => dictionary.GetOrAddValue(null, 100));
    }

    [Test]
    public void GetOrAddValue_NullValue_AddsAndReturnsNull()
    {
        IDictionary<string, string> dictionary = new Dictionary<string, string>();

        string result = dictionary.GetOrAddValue("key1", null);

        Assert.IsNull(result, "Should return null when adding null value.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.IsNull(dictionary["key1"], "Null value should be added to dictionary.");
    }

    [Test]
    public void GetOrAddValue_NullDictionary_ThrowsNullReferenceException()
    {
        IDictionary<string, int> dictionary = null;

        Assert.Throws<NullReferenceException>(() => dictionary.GetOrAddValue("key1", 100));
    }

    [Test]
    public void GetOrAddValue_IntKey_WorksCorrectly()
    {
        IDictionary<int, string> dictionary = new Dictionary<int, string>();

        string result = dictionary.GetOrAddValue(42, "answer");

        Assert.AreEqual("answer", result, "Should return the new value.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreEqual("answer", dictionary[42], "Value should be added to dictionary.");
    }

    [Test]
    public void GetOrAddValue_ComplexObjectAsKey_WorksCorrectly()
    {
        ComplexObject key1 = new ComplexObject { Id = 1, Name = "Object1" };
        ComplexObject key2 = new ComplexObject { Id = 1, Name = "Object1" };
        IDictionary<ComplexObject, string> dictionary = new Dictionary<ComplexObject, string>
        {
            { key1, "value1" },
        };

        string result = dictionary.GetOrAddValue(key2, "value2");

        Assert.AreEqual("value1", result, "Should return existing value for equal keys.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should still have 1 element.");
    }

    [Test]
    public void GetOrAddValue_ComplexObjectAsValue_WorksCorrectly()
    {
        ComplexObject value = new ComplexObject { Id = 1, Name = "Object1" };
        IDictionary<string, ComplexObject> dictionary = new Dictionary<string, ComplexObject>();

        ComplexObject result = dictionary.GetOrAddValue("key1", value);

        Assert.AreEqual(value, result, "Should return the added complex object.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreSame(value, dictionary["key1"], "Same object reference should be stored.");
    }

    [Test]
    public void GetOrAddValue_DuplicateCallsWithSameKey_ReturnsSameValue()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();

        int result1 = dictionary.GetOrAddValue("key1", 100);
        int result2 = dictionary.GetOrAddValue("key1", 200);
        int result3 = dictionary.GetOrAddValue("key1", 300);

        Assert.AreEqual(100, result1, "First call should return the added value.");
        Assert.AreEqual(100, result2, "Second call should return the original value.");
        Assert.AreEqual(100, result3, "Third call should return the original value.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should still have 1 element.");
        Assert.AreEqual(100, dictionary["key1"], "Original value should remain unchanged.");
    }

    [Test]
    public void GetOrAddValue_MultipleKeys_EachAddedIndependently()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();

        int result1 = dictionary.GetOrAddValue("key1", 100);
        int result2 = dictionary.GetOrAddValue("key2", 200);
        int result3 = dictionary.GetOrAddValue("key3", 300);

        Assert.AreEqual(100, result1, "First key should return first value.");
        Assert.AreEqual(200, result2, "Second key should return second value.");
        Assert.AreEqual(300, result3, "Third key should return third value.");
        Assert.AreEqual(3, dictionary.Count, "Dictionary should have 3 elements.");
        Assert.AreEqual(100, dictionary["key1"], "First value should be correct.");
        Assert.AreEqual(200, dictionary["key2"], "Second value should be correct.");
        Assert.AreEqual(300, dictionary["key3"], "Third value should be correct.");
    }

    [Test]
    public void GetOrAddValue_ExistingKeyWithNullValue_ReturnsNull()
    {
        IDictionary<string, string> dictionary = new Dictionary<string, string>
        {
            { "key1", null },
        };

        string result = dictionary.GetOrAddValue("key1", "newValue");

        Assert.IsNull(result, "Should return existing null value.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should still have 1 element.");
        Assert.IsNull(dictionary["key1"], "Null value should remain in dictionary.");
    }

    [Test]
    public void GetOrAddValue_DefaultValueType_WorksCorrectly()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>();

        int result = dictionary.GetOrAddValue("key1", 0);

        Assert.AreEqual(0, result, "Should return the default value for int (0).");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreEqual(0, dictionary["key1"], "Zero should be added to dictionary.");
    }

    [Test]
    public void GetOrAddValue_EmptyString_WorksAsValidKey()
    {
        IDictionary<string, string> dictionary = new Dictionary<string, string>();

        string result = dictionary.GetOrAddValue("", "emptyKeyValue");

        Assert.AreEqual("emptyKeyValue", result, "Empty string should work as a valid key.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreEqual(
            "emptyKeyValue",
            dictionary[""],
            "Value should be retrievable with empty key."
        );
    }

    [Test]
    public void GetOrAddValue_AddToNonEmptyDictionary_WorksCorrectly()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>
        {
            { "existing1", 10 },
            { "existing2", 20 },
        };

        int result = dictionary.GetOrAddValue("newKey", 30);

        Assert.AreEqual(30, result, "Should return the new value.");
        Assert.AreEqual(3, dictionary.Count, "Dictionary should have 3 elements.");
        Assert.AreEqual(10, dictionary["existing1"], "Existing values should remain unchanged.");
        Assert.AreEqual(20, dictionary["existing2"], "Existing values should remain unchanged.");
        Assert.AreEqual(30, dictionary["newKey"], "New value should be added.");
    }

    [Test]
    public void GetOrAddValue_NullableValueType_WorksCorrectly()
    {
        IDictionary<string, int?> dictionary = new Dictionary<string, int?>();

        int? result1 = dictionary.GetOrAddValue("key1", 100);
        int? result2 = dictionary.GetOrAddValue("key2", null);

        Assert.AreEqual(100, result1, "Should return the non-null nullable value.");
        Assert.IsNull(result2, "Should return null for nullable value.");
        Assert.AreEqual(2, dictionary.Count, "Dictionary should have 2 elements.");
        Assert.AreEqual(100, dictionary["key1"], "Non-null value should be stored.");
        Assert.IsNull(dictionary["key2"], "Null value should be stored.");
    }

    [Test]
    public void GetOrAddValue_ListAsValue_WorksCorrectly()
    {
        List<int> list = new List<int> { 1, 2, 3 };
        IDictionary<string, List<int>> dictionary = new Dictionary<string, List<int>>();

        List<int> result = dictionary.GetOrAddValue("key1", list);

        Assert.AreSame(list, result, "Should return the same list reference.");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should have 1 element.");
        Assert.AreSame(list, dictionary["key1"], "Same list reference should be stored.");
        CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, dictionary["key1"]);
    }

    [Test]
    public void GetOrAddValue_CaseInsensitiveDictionary_RespectsComparerBehavior()
    {
        IDictionary<string, int> dictionary = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "KEY1", 100 },
        };

        int result1 = dictionary.GetOrAddValue("key1", 200);
        int result2 = dictionary.GetOrAddValue("Key1", 300);

        Assert.AreEqual(100, result1, "Should return existing value (case-insensitive).");
        Assert.AreEqual(100, result2, "Should return existing value (case-insensitive).");
        Assert.AreEqual(1, dictionary.Count, "Dictionary should still have 1 element.");
    }
}
