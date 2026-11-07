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
    }
}
