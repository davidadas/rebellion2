using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Tactical;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatMotionTests
    {
        [Test]
        public void Advance_UnorderedPosition_UsesLowerBattleBoundary()
        {
            CombatUnit unit = new(
                new CapitalShip
                {
                    InstanceID = "ship",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                1
            )
            {
                Position = new Vector3(float.NaN, float.NaN, float.NaN),
            };
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3.UnitZ);
            TacticalCombatSpatialIndex spatial = new(new[] { unit });
            TacticalCombatMotion motion = new();

            motion.Advance(unit, group, null, 100, spatial, 16);

            Assert.AreEqual(new Vector3(-100 * 0.6666667f), unit.Position);
        }

        [Test]
        public void Advance_FighterWithPositiveMovementBelowStoredPrecision_StillTurns()
        {
            CombatUnit unit = CreateFighter();
            unit.CommandBudget = float.Epsilon;
            for (int index = 0; index < 3; index++)
                unit.Systems.ApplyComponentEvent(TacticalShipSystem.Engines, false);
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3.UnitX);
            TacticalCombatSpatialIndex spatial = new(new[] { unit });
            TacticalCombatMotion motion = new();

            motion.Advance(unit, group, null, 100, spatial, 1000);

            Assert.AreEqual(0, unit.GetSpeed());
            Assert.That(unit.Heading.X, Is.GreaterThan(0));
        }

        [Test]
        public void Advance_FighterWithUnorderedElapsed_PreservesHeading()
        {
            CombatUnit unit = CreateFighter();
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3.UnitX);
            TacticalCombatSpatialIndex spatial = new(new[] { unit });
            TacticalCombatMotion motion = new();

            motion.Advance(unit, group, null, 100, spatial, float.NaN);

            Assert.AreEqual(Vector3.UnitZ, unit.Heading);
        }

        [TestCase(int.MinValue, 1, 6)]
        [TestCase(1, int.MinValue, -6)]
        public void Advance_EqualHeightCapitalWithHighBitRegistration_UsesUnsignedAvoidanceOrder(
            int movingIndex,
            int neighborIndex,
            float expectedHeight
        )
        {
            CombatUnit unit = CreateCapital(movingIndex);
            CombatUnit neighbor = CreateCapital(neighborIndex);
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3.UnitZ);
            group.Formation.Refresh(0);
            group.SetWaypoint(new Vector3(0, 0, 100));
            group.Advance(1000, 1000, 0);
            TacticalCombatSpatialIndex spatial = new(new[] { unit, neighbor });
            TacticalCombatMotion motion = new();

            motion.Advance(unit, group, null, 1000, spatial, 1000);

            Assert.AreEqual(expectedHeight, unit.Position.Y);
        }

        /// <summary>
        /// Creates a moving capital ship with an explicit battle registration key.
        /// </summary>
        /// <param name="index">The battle registration key.</param>
        /// <returns>The complete tactical ship.</returns>
        private static CombatUnit CreateCapital(int index) =>
            new(
                new CapitalShip
                {
                    InstanceID = $"ship-{index}",
                    MaxHullStrength = 100,
                    CurrentHullStrength = 100,
                    SublightSpeed = 100,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                CombatSide.Attacker,
                index
            );

        /// <summary>
        /// Creates a squadron whose stored direction can turn toward a group heading.
        /// </summary>
        /// <returns>The complete tactical squadron.</returns>
        private static CombatUnit CreateFighter() =>
            new(
                new Starfighter
                {
                    InstanceID = "fighter",
                    MaxSquadronSize = 12,
                    CurrentSquadronSize = 12,
                    Agility = 5,
                },
                CombatSide.Attacker,
                1
            );
    }
}
