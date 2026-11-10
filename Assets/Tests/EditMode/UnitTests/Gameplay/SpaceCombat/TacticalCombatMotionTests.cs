using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatMotionTests
    {
        [Test]
        public void Advance_UnorderedPosition_UsesLowerBattleBoundary()
        {
            TacticalCombatUnit unit = new(
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
            TacticalCombatUnit unit = CreateFighter();
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
            TacticalCombatUnit unit = CreateFighter();
            TacticalCombatGroup group = new(2, new[] { unit }, Vector3.UnitX);
            TacticalCombatSpatialIndex spatial = new(new[] { unit });
            TacticalCombatMotion motion = new();

            motion.Advance(unit, group, null, 100, spatial, float.NaN);

            Assert.AreEqual(Vector3.UnitZ, unit.Heading);
        }

        /// <summary>
        /// Creates a squadron whose stored direction can turn toward a group heading.
        /// </summary>
        /// <returns>The complete tactical squadron.</returns>
        private static TacticalCombatUnit CreateFighter() =>
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
