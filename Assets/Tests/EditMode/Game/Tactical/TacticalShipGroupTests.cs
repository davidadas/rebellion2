using System;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalShipGroupTests
    {
        [Test]
        public void SetBehavior_FormationOrder_ReplacesCurrentBehavior()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);

            group.SetBehavior(TacticalBehavior.LeftHook);
            group.SetBehavior(TacticalBehavior.Hammer);

            Assert.AreEqual(TacticalBehavior.Hammer, group.Behavior);
        }

        [Test]
        public void AddTarget_OpposingActiveUnit_AddsTargetOnce()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender);

            group.AddTarget(target);
            group.AddTarget(target);

            Assert.AreEqual(1, group.Targets.Count);
            Assert.AreSame(target, group.Targets[0]);
        }

        [Test]
        public void AddTarget_FriendlyUnit_ThrowsArgumentException()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Attacker);

            Assert.Throws<ArgumentException>(() => group.AddTarget(target));
        }

        [Test]
        public void RemoveInactiveTargets_DestroyedTarget_RemovesTarget()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender);
            group.AddTarget(target);
            target.Hull = 0;

            group.RemoveInactiveTargets();

            Assert.IsEmpty(group.Targets);
        }

        private static TacticalShipGroup CreateGroup(TacticalBattleSide side)
        {
            return new TacticalShipGroup(side, new[] { CreateUnit(side) });
        }

        private static TacticalUnitState CreateUnit(TacticalBattleSide side)
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = 100,
                MaxShieldStrength = 100,
            };
            return TacticalUnitState.FromCapitalShip(ship, side);
        }
    }
}
