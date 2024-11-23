using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

public enum TestEnum
{
    Value1,
    Value2,
    Value3,
}

[PersistableObject]
public interface ITestItem
{
    [PersistableMember]
    string Name { get; set; }
}

[PersistableObject]
public class SimpleItem : ITestItem
{
    [PersistableMember]
    public string Name { get; set; }

    [PersistableMember]
    public int Value { get; set; }

    [PersistableMember]
    public TestEnum EnumValue { get; set; }

    [PersistableAttribute]
    public string AttributeVariable { get; set; }

    public string PublicVariable { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    private string PrivateVariable { get; set; }

    public SimpleItem() { }
}

[PersistableObject]
public class NestedItem
{
    [PersistableMember]
    public string Identifier { get; set; }

    [PersistableMember]
    public List<SimpleItem> Items { get; set; }

    public string PublicVariable { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    public NestedItem()
    {
        Items = new List<SimpleItem>();
    }
}

[PersistableObject]
public class DeeplyNestedItem
{
    [PersistableMember]
    public string Label { get; set; }

    [PersistableMember]
    public List<NestedItem> NestedItems { get; set; }

    public string PublicVariable { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    public DeeplyNestedItem()
    {
        NestedItems = new List<NestedItem>();
    }
}

[PersistableObject]
public class InterfaceCollection
{
    [PersistableMember]
    public List<ITestItem> Items { get; set; }

    public string PublicVariable { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    public InterfaceCollection()
    {
        Items = new List<ITestItem>();
    }
}

[PersistableObject]
public class SimpleItemWithDictionary
{
    [PersistableMember]
    public string Name { get; set; }

    [PersistableMember]
    public Dictionary<string, int> StringIntDict { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    public SimpleItemWithDictionary()
    {
        StringIntDict = new Dictionary<string, int>();
    }
}

[PersistableObject]
public class ComplexItemWithDictionary
{
    [PersistableMember]
    public string Name { get; set; }

    [PersistableMember]
    public Dictionary<string, SimpleItem> StringObjectDict { get; set; }

    [PersistableIgnore]
    public string IgnoredPublicVariable { get; set; }

    public ComplexItemWithDictionary()
    {
        StringObjectDict = new Dictionary<string, SimpleItem>();
    }
}

[PersistableObject]
public class ItemWithAttributes
{
    [PersistableMember]
    public string Name { get; set; }

    [PersistableAttribute]
    public int AttributeValue { get; set; }

    [PersistableAttribute]
    public string AttributeString { get; set; }

    [PersistableAttribute]
    public TestEnum AttributeEnum { get; set; }

    public ItemWithAttributes() { }
}

[PersistableObject]
public class ItemWithCustomName
{
    [PersistableMember(Name = "CustomNamedProperty")]
    public string Property { get; set; }

    public ItemWithCustomName() { }
}

[PersistableObject]
public class ItemWithInclude
{
    [PersistableInclude(typeof(SimpleItem))]
    [PersistableInclude(typeof(NestedItem))]
    public ITestItem Item { get; set; }

    public ItemWithInclude() { }
}

[TestFixture]
public class GameSerializerTests
{
    [Test]
    public void Serialize_SingleObject_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        SimpleItem item = new SimpleItem
        {
            Name = "SingleObject",
            Value = 42,
            EnumValue = TestEnum.Value1,
            AttributeVariable = "AttributeValue",
            PublicVariable = "PublicValue",
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem AttributeVariable=""AttributeValue"">
  <Name>SingleObject</Name>
  <Value>42</Value>
  <EnumValue>Value1</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_SingleObject_ReturnsExpectedObject()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem AttributeVariable=""AttributeValue"">
  <Name>SingleObject</Name>
  <Value>42</Value>
  <EnumValue>Value1</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        SimpleItem deserialized = (SimpleItem)DeserializeFromString(serializer, xmlInput);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual("SingleObject", deserialized.Name);
        Assert.AreEqual(42, deserialized.Value);
        Assert.AreEqual(TestEnum.Value1, deserialized.EnumValue);
        Assert.AreEqual("AttributeValue", deserialized.AttributeVariable);
        Assert.AreEqual("PublicValue", deserialized.PublicVariable);
    }

    [Test]
    public void Serialize_ItemWithAttributes_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithAttributes));
        ItemWithAttributes item = new ItemWithAttributes
        {
            Name = "AttributeItem",
            AttributeValue = 42,
            AttributeString = "StringAttribute",
            AttributeEnum = TestEnum.Value2,
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithAttributes AttributeValue=""42"" AttributeString=""StringAttribute"" AttributeEnum=""Value2"">
  <Name>AttributeItem</Name>
</ItemWithAttributes>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_ItemWithAttributes_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithAttributes));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithAttributes AttributeValue=""42"" AttributeString=""StringAttribute"" AttributeEnum=""Value2"">
  <Name>AttributeItem</Name>
</ItemWithAttributes>";

