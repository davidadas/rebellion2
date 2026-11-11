using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game.Scripting;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Scripting
{
    public sealed class GameConditionalTests
    {
        [Test]
        public void Serialize_AuthoredCondition_WritesOnlyExpressionState()
        {
            IsActiveConditional conditional = new() { NodeInstanceID = "planet" };

            XElement serialized = XElement.Parse(SerializationHelper.Serialize(conditional));

            Assert.AreEqual("planet", serialized.Attribute("NodeInstanceID")?.Value);
            Assert.IsNull(serialized.Element("InstanceID"));
        }

        [Test]
        public void Deserialize_LegacySaveIdentity_RetainsConditionOperand()
        {
            IsActiveConditional conditional = SerializationHelper.Deserialize<IsActiveConditional>(
                "<IsActive NodeInstanceID=\"planet\"><InstanceID>unused</InstanceID></IsActive>",
                new GameSerializerSettings { IgnoreUnknownElements = true }
            );

            Assert.AreEqual("planet", conditional.NodeInstanceID);
        }
    }
}
