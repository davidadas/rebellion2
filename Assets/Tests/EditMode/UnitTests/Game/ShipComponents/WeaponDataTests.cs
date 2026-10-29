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
            copy.Projectile.Damage = 1;

            Assert.AreNotSame(original, copy);
            Assert.AreEqual(HardpointWeaponType.IonCannon, copy.WeaponType);
            Assert.AreEqual(WeaponDeliveryMode.Hitscan, copy.Projectile.DeliveryMode);
            Assert.AreEqual(80, original.Projectile.Damage);
            Assert.AreEqual(1, copy.Projectile.Damage);
            Assert.AreEqual(750f, copy.Range);
            Assert.AreEqual(0.75f, copy.FireIntervalSeconds);
            Assert.AreEqual(900f, copy.Projectile.Speed);
            Assert.AreEqual(0.2f, copy.Projectile.EffectDurationSeconds);
        }

        [Test]
        public void Serialize_PopulatedData_MaintainsState()
        {
            WeaponData original = CreatePopulatedData();

            string serialized = SerializationHelper.Serialize(original);
            WeaponData deserialized = SerializationHelper.Deserialize<WeaponData>(serialized);

            Assert.AreEqual(original.WeaponType, deserialized.WeaponType);
            Assert.AreEqual(original.Projectile.DeliveryMode, deserialized.Projectile.DeliveryMode);
            Assert.AreEqual(original.Projectile.Damage, deserialized.Projectile.Damage);
            Assert.AreEqual(original.Range, deserialized.Range);
            Assert.AreEqual(original.FireIntervalSeconds, deserialized.FireIntervalSeconds);
            Assert.AreEqual(original.Projectile.Speed, deserialized.Projectile.Speed);
            Assert.AreEqual(
                original.Projectile.EffectDurationSeconds,
                deserialized.Projectile.EffectDurationSeconds
            );
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
                Range = 750f,
                FireIntervalSeconds = 0.75f,
                Projectile = new ProjectileData
                {
                    DeliveryMode = WeaponDeliveryMode.Hitscan,
                    Damage = 80,
                    Speed = 900f,
                    EffectDurationSeconds = 0.2f,
                },
            };
        }
    }
}
