using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.SpaceCombat;
using Rebellion.Multiplayer;
using Rebellion.Util.Random;

namespace Rebellion.Architecture.Tests
{
    [TestFixture]
    public sealed class CombatOwnershipTests
    {
        [TestCase(typeof(TacticalCombatPlanner), "Rebellion.AI.Planners")]
        [TestCase(typeof(LinearCongruentialRandomProvider), "Rebellion.Util.Random")]
        [TestCase(typeof(TacticalCombatSeed), "Rebellion.Game.SpaceCombat")]
        [TestCase(typeof(TacticalCombatReplicaPlayback), "Rebellion.Multiplayer")]
        public void CombatServices_Responsibilities_UseOwningNamespaces(
            Type serviceType,
            string expectedNamespace
        )
        {
            Assert.That(serviceType.Namespace, Is.EqualTo(expectedNamespace));
        }

        [Test]
        public void RandomProviders_ProductionImplementations_ResideInRandomUtilities()
        {
            Type[] providers = typeof(GameRoot)
                .Assembly.GetTypes()
                .Where(type => type.IsClass && typeof(IRandomNumberProvider).IsAssignableFrom(type))
                .ToArray();

            Assert.That(providers, Is.Not.Empty);
            Assert.That(
                providers.Select(type => type.Namespace),
                Is.All.EqualTo("Rebellion.Util.Random")
            );
        }
    }
}