        ItemWithAttributes deserialized = (ItemWithAttributes)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual("AttributeItem", deserialized.Name);
        Assert.AreEqual(42, deserialized.AttributeValue);
        Assert.AreEqual("StringAttribute", deserialized.AttributeString);
        Assert.AreEqual(TestEnum.Value2, deserialized.AttributeEnum);
    }

    [Test]
    public void Serialize_MixedMemberAndAttribute_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        SimpleItem item = new SimpleItem
        {
            Name = "MixedItem",
            Value = 100,
            EnumValue = TestEnum.Value3,
            AttributeVariable = "AttributeValue",
            PublicVariable = "PublicValue",
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem AttributeVariable=""AttributeValue"">
  <Name>MixedItem</Name>
  <Value>100</Value>
  <EnumValue>Value3</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_MixedMemberAndAttribute_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem AttributeVariable=""AttributeValue"">
  <Name>MixedItem</Name>
  <Value>100</Value>
  <EnumValue>Value3</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        SimpleItem deserialized = (SimpleItem)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual("MixedItem", deserialized.Name);
        Assert.AreEqual(100, deserialized.Value);
        Assert.AreEqual(TestEnum.Value3, deserialized.EnumValue);
        Assert.AreEqual("AttributeValue", deserialized.AttributeVariable);
        Assert.AreEqual("PublicValue", deserialized.PublicVariable);
    }

    [Test]
    public void Serialize_SimpleItem_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        SimpleItem item = new SimpleItem
        {
            Name = "Item1",
            Value = 42,
            EnumValue = TestEnum.Value2,
            PublicVariable = "PublicValue",
            IgnoredPublicVariable = "IgnoredValue",
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem>
  <Name>Item1</Name>
  <Value>42</Value>
  <EnumValue>Value2</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
        Assert.IsFalse(
            serializedXml.Contains("IgnoredPublicVariable"),
            "Serialized XML should not contain ignored public variable."
        );
    }

    [Test]
    public void Deserialize_SimpleItem_DoesNotIgnoreIgnoredPublicVariable()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem>
  <Name>Item1</Name>
  <Value>42</Value>
  <EnumValue>Value2</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
  <IgnoredPublicVariable>IgnoredValue</IgnoredPublicVariable>
</SimpleItem>";

        SimpleItem deserialized = (SimpleItem)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual("Item1", deserialized.Name);
        Assert.AreEqual(42, deserialized.Value);
        Assert.AreEqual(TestEnum.Value2, deserialized.EnumValue);
        Assert.AreEqual("PublicValue", deserialized.PublicVariable);
        Assert.AreEqual("IgnoredValue", deserialized.IgnoredPublicVariable);
    }

    [Test]
    public void Serialize_NestedItem_IgnoresIgnoredPublicVariables()
    {
        GameSerializer serializer = new GameSerializer(typeof(NestedItem));
        NestedItem nestedItem = new NestedItem
        {
            Identifier = "Nested1",
            PublicVariable = "NestedPublicValue",
            IgnoredPublicVariable = "NestedIgnoredValue",
            Items = new List<SimpleItem>
            {
                new SimpleItem
                {
                    Name = "Simple1",
                    Value = 10,
                    EnumValue = TestEnum.Value1,
                    PublicVariable = "SimplePublicValue",
                    IgnoredPublicVariable = "SimpleIgnoredValue",
                },
            },
        };

        string serializedXml = SerializeToString(serializer, nestedItem);

        Assert.IsTrue(serializedXml.Contains("<Identifier>Nested1</Identifier>"));
        Assert.IsTrue(serializedXml.Contains("<PublicVariable>NestedPublicValue</PublicVariable>"));
        Assert.IsFalse(serializedXml.Contains("NestedIgnoredValue"));
        Assert.IsTrue(serializedXml.Contains("<Name>Simple1</Name>"));
        Assert.IsTrue(serializedXml.Contains("<PublicVariable>SimplePublicValue</PublicVariable>"));
        Assert.IsFalse(serializedXml.Contains("SimpleIgnoredValue"));
    }

