using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Util.Serialization
{
    [TestFixture]
    public sealed class SerializationMetadataTests
    {
        [Test]
        public void GetElementType_SerializedAlias_UsesTheSerializerMemberContract()
        {
            using StringWriter output = new StringWriter();
            new GameSerializer(typeof(MetadataSample)).Serialize(
                output,
                new MetadataSample { Value = 42 }
            );
            StringAssert.Contains("<number>42</number>", output.ToString());
            Assert.AreEqual(
                typeof(int),
                SerializationMetadata.GetElementType(typeof(MetadataSample), "number")
            );
            Assert.IsNull(SerializationMetadata.GetElementType(typeof(MetadataSample), "Unknown"));
        }

        [Test]
        public void GetAttributeType_DeclaredAttribute_ReportsItsValueType()
        {
            Assert.AreEqual(
                typeof(string),
                SerializationMetadata.GetAttributeType(typeof(MetadataSample), "ID")
            );
            Assert.IsNull(SerializationMetadata.GetAttributeType(typeof(MetadataSample), "number"));
        }

        [Test]
        public void GetCollectionItemName_CustomItemName_UsesTheSerializerContract()
        {
            Assert.AreEqual(
                "amount",
                SerializationMetadata.GetCollectionItemName(typeof(MetadataSample), "Values")
            );
            Assert.AreEqual(
                typeof(int),
                SerializationMetadata.GetCollectionItemType(typeof(List<int>))
            );
            Assert.IsNull(
                SerializationMetadata.GetCollectionItemType(typeof(Dictionary<string, int>))
            );
        }

        [Test]
        public void GetCollectionItemType_NamedListSubclass_MatchesDeserializedItems()
        {
            using StringReader input = new(
                "<FactionThemes><FactionTheme><FactionInstanceID>sample</FactionInstanceID></FactionTheme></FactionThemes>"
            );
            FactionThemes themes = (FactionThemes)
                new GameSerializer(typeof(FactionThemes)).Deserialize(input);
            Assert.AreEqual(
                typeof(FactionTheme),
                SerializationMetadata.GetCollectionItemType(themes.GetType())
            );
            Assert.AreEqual("sample", themes[0].FactionInstanceID);
        }

        [Test]
        public void GetTypes_ChangedReturnedArray_DoesNotModifyTheSerializerRegistry()
        {
            Type[] first = (Type[])SerializationMetadata.GetTypes(nameof(MetadataSample));
            Array.Clear(first, 0, first.Length);
            CollectionAssert.Contains(
                SerializationMetadata.GetTypes(nameof(MetadataSample)),
                typeof(MetadataSample)
            );
        }

        [PersistableObject]
        public sealed class MetadataSample
        {
            [PersistableMember(Name = "number")]
            public int Value { get; set; }

            [PersistableAttribute]
            public string ID { get; set; }

            [PersistableMember, PersistableCollectionItem(Name = "amount")]
            public List<int> Values { get; set; } = new List<int>();
        }
    }
}
