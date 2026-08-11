using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public sealed class TacticalDeathStarAttackSystemTests
    {
        [Test]
        public void TryBegin_EligibleFighterGroup_EmitsStartedEvent()
        {
            TacticalUnitState fighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0d);

            bool began = system.TryBegin(group, deathStar);

            Assert.IsTrue(began);
            TacticalCombatEvent combatEvent = system.DrainEvents().Single();
            Assert.AreEqual(TacticalCombatEventKind.DeathStarAttackStarted, combatEvent.Kind);
            Assert.AreSame(fighters, combatEvent.Source);
            Assert.AreSame(deathStar, combatEvent.Target);
        }

        [Test]
        public void TryBegin_AnotherRunActive_DoesNotBeginConcurrentRun()
        {
            TacticalUnitState firstFighters = CreateFighters(12, 100);
            TacticalUnitState secondFighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup firstGroup = CreateAttackGroup(firstFighters, deathStar);
            TacticalShipGroup secondGroup = CreateAttackGroup(secondFighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0d);
            system.TryBegin(firstGroup, deathStar);
            system.DrainEvents();

            bool began = system.TryBegin(secondGroup, deathStar);

            Assert.IsFalse(began);
            Assert.IsEmpty(system.DrainEvents());
        }

        [Test]
        public void Advance_AfterNinthCheckpoint_EmitsAllFailureReports()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(2.4f);

            Assert.AreEqual(
                TacticalDeathStarAttackSystem.ReportCount,
                system
                    .DrainEvents()
                    .Count(combatEvent =>
                        combatEvent.Kind == TacticalCombatEventKind.DeathStarAttackReport
                    )
            );
        }

        [Test]
        public void Advance_NewRun_EmitsFirstReportImmediately()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(0f);

            TacticalCombatEvent combatEvent = system.DrainEvents().Single();
            Assert.AreEqual(TacticalCombatEventKind.DeathStarAttackReport, combatEvent.Kind);
            Assert.AreEqual(0, combatEvent.DeathStarReportIndex);
        }

        [Test]
        public void Advance_BeforeNextCheckpoint_DoesNotEmitAnotherReport()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();
            system.Advance(0f);
            system.DrainEvents();

            system.Advance(TacticalDeathStarAttackSystem.ReportInterval - 0.01f);

            Assert.IsEmpty(system.DrainEvents());
        }

        [Test]
        public void Advance_AtNextCheckpoint_EmitsNextReport()
        {
            TacticalUnitState fighters = CreateFighters(12, 0);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();
            system.Advance(0f);
            system.DrainEvents();

            system.Advance(TacticalDeathStarAttackSystem.ReportInterval);

            TacticalCombatEvent combatEvent = system.DrainEvents().Single();
            Assert.AreEqual(TacticalCombatEventKind.DeathStarAttackReport, combatEvent.Kind);
            Assert.AreEqual(5, combatEvent.DeathStarReportIndex);
        }

        [Test]
        public void Advance_CleanSuccessfulRun_EmitsOriginalReportSequence()
        {
            TacticalUnitState fighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(2.4f);

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 10, 3, 4, 11 },
                system.DrainEvents().Select(combatEvent => combatEvent.DeathStarReportIndex)
            );
        }

        [Test]
        public void Advance_DamagedSuccessfulRun_EmitsDamagedReportSequence()
        {
            TacticalUnitState fighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d, 0.25d, 0d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(2.4f);

            CollectionAssert.AreEqual(
                new[] { 0, 5, 6, 7, 12, 13, 8 },
                system.DrainEvents().Select(combatEvent => combatEvent.DeathStarReportIndex)
            );
        }

        [Test]
        public void Advance_SuccessfulRunAfterThreeSeconds_DestroysDeathStarAndReportsOutcome()
        {
            TacticalUnitState fighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(TacticalDeathStarAttackSystem.RunDuration);

            Assert.IsFalse(deathStar.IsActive);
            CollectionAssert.AreEqual(
                new[]
                {
                    TacticalCombatEventKind.DeathStarAttackSucceeded,
                    TacticalCombatEventKind.UnitDestroyed,
                },
                system
                    .DrainEvents()
                    .Where(combatEvent =>
                        combatEvent.Kind != TacticalCombatEventKind.DeathStarAttackReport
                    )
                    .Select(combatEvent => combatEvent.Kind)
            );
        }

        [Test]
        public void Advance_FailedRunAfterThreeSeconds_PreservesDeathStarAndReportsOutcome()
        {
            TacticalUnitState fighters = CreateFighters(1, 0);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0.99d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();

            system.Advance(TacticalDeathStarAttackSystem.RunDuration);

            Assert.IsTrue(deathStar.IsActive);
            Assert.AreEqual(
                TacticalCombatEventKind.DeathStarAttackFailed,
                system
                    .DrainEvents()
                    .Last(combatEvent =>
                        combatEvent.Kind != TacticalCombatEventKind.DeathStarAttackReport
                    )
                    .Kind
            );
        }

        [Test]
        public void Advance_OrderChangedDuringRun_PreservesDeathStarAndReportsBrokenOff()
        {
            TacticalUnitState fighters = CreateFighters(12, 100);
            TacticalUnitState deathStar = CreateDeathStar();
            TacticalShipGroup group = CreateAttackGroup(fighters, deathStar);
            TacticalDeathStarAttackSystem system = CreateSystem(0d);
            system.TryBegin(group, deathStar);
            system.DrainEvents();
            group.SetBehavior(TacticalBehavior.None);

            system.Advance(0.1f);

            Assert.IsTrue(deathStar.IsActive);
            Assert.AreEqual(
                TacticalCombatEventKind.DeathStarAttackBrokenOff,
                system.DrainEvents().Single().Kind
            );
        }

        private static TacticalDeathStarAttackSystem CreateSystem(params double[] randomValues)
        {
            return new TacticalDeathStarAttackSystem(
                new TacticalDeathStarAttackResolver(new FixedRandomProvider(randomValues)),
                new Dictionary<TacticalBattleSide, float> { [TacticalBattleSide.Attacker] = 1f }
            );
        }

        private static TacticalShipGroup CreateAttackGroup(
            TacticalUnitState fighters,
            TacticalUnitState deathStar
        )
        {
            TacticalUnitState[] battleUnits = { fighters, deathStar };
            TacticalShipGroup group = new TacticalShipGroup(
                TacticalBattleSide.Attacker,
                battleUnits,
                new[] { fighters }
            );
            group.AssignPrimaryTarget(deathStar);
            group.SetBehavior(TacticalBehavior.AttackDeathStar);
            return group;
        }

        private static TacticalUnitState CreateFighters(int squadronSize, int attackStrength)
        {
            return TacticalUnitState.FromFighters(
                new Starfighter
                {
                    CurrentSquadronSize = squadronSize,
                    MaxSquadronSize = squadronSize,
                    LaserCannon = attackStrength,
                    Agility = 8,
                    SublightSpeed = 100,
                },
                TacticalBattleSide.Attacker
            );
        }

        private static TacticalUnitState CreateDeathStar()
        {
            return TacticalUnitState.FromCapitalShip(
                new CapitalShip
                {
                    CurrentHullStrength = 100,
                    MaxHullStrength = 100,
                    MaxShieldStrength = 100,
                    IsDeathStar = true,
                },
                TacticalBattleSide.Defender
            );
        }
    }
}
