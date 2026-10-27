using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.ShipComponents
{
    [TestFixture]
    public sealed class ProjectileDataTests
    {
        [Test]
        public void CreateCopy_EmittedProperties_RemainIndependentOfSource()
        {
            ProjectileData definition = new ProjectileData
            {
                Damage = 8f,
                Speed = 100f,
                ColorHex = "#2EFF4D",
                ImpactSoundPaths = new() { "impact.wav" },
            };

            ProjectileData shot = definition.CreateCopy();
            definition.Damage = 1f;
            definition.Speed = 2f;
            definition.ImpactSoundPaths.Clear();

            Assert.AreEqual(8f, shot.Damage);
            Assert.AreEqual(100f, shot.Speed);
            CollectionAssert.AreEqual(new[] { "impact.wav" }, shot.ImpactSoundPaths);
        }

        [Test]
        public void Serialize_EmittedShot_ContainsProjectilePropertiesWithoutGunSettings()
        {
            BattleProjectile shot = new BattleProjectile
            {
                Range = 50f,
                Projectile = new ProjectileData
                {
                    Damage = 8f,
                    Speed = 100f,
                    ColorHex = "#2EFF4D",
                },
            };

            string xml = SerializationHelper.Serialize(shot);
            BattleProjectile restored = SerializationHelper.Deserialize<BattleProjectile>(xml);

            Assert.AreEqual(50f, restored.Range);
            Assert.AreEqual(8f, restored.Projectile.Damage);
            Assert.AreEqual("#2EFF4D", restored.Projectile.ColorHex);
            StringAssert.DoesNotContain("ShotsPerVolley", xml);
            StringAssert.DoesNotContain("Recharge", xml);
            StringAssert.DoesNotContain("FireSoundPaths", xml);
        }
    }
}
