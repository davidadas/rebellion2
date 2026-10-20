using NUnit.Framework;
using Rebellion.Game.ShipComponents;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.ShipComponents
{
    [TestFixture]
    public sealed class WeaponDataTests
    {
        [Test]
        public void CreateCopy_PopulatedData_CreatesIndependentCopy()
        {
            WeaponData original = CreatePopulatedData();

            WeaponData copy = original.CreateCopy();
            copy.Damage = 1;

            Assert.AreNotSame(original, copy);
            Assert.AreEqual(HardpointWeaponType.IonCannon, copy.WeaponType);
            Assert.AreEqual(WeaponDeliveryMode.Hitscan, copy.DeliveryMode);
            Assert.AreEqual(80, original.Damage);
            Assert.AreEqual(1, copy.Damage);
            Assert.AreEqual(750f, copy.Range);
            Assert.AreEqual(0.75f, copy.FireIntervalSeconds);
            Assert.AreEqual(900f, copy.ProjectileSpeed);
            Assert.AreEqual(0.2f, copy.EffectDurationSeconds);
        }

        [Test]
        public void SerializeAndDeserialize_PopulatedData_MaintainsState()
        {
            WeaponData original = CreatePopulatedData();

            string serialized = SerializationHelper.Serialize(original);
            WeaponData deserialized = SerializationHelper.Deserialize<WeaponData>(serialized);

            Assert.AreEqual(original.WeaponType, deserialized.WeaponType);
            Assert.AreEqual(original.DeliveryMode, deserialized.DeliveryMode);
            Assert.AreEqual(original.Damage, deserialized.Damage);
            Assert.AreEqual(original.Range, deserialized.Range);
            Assert.AreEqual(original.FireIntervalSeconds, deserialized.FireIntervalSeconds);
            Assert.AreEqual(original.ProjectileSpeed, deserialized.ProjectileSpeed);
            Assert.AreEqual(original.EffectDurationSeconds, deserialized.EffectDurationSeconds);
        }

        /// <summary>
        /// Creates populated weapon data for copy and serialization coverage.
        /// </summary>
        /// <returns>The populated weapon data.</returns>
        private static WeaponData CreatePopulatedData()
        {
            return new WeaponData
            {
                WeaponType = HardpointWeaponType.IonCannon,
                DeliveryMode = WeaponDeliveryMode.Hitscan,
                Damage = 80,
                Range = 750f,
                FireIntervalSeconds = 0.75f,
                ProjectileSpeed = 900f,
                EffectDurationSeconds = 0.2f,
            };
        }
    }
}