    [Test]
    public void Deserialize_NestedItem_DoesNotIgnoreIgnoredPublicVariables()
    {
        GameSerializer serializer = new GameSerializer(typeof(NestedItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<NestedItem>
  <Identifier>Nested1</Identifier>
  <Items>
    <SimpleItem>
      <Name>Simple1</Name>
      <Value>10</Value>
      <EnumValue>Value1</EnumValue>
      <PublicVariable>SimplePublicValue</PublicVariable>
      <IgnoredPublicVariable>SimpleIgnoredValue</IgnoredPublicVariable>
    </SimpleItem>
  </Items>
  <PublicVariable>NestedPublicValue</PublicVariable>
  <IgnoredPublicVariable>NestedIgnoredValue</IgnoredPublicVariable>
</NestedItem>";

        NestedItem deserialized = (NestedItem)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual("Nested1", deserialized.Identifier);
        Assert.AreEqual("NestedPublicValue", deserialized.PublicVariable);
        Assert.AreEqual("NestedIgnoredValue", deserialized.IgnoredPublicVariable);
        Assert.AreEqual(1, deserialized.Items.Count);
        Assert.AreEqual("Simple1", deserialized.Items[0].Name);
        Assert.AreEqual("SimplePublicValue", deserialized.Items[0].PublicVariable);
        Assert.AreEqual("SimpleIgnoredValue", deserialized.Items[0].IgnoredPublicVariable);
    }

    [Test]
    public void Serialize_DeeplyNestedItem_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(DeeplyNestedItem));
        DeeplyNestedItem deepItem = new DeeplyNestedItem
        {
            Label = "DeepItem",
            PublicVariable = "DeepPublicValue",
            NestedItems = new List<NestedItem>
            {
                new NestedItem
                {
                    Identifier = "Nested1",
                    PublicVariable = "NestedPublicValue1",
                    Items = new List<SimpleItem>
                    {
                        new SimpleItem
                        {
                            Name = "Simple1",
                            Value = 10,
                            EnumValue = TestEnum.Value1,
                            PublicVariable = "SimplePublicValue1",
                        },
                        new SimpleItem
                        {
                            Name = "Simple2",
                            Value = 20,
                            EnumValue = TestEnum.Value2,
                            PublicVariable = "SimplePublicValue2",
                        },
                    },
                },
                new NestedItem
                {
                    Identifier = "Nested2",
                    PublicVariable = "NestedPublicValue2",
                    Items = new List<SimpleItem>
                    {
                        new SimpleItem
                        {
                            Name = "Simple3",
                            Value = 30,
                            EnumValue = TestEnum.Value3,
                            PublicVariable = "SimplePublicValue3",
                        },
                    },
                },
            },
        };

        string serializedXml = SerializeToString(serializer, deepItem);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<DeeplyNestedItem>
  <Label>DeepItem</Label>
  <NestedItems>
    <NestedItem>
      <Identifier>Nested1</Identifier>
      <Items>
        <SimpleItem>
          <Name>Simple1</Name>
          <Value>10</Value>
          <EnumValue>Value1</EnumValue>
          <PublicVariable>SimplePublicValue1</PublicVariable>
        </SimpleItem>
        <SimpleItem>
          <Name>Simple2</Name>
          <Value>20</Value>
          <EnumValue>Value2</EnumValue>
          <PublicVariable>SimplePublicValue2</PublicVariable>
        </SimpleItem>
      </Items>
      <PublicVariable>NestedPublicValue1</PublicVariable>
    </NestedItem>
    <NestedItem>
      <Identifier>Nested2</Identifier>
      <Items>
        <SimpleItem>
          <Name>Simple3</Name>
          <Value>30</Value>
          <EnumValue>Value3</EnumValue>
          <PublicVariable>SimplePublicValue3</PublicVariable>
        </SimpleItem>
      </Items>
      <PublicVariable>NestedPublicValue2</PublicVariable>
    </NestedItem>
  </NestedItems>
  <PublicVariable>DeepPublicValue</PublicVariable>
</DeeplyNestedItem>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Serialize_InterfaceCollection_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(InterfaceCollection));
        InterfaceCollection interfaceCollection = new InterfaceCollection
        {
            Items = new List<ITestItem>
            {
                new SimpleItem
                {
                    Name = "InterfaceItem1",
                    Value = 100,
                    EnumValue = TestEnum.Value1,
                    PublicVariable = "PublicValue1",
                },
                new SimpleItem
                {
                    Name = "InterfaceItem2",
                    Value = 200,
                    EnumValue = TestEnum.Value2,
                    PublicVariable = "PublicValue2",
                },
            },
            PublicVariable = "CollectionPublicValue",
        };

