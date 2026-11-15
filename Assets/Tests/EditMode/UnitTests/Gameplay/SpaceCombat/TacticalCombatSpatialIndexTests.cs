using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSpatialIndexTests
    {
        [Test]
        public void QueryCollision_HighBitRegistrations_ReturnsUnsignedBattleOrder()
        {
            TacticalCombatUnit source = CreateCapital(2);
            TacticalCombatUnit low = CreateCapital(1);
            TacticalCombatUnit high = CreateCapital(int.MinValue);
            TacticalCombatSpatialIndex spatial = new(new[] { high, source, low });
            List<TacticalCombatUnit> candidates = new();

            spatial.QueryCollision(source, 10, candidates);

            CollectionAssert.AreEqual(new[] { low, high }, candidates);
        }

        [Test]
        public void QueryFighters_HighBitRegistrations_ReturnsUnsignedBattleOrder()
        {
            TacticalCombatUnit source = CreateCapital(2);
            TacticalCombatUnit low = CreateFighter(1);
            TacticalCombatUnit high = CreateFighter(int.MinValue);
            TacticalCombatSpatialIndex spatial = new(new[] { high, source, low });
            List<TacticalCombatUnit> candidates = new();

            spatial.QueryFighters(source, 10, candidates);

            CollectionAssert.AreEqual(new[] { low, high }, candidates);
        }

        /// <summary>
        /// Creates a capital ship with an explicit battle registration key.
        /// </summary>
        /// <param name="index">The battle registration key.</param>
        /// <returns>The complete tactical ship.</returns>
        private static TacticalCombatUnit CreateCapital(int index) =>
            new(
                new CapitalShip
                {
                    InstanceID = $"ship-{index}",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                index
            );

        /// <summary>
        /// Creates an opposing squadron with an explicit battle registration key.
        /// </summary>
        /// <param name="index">The battle registration key.</param>
        /// <returns>The complete tactical squadron.</returns>
        private static TacticalCombatUnit CreateFighter(int index) =>
            new(
                new Starfighter
                {
                    InstanceID = $"fighter-{index}",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Defender,
                index
            );
    }
}
