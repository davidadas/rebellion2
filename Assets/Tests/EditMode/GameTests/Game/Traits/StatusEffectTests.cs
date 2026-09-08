using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Traits;

namespace Rebellion.Tests.Game.Traits
{
    [TestFixture]
    public sealed class StatusEffectTests
    {
        [Test]
        public void SerializeDeserialize_PreservesAuthoredModifiers()
        {
            StatusEffect original = new StatusEffect
            {
                ID = "carbonite-sickness",
                Name = "Carbonite Sickness",
                Description = "Recently released from carbonite and temporarily disoriented.",
                Modifiers = new List<Modifier>
                {
                    new Modifier
                    {
                        Type = ModifierType.Leadership,
                        Operation = ModifierOperation.Multiply,
                        Value = 0.75m,
                        Target = ModifierTarget.Self,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(original);
            StatusEffect deserialized = SerializationHelper.Deserialize<StatusEffect>(xml);

            Assert.AreEqual(original.ID, deserialized.ID);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Description, deserialized.Description);
            Assert.AreEqual(1, deserialized.Modifiers.Count);
            Assert.AreEqual(ModifierType.Leadership, deserialized.Modifiers[0].Type);
            Assert.AreEqual(0.75m, deserialized.Modifiers[0].Value);
        }
    }
}