        string serializedXml = SerializeToString(serializer, interfaceCollection);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<InterfaceCollection>
  <Items>
    <SimpleItem>
      <Name>InterfaceItem1</Name>
      <Value>100</Value>
      <EnumValue>Value1</EnumValue>
      <PublicVariable>PublicValue1</PublicVariable>
    </SimpleItem>
    <SimpleItem>
      <Name>InterfaceItem2</Name>
      <Value>200</Value>
      <EnumValue>Value2</EnumValue>
      <PublicVariable>PublicValue2</PublicVariable>
    </SimpleItem>
  </Items>
  <PublicVariable>CollectionPublicValue</PublicVariable>
</InterfaceCollection>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Serialize_NestedItem_WithInterfaceAndDeeplyNested_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(DeeplyNestedItem));
        DeeplyNestedItem deeplyNestedItem = new DeeplyNestedItem
        {
            Label = "ComplexDeep",
            PublicVariable = "DeepPublicValue",
            NestedItems = new List<NestedItem>
            {
                new NestedItem
                {
                    Identifier = "Layer1",
                    PublicVariable = "NestedPublicValue",
                    Items = new List<SimpleItem>
                    {
                        new SimpleItem
                        {
                            Name = "DeepItem1",
                            Value = 300,
                            EnumValue = TestEnum.Value1,
                            PublicVariable = "SimplePublicValue1",
                        },
                        new SimpleItem
                        {
                            Name = "DeepItem2",
                            Value = 400,
                            EnumValue = TestEnum.Value2,
                            PublicVariable = "SimplePublicValue2",
                        },
                    },
                },
            },
        };

        string serializedXml = SerializeToString(serializer, deeplyNestedItem);

        Assert.IsTrue(serializedXml.Contains("<Label>ComplexDeep</Label>"));
        Assert.IsTrue(serializedXml.Contains("<Identifier>Layer1</Identifier>"));
        Assert.IsTrue(serializedXml.Contains("<Name>DeepItem1</Name>"));
        Assert.IsTrue(serializedXml.Contains("<EnumValue>Value1</EnumValue>"));
        Assert.IsTrue(serializedXml.Contains("<PublicVariable>DeepPublicValue</PublicVariable>"));
        Assert.IsTrue(serializedXml.Contains("<PublicVariable>NestedPublicValue</PublicVariable>"));
        Assert.IsTrue(
            serializedXml.Contains("<PublicVariable>SimplePublicValue1</PublicVariable>")
        );
    }

    [Test]
    public void Deserialize_SimpleItem_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem>
  <Name>Item1</Name>
  <Value>42</Value>
  <EnumValue>Value2</EnumValue>
  <PublicVariable>PublicValue</PublicVariable>
</SimpleItem>";

        SimpleItem deserialized = (SimpleItem)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual("Item1", deserialized.Name);
        Assert.AreEqual(42, deserialized.Value);
        Assert.AreEqual(TestEnum.Value2, deserialized.EnumValue);
        Assert.AreEqual("PublicValue", deserialized.PublicVariable);
    }

    [Test]
    public void Deserialize_DeeplyNestedItem_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(DeeplyNestedItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<DeeplyNestedItem>
  <Label>DeepItem</Label>
  <NestedItems>
    <NestedItem>
      <Identifier>Nested1</Identifier>
      <Items>
        <SimpleItem>
          <Name>Simple1</Name>
          <Value>10</Value>
          <EnumValue>Value1</EnumValue>
          <PublicVariable>SimplePublicValue1</PublicVariable>
        </SimpleItem>
        <SimpleItem>
          <Name>Simple2</Name>
          <Value>20</Value>
          <EnumValue>Value2</EnumValue>
          <PublicVariable>SimplePublicValue2</PublicVariable>
        </SimpleItem>
      </Items>
      <PublicVariable>NestedPublicValue</PublicVariable>
    </NestedItem>
  </NestedItems>
  <PublicVariable>DeepPublicValue</PublicVariable>
</DeeplyNestedItem>";

        DeeplyNestedItem deserialized = (DeeplyNestedItem)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual("DeepItem", deserialized.Label);
        Assert.AreEqual("DeepPublicValue", deserialized.PublicVariable);
        Assert.AreEqual(1, deserialized.NestedItems.Count);
        Assert.AreEqual("Nested1", deserialized.NestedItems[0].Identifier);
        Assert.AreEqual("NestedPublicValue", deserialized.NestedItems[0].PublicVariable);
        Assert.AreEqual("Simple1", deserialized.NestedItems[0].Items[0].Name);
        Assert.AreEqual(10, deserialized.NestedItems[0].Items[0].Value);
        Assert.AreEqual(TestEnum.Value1, deserialized.NestedItems[0].Items[0].EnumValue);
        Assert.AreEqual("SimplePublicValue1", deserialized.NestedItems[0].Items[0].PublicVariable);
    }

    [Test]
    public void Deserialize_InterfaceCollection_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(InterfaceCollection));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<InterfaceCollection>
  <Items>
    <SimpleItem>
      <Name>InterfaceItem1</Name>
      <Value>100</Value>
      <EnumValue>Value1</EnumValue>
      <PublicVariable>PublicValue1</PublicVariable>
    </SimpleItem>
  </Items>
  <PublicVariable>CollectionPublicValue</PublicVariable>
