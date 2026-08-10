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
        public void SetFormation_ValidFormation_ReplacesCurrentFormation()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);

            group.SetFormation(TacticalFormation.Surround);

            Assert.AreEqual(TacticalFormation.Surround, group.Formation);
        }

        [Test]
        public void AddTarget_OpposingActiveUnit_AddsTargetOnce()
        {
            TacticalUnitState unit = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender);
            TacticalShipGroup group = CreateGroup(unit, target);

            group.AddTarget(target);
            group.AddTarget(target);

            Assert.AreEqual(1, group.Targets.Count);
            Assert.AreSame(target, group.Targets[0]);
        }

        [Test]
        public void AddTarget_FriendlyUnit_ThrowsArgumentException()
        {
            TacticalUnitState unit = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Attacker);
            TacticalShipGroup group = CreateGroup(unit, target);

            Assert.Throws<ArgumentException>(() => group.AddTarget(target));
        }

        [Test]
        public void RemoveInactiveTargets_DestroyedTarget_RemovesTarget()
        {
            TacticalUnitState unit = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState target = CreateUnit(TacticalBattleSide.Defender);
            TacticalShipGroup group = CreateGroup(unit, target);
            group.AddTarget(target);
            target.Hull = 0;

            group.RemoveInactiveTargets();

            Assert.IsEmpty(group.Targets);
        }

        [Test]
        public void AddUnit_AlliedBattleUnit_AddsUnitOnce()
        {
            TacticalUnitState first = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState second = CreateUnit(TacticalBattleSide.Attacker);
            TacticalShipGroup group = CreateGroup(first, second);

            group.AddUnit(second);
            group.AddUnit(second);

            Assert.AreEqual(2, group.Units.Count);
            Assert.AreSame(second, group.Units[1]);
        }

        [Test]
        public void AddUnit_UnitOutsideBattle_ThrowsArgumentException()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);
            TacticalUnitState outsider = CreateUnit(TacticalBattleSide.Attacker);

            Assert.Throws<ArgumentException>(() => group.AddUnit(outsider));
        }

        [Test]
        public void ReplaceTargets_OrderedTargets_ReplacesPriorityList()
        {
            TacticalUnitState unit = CreateUnit(TacticalBattleSide.Attacker);
            TacticalUnitState first = CreateUnit(TacticalBattleSide.Defender);
            TacticalUnitState second = CreateUnit(TacticalBattleSide.Defender);
            TacticalShipGroup group = CreateGroup(unit, first, second);
            group.AddTarget(first);

            group.ReplaceTargets(new[] { second, first });

            CollectionAssert.AreEqual(new[] { second, first }, group.Targets);
        }

        [Test]
        public void ReplaceNavigationPoints_OrderedRoute_ReplacesNavigationList()
        {
            TacticalShipGroup group = CreateGroup(TacticalBattleSide.Attacker);
            TacticalNavPoint discarded = new TacticalNavPoint(1f, 2f, 3f);
            TacticalNavPoint first = new TacticalNavPoint(4f, 5f, 6f);
            TacticalNavPoint second = new TacticalNavPoint(7f, 8f, 9f);
            group.AddNavigationPoint(discarded);

            group.ReplaceNavigationPoints(new[] { first, second });

            CollectionAssert.AreEqual(new[] { first, second }, group.NavigationPoints);
        }

        private static TacticalShipGroup CreateGroup(TacticalBattleSide side)
        {
            TacticalUnitState unit = CreateUnit(side);
            return CreateGroup(unit);
        }

        private static TacticalShipGroup CreateGroup(params TacticalUnitState[] battleUnits)
        {
            return new TacticalShipGroup(
                battleUnits[0].Side,
                battleUnits,
                new[] { battleUnits[0] }
            );
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