</InterfaceCollection>";

        InterfaceCollection deserialized = (InterfaceCollection)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual(1, deserialized.Items.Count);
        Assert.AreEqual("InterfaceItem1", deserialized.Items[0].Name);
        Assert.AreEqual(100, ((SimpleItem)deserialized.Items[0]).Value);
        Assert.AreEqual(TestEnum.Value1, ((SimpleItem)deserialized.Items[0]).EnumValue);
        Assert.AreEqual("PublicValue1", ((SimpleItem)deserialized.Items[0]).PublicVariable);
        Assert.AreEqual("CollectionPublicValue", deserialized.PublicVariable);
    }

    [Test]
    public void Serialize_PublicVariableWithoutAttribute_IsIncludedInXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        SimpleItem item = new SimpleItem
        {
            Name = "TestItem",
            Value = 100,
            EnumValue = TestEnum.Value3,
            PublicVariable = "This should be serialized",
        };

        string serializedXml = SerializeToString(serializer, item);
        Assert.IsTrue(
            serializedXml.Contains("<PublicVariable>This should be serialized</PublicVariable>"),
            "Serialized XML should include the public variable without PersistableMember attribute."
        );
    }

    [Test]
    public void Deserialize_PublicVariableWithoutAttribute_IsDeserialized()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItem));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItem>
  <Name>TestItem</Name>
  <Value>100</Value>
  <EnumValue>Value3</EnumValue>
  <PublicVariable>This should be deserialized</PublicVariable>
</SimpleItem>";

        SimpleItem deserialized = (SimpleItem)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual("TestItem", deserialized.Name);
        Assert.AreEqual(100, deserialized.Value);
        Assert.AreEqual(TestEnum.Value3, deserialized.EnumValue);
        Assert.AreEqual(
            "This should be deserialized",
            deserialized.PublicVariable,
            "Public variable without PersistableMember attribute should be deserialized."
        );
    }

    [Test]
    public void Serialize_SimpleItemWithDictionary_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItemWithDictionary));
        SimpleItemWithDictionary item = new SimpleItemWithDictionary
        {
            Name = "DictItem",
            StringIntDict = new Dictionary<string, int> { { "Key1", 10 }, { "Key2", 20 } },
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItemWithDictionary>
  <Name>DictItem</Name>
  <StringIntDict>
    <Entry>
      <Key>Key1</Key>
      <Value>10</Value>
    </Entry>
    <Entry>
      <Key>Key2</Key>
      <Value>20</Value>
    </Entry>
  </StringIntDict>
</SimpleItemWithDictionary>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_SimpleItemWithDictionary_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(SimpleItemWithDictionary));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItemWithDictionary>
  <Name>DictItem</Name>
  <StringIntDict>
    <Entry>
      <Key>Key1</Key>
      <Value>10</Value>
    </Entry>
    <Entry>
      <Key>Key2</Key>
      <Value>20</Value>
    </Entry>
  </StringIntDict>
</SimpleItemWithDictionary>";

        SimpleItemWithDictionary deserialized = (SimpleItemWithDictionary)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual("DictItem", deserialized.Name);
        Assert.AreEqual(2, deserialized.StringIntDict.Count);
        Assert.AreEqual(10, deserialized.StringIntDict["Key1"]);
        Assert.AreEqual(20, deserialized.StringIntDict["Key2"]);
    }

    [Test]
    public void Serialize_ComplexItemWithDictionary_ReturnsExpectedXml()
    {
        GameSerializer serializer = new GameSerializer(typeof(ComplexItemWithDictionary));
        ComplexItemWithDictionary item = new ComplexItemWithDictionary
        {
            Name = "ComplexDictItem",
            StringObjectDict = new Dictionary<string, SimpleItem>
            {
                {
                    "ObjectKey1",
                    new SimpleItem
                    {
                        Name = "Item1",
                        Value = 100,
                        EnumValue = TestEnum.Value1,
                        PublicVariable = "PublicValue1",
                    }
                },
                {
                    "ObjectKey2",
                    new SimpleItem
                    {
                        Name = "Item2",
                        Value = 200,
                        EnumValue = TestEnum.Value2,
                        PublicVariable = "PublicValue2",
                    }
                },
            },
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ComplexItemWithDictionary>
  <Name>ComplexDictItem</Name>
  <StringObjectDict>
    <Entry>
      <Key>ObjectKey1</Key>
      <Value>
        <SimpleItem>
          <Name>Item1</Name>
          <Value>100</Value>
          <EnumValue>Value1</EnumValue>
          <PublicVariable>PublicValue1</PublicVariable>
        </SimpleItem>
      </Value>
    </Entry>
    <Entry>
      <Key>ObjectKey2</Key>
      <Value>
        <SimpleItem>
          <Name>Item2</Name>
          <Value>200</Value>
          <EnumValue>Value2</EnumValue>
          <PublicVariable>PublicValue2</PublicVariable>
        </SimpleItem>
      </Value>
    </Entry>
  </StringObjectDict>
</ComplexItemWithDictionary>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_ComplexItemWithDictionary_RetainsObjectProperties()
    {
        GameSerializer serializer = new GameSerializer(typeof(ComplexItemWithDictionary));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ComplexItemWithDictionary>
  <Name>ComplexDictItem</Name>
  <StringObjectDict>
    <Entry>
      <Key>ObjectKey1</Key>
      <Value>
        <SimpleItem>
          <Name>Item1</Name>
          <Value>100</Value>
          <EnumValue>Value1</EnumValue>
          <PublicVariable>PublicValue1</PublicVariable>
        </SimpleItem>
      </Value>
    </Entry>
    <Entry>
      <Key>ObjectKey2</Key>
      <Value>
        <SimpleItem>
          <Name>Item2</Name>
          <Value>200</Value>
          <EnumValue>Value2</EnumValue>
          <PublicVariable>PublicValue2</PublicVariable>
        </SimpleItem>
      </Value>
    </Entry>
  </StringObjectDict>
</ComplexItemWithDictionary>";

        ComplexItemWithDictionary deserialized = (ComplexItemWithDictionary)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual("ComplexDictItem", deserialized.Name);
        Assert.AreEqual(2, deserialized.StringObjectDict.Count);
        Assert.AreEqual("Item1", deserialized.StringObjectDict["ObjectKey1"].Name);
        Assert.AreEqual(100, deserialized.StringObjectDict["ObjectKey1"].Value);
        Assert.AreEqual(TestEnum.Value1, deserialized.StringObjectDict["ObjectKey1"].EnumValue);
        Assert.AreEqual("PublicValue1", deserialized.StringObjectDict["ObjectKey1"].PublicVariable);
        Assert.AreEqual("Item2", deserialized.StringObjectDict["ObjectKey2"].Name);
        Assert.AreEqual(200, deserialized.StringObjectDict["ObjectKey2"].Value);
        Assert.AreEqual(TestEnum.Value2, deserialized.StringObjectDict["ObjectKey2"].EnumValue);
        Assert.AreEqual("PublicValue2", deserialized.StringObjectDict["ObjectKey2"].PublicVariable);
    }

    [Test]
    public void Serialize_ArrayOfSimpleItems_ReturnsExpectedXml()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "SimpleItems" };
        GameSerializer serializer = new GameSerializer(typeof(List<SimpleItem>), settings);
        List<SimpleItem> items = new List<SimpleItem>
        {
            new SimpleItem
            {
                Name = "Item1",
                Value = 10,
                EnumValue = TestEnum.Value1,
                PublicVariable = "Public1",
            },
            new SimpleItem
            {
                Name = "Item2",
                Value = 20,
                EnumValue = TestEnum.Value2,
                PublicVariable = "Public2",
            },
            new SimpleItem
            {
                Name = "Item3",
                Value = 30,
                EnumValue = TestEnum.Value3,
                PublicVariable = "Public3",
            },
        };

        string serializedXml = SerializeToString(serializer, items);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<SimpleItems>
  <SimpleItem>
    <Name>Item1</Name>
    <Value>10</Value>
    <EnumValue>Value1</EnumValue>
    <PublicVariable>Public1</PublicVariable>
  </SimpleItem>
  <SimpleItem>
    <Name>Item2</Name>
    <Value>20</Value>
    <EnumValue>Value2</EnumValue>
    <PublicVariable>Public2</PublicVariable>
  </SimpleItem>
  <SimpleItem>
    <Name>Item3</Name>
    <Value>30</Value>
    <EnumValue>Value3</EnumValue>
    <PublicVariable>Public3</PublicVariable>
  </SimpleItem>
</SimpleItems>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_ArrayOfSimpleItems_RetainsObjectProperties()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "SimpleItems" };
        GameSerializer serializer = new GameSerializer(typeof(List<SimpleItem>), settings);
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ArrayOfSimpleItem>
  <SimpleItem>
    <Name>Item1</Name>
    <Value>10</Value>
    <EnumValue>Value1</EnumValue>
    <PublicVariable>Public1</PublicVariable>
  </SimpleItem>
  <SimpleItem>
    <Name>Item2</Name>
    <Value>20</Value>
    <EnumValue>Value2</EnumValue>
    <PublicVariable>Public2</PublicVariable>
  </SimpleItem>
  <SimpleItem>
    <Name>Item3</Name>
    <Value>30</Value>
    <EnumValue>Value3</EnumValue>
    <PublicVariable>Public3</PublicVariable>
  </SimpleItem>
</ArrayOfSimpleItem>";

        List<SimpleItem> deserialized =
            (List<SimpleItem>)DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual(3, deserialized.Count);
        Assert.AreEqual("Item1", deserialized[0].Name);
        Assert.AreEqual(10, deserialized[0].Value);
        Assert.AreEqual(TestEnum.Value1, deserialized[0].EnumValue);
        Assert.AreEqual("Public1", deserialized[0].PublicVariable);
        Assert.AreEqual("Item2", deserialized[1].Name);
        Assert.AreEqual(20, deserialized[1].Value);
        Assert.AreEqual(TestEnum.Value2, deserialized[1].EnumValue);
        Assert.AreEqual("Public2", deserialized[1].PublicVariable);
        Assert.AreEqual("Item3", deserialized[2].Name);
        Assert.AreEqual(30, deserialized[2].Value);
        Assert.AreEqual(TestEnum.Value3, deserialized[2].EnumValue);
        Assert.AreEqual("Public3", deserialized[2].PublicVariable);
    }

    [Test]
    public void Serialize_IntArray_ReturnsExpectedXml()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "IntArray" };
        GameSerializer serializer = new GameSerializer(typeof(int[]), settings);
        int[] array = new int[] { 1, 2, 3, 4, 5 };

        string serializedXml = SerializeToString(serializer, array);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<IntArray>
  <Int32>1</Int32>
  <Int32>2</Int32>
  <Int32>3</Int32>
  <Int32>4</Int32>
  <Int32>5</Int32>
</IntArray>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_IntArray_RetainsValues()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "IntArray" };
        GameSerializer serializer = new GameSerializer(typeof(int[]), settings);
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<IntArray>
  <Int32>1</Int32>
  <Int32>2</Int32>
  <Int32>3</Int32>
  <Int32>4</Int32>
  <Int32>5</Int32>
</IntArray>";

        int[] deserialized = (int[])DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual(5, deserialized.Length);
        Assert.AreEqual(1, deserialized[0]);
        Assert.AreEqual(2, deserialized[1]);
        Assert.AreEqual(3, deserialized[2]);
        Assert.AreEqual(4, deserialized[3]);
        Assert.AreEqual(5, deserialized[4]);
    }

    [Test]
    public void Serialize_StringArray_ReturnsExpectedXml()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "StringArray" };
        GameSerializer serializer = new GameSerializer(typeof(string[]), settings);
        string[] array = new string[] { "One", "Two", "Three", "Four", "Five" };

        string serializedXml = SerializeToString(serializer, array);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<StringArray>
  <String>One</String>
  <String>Two</String>
  <String>Three</String>
  <String>Four</String>
  <String>Five</String>
</StringArray>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_StringArray_RetainsValues()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "StringArray" };
        GameSerializer serializer = new GameSerializer(typeof(string[]), settings);
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<StringArray>
  <String>One</String>
  <String>Two</String>
  <String>Three</String>
  <String>Four</String>
  <String>Five</String>
</StringArray>";

        string[] deserialized = (string[])DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual(5, deserialized.Length);
        Assert.AreEqual("One", deserialized[0]);
        Assert.AreEqual("Two", deserialized[1]);
        Assert.AreEqual("Three", deserialized[2]);
        Assert.AreEqual("Four", deserialized[3]);
        Assert.AreEqual("Five", deserialized[4]);
    }

    [Test]
    public void Serialize_FloatArray_ReturnsExpectedXml()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "FloatArray" };
        GameSerializer serializer = new GameSerializer(typeof(float[]), settings);
        float[] array = new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f };

        string serializedXml = SerializeToString(serializer, array);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<FloatArray>
  <Single>1.1</Single>
  <Single>2.2</Single>
  <Single>3.3</Single>
  <Single>4.4</Single>
  <Single>5.5</Single>
</FloatArray>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_FloatArray_RetainsValues()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "FloatArray" };
        GameSerializer serializer = new GameSerializer(typeof(float[]), settings);
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<FloatArray>
  <Single>1.1</Single>
  <Single>2.2</Single>
  <Single>3.3</Single>
  <Single>4.4</Single>
  <Single>5.5</Single>
</FloatArray>";

        float[] deserialized = (float[])DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual(5, deserialized.Length);
        Assert.AreEqual(1.1f, deserialized[0], 0.0001f);
        Assert.AreEqual(2.2f, deserialized[1], 0.0001f);
        Assert.AreEqual(3.3f, deserialized[2], 0.0001f);
        Assert.AreEqual(4.4f, deserialized[3], 0.0001f);
        Assert.AreEqual(5.5f, deserialized[4], 0.0001f);
    }

    [Test]
    public void Serialize_EnumArray_ReturnsExpectedXml()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "EnumArray" };
        GameSerializer serializer = new GameSerializer(typeof(TestEnum[]), settings);
        TestEnum[] array = new TestEnum[] { TestEnum.Value1, TestEnum.Value2, TestEnum.Value3 };

        string serializedXml = SerializeToString(serializer, array);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<EnumArray>
  <TestEnum>Value1</TestEnum>
  <TestEnum>Value2</TestEnum>
  <TestEnum>Value3</TestEnum>
</EnumArray>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should match expected XML."
        );
    }

    [Test]
    public void Deserialize_EnumArray_RetainsValues()
    {
        GameSerializerSettings settings = new GameSerializerSettings { RootName = "EnumArray" };
        GameSerializer serializer = new GameSerializer(typeof(TestEnum[]), settings);
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<EnumArray>
  <TestEnum>Value1</TestEnum>
  <TestEnum>Value2</TestEnum>
  <TestEnum>Value3</TestEnum>
</EnumArray>";

        TestEnum[] deserialized = (TestEnum[])DeserializeFromString(serializer, xmlInput);

        Assert.AreEqual(3, deserialized.Length);
        Assert.AreEqual(TestEnum.Value1, deserialized[0]);
        Assert.AreEqual(TestEnum.Value2, deserialized[1]);
        Assert.AreEqual(TestEnum.Value3, deserialized[2]);
    }

    [Test]
    public void Serialize_ItemWithCustomName_UsesCustomName()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithCustomName));
        ItemWithCustomName item = new ItemWithCustomName { Property = "TestValue" };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithCustomName>
  <CustomNamedProperty>TestValue</CustomNamedProperty>
</ItemWithCustomName>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should use the custom property name."
        );
    }

    [Test]
    public void Deserialize_ItemWithCustomName_UsesCustomName()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithCustomName));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithCustomName>
  <CustomNamedProperty>TestValue</CustomNamedProperty>
</ItemWithCustomName>";

        ItemWithCustomName deserialized = (ItemWithCustomName)DeserializeFromString(
            serializer,
            xmlInput
        );

        Assert.AreEqual(
            "TestValue",
            deserialized.Property,
            "Deserialized value should match the input for the custom-named property."
        );
    }

    [Test]
    public void Serialize_ItemWithInclude_UsesActualType()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithInclude));
        ItemWithInclude item = new ItemWithInclude
        {
            Item = new SimpleItem { Name = "SimpleItemName", Value = 42 },
        };

        string serializedXml = SerializeToString(serializer, item);
        string expectedXml =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithInclude>
  <SimpleItem>
    <Name>SimpleItemName</Name>
    <Value>42</Value>
    <EnumValue>Value1</EnumValue>
  </SimpleItem>
</ItemWithInclude>";

        Assert.AreEqual(
            expectedXml.Trim(),
            serializedXml.Trim(),
            "Serialized XML should use the actual type name for the included item."
        );
    }

    [Test]
    public void Deserialize_ItemWithInclude_UsesActualType()
    {
        GameSerializer serializer = new GameSerializer(typeof(ItemWithInclude));
        string xmlInput =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<ItemWithInclude>
  <SimpleItem>
    <Name>SimpleItemName</Name>
    <Value>42</Value>
  </SimpleItem>
</ItemWithInclude>";

        ItemWithInclude deserialized = (ItemWithInclude)DeserializeFromString(serializer, xmlInput);

        Assert.IsNotNull(deserialized.Item);
        Assert.IsInstanceOf<SimpleItem>(deserialized.Item);
        SimpleItem simpleItem = (SimpleItem)deserialized.Item;
        Assert.AreEqual("SimpleItemName", simpleItem.Name);
        Assert.AreEqual(42, simpleItem.Value);
    }

    private string SerializeToString(GameSerializer serializer, object obj)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            serializer.Serialize(stream, obj);
            stream.Seek(0, SeekOrigin.Begin);

            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    private object DeserializeFromString(GameSerializer serializer, string xml)
    {
        using (MemoryStream stream = new MemoryStream())
        using (StreamWriter writer = new StreamWriter(stream))
        {
            writer.Write(xml);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            return serializer.Deserialize(stream);
        }
    }
}
