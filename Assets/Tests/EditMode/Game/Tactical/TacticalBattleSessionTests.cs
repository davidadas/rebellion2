using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalBattleSessionTests
    {
        [Test]
        public void Create_NullEncounter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CreateTacticalSession(null));
        }

        [Test]
        public void Create_MissingFleet_ThrowsArgumentException()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(),
            };

            Assert.Throws<ArgumentException>(() => CreateTacticalSession(encounter));
        }

        [Test]
        public void Create_OperationalShipsAndFighters_CreatesTacticalUnitsForBothSides()
        {
            Starfighter attackingFighters = CreateFighters(8, 12);
            CapitalShip attackingShip = CreateShip(600, 250, attackingFighters);
            CapitalShip defendingShip = CreateShip(450, 175);
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(attackingShip),
                DefenderFleet = CreateFleet(defendingShip),
            };

            TacticalBattleSession session = CreateTacticalSession(encounter);

            Assert.AreEqual(3, session.Units.Count);
            Assert.AreEqual(
                2,
                session.Units.Count(unit => unit.Side == TacticalBattleSide.Attacker)
            );
            Assert.AreEqual(
                1,
                session.Units.Count(unit => unit.Side == TacticalBattleSide.Defender)
            );
            TacticalUnitState fighterState = session.Units.Single(unit =>
                unit.Kind == TacticalUnitKind.Fighters
            );
            Assert.AreEqual(8, fighterState.Hull);
            Assert.AreEqual(96, fighterState.Shields);
        }

        [Test]
        public void Create_UnavailableUnits_ExcludesThemFromBattle()
        {
            CapitalShip destroyedShip = CreateShip(0, 100);
            CapitalShip incompleteShip = CreateShip(100, 100);
            incompleteShip.ManufacturingStatus = ManufacturingStatus.Building;
            Starfighter depletedFighters = CreateFighters(0, 10);
            CapitalShip activeShip = CreateShip(100, 100, depletedFighters);
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(destroyedShip, incompleteShip, activeShip),
                DefenderFleet = CreateFleet(CreateShip(100, 100)),
            };

            TacticalBattleSession session = CreateTacticalSession(encounter);

            Assert.AreEqual(2, session.Units.Count);
            Assert.IsTrue(session.Units.All(unit => unit.Kind == TacticalUnitKind.CapitalShip));
        }

        [Test]
        public void Create_DeployedPlanetaryFighters_IncludesOwningSides()
        {
            Planet planet = new Planet();
            Starfighter attackingFighters = CreateFighters(7, 10);
            attackingFighters.OwnerInstanceID = "attacker";
            Starfighter defendingFighters = CreateFighters(5, 12);
            defendingFighters.OwnerInstanceID = "defender";
            planet.Starfighters.Add(attackingFighters);
            planet.Starfighters.Add(defendingFighters);
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
                AttackerOwnerInstanceID = "attacker",
                DefenderOwnerInstanceID = "defender",
                Planet = planet,
            };

            TacticalBattleSession session = CreateTacticalSession(encounter);

            Assert.AreEqual(4, session.Units.Count);
            Assert.AreEqual(
                TacticalBattleSide.Attacker,
                session.Units.Single(unit => unit.Unit == attackingFighters).Side
            );
            Assert.AreEqual(
                TacticalBattleSide.Defender,
                session.Units.Single(unit => unit.Unit == defendingFighters).Side
            );
            Assert.IsTrue(session.Units.Single(unit => unit.Unit == attackingFighters).IsDeployed);
            Assert.IsTrue(session.Units.Single(unit => unit.Unit == defendingFighters).IsDeployed);
        }

        [Test]
        public void Create_CarrierFightersWithoutHyperdrive_HoldsFightersForLaunch()
        {
            Starfighter fighters = CreateFighters(12, 10);
            fighters.Hyperdrive = 0;
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250, fighters)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };

            TacticalBattleSession session = CreateArrivingSession(
                encounter.AttackerFleet.CapitalShips.Single(),
                encounter.DefenderFleet.CapitalShips.Single()
            );

            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            Assert.IsFalse(fighterUnit.IsDeployed);
            Assert.IsFalse(fighterUnit.IsActive);
        }

        [Test]
        public void Create_CarrierFightersWithHyperdrive_DeploysFightersImmediately()
        {
            Starfighter fighters = CreateFighters(12, 10);
            fighters.Hyperdrive = 100;
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250, fighters)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };

            TacticalBattleSession session = TacticalBattleSession.Create(
                encounter,
                new FixedRandomProvider(new[] { 0d })
            );

            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            Assert.IsTrue(fighterUnit.IsDeployed);
            Assert.IsTrue(fighterUnit.IsActive);
        }

        [Test]
        public void Create_OperationalFleets_StartsInArrivalPhaseWithoutMovingSimulationPositions()
        {
            TacticalBattleSession session = CreateArrivingSession();
            TacticalUnitState attacker = session.Units.First(unit =>
                unit.Side == TacticalBattleSide.Attacker
            );
            Vector3 simulationPosition = attacker.Position;

            Vector3 presentationPosition = session.GetPresentationPosition(attacker);

            Assert.AreEqual(TacticalBattlePhase.Arrival, session.Phase);
            Assert.AreEqual(simulationPosition, attacker.Position);
            Assert.Less(presentationPosition.Z, simulationPosition.Z);
        }

        [Test]
        public void Advance_ArrivalDuration_EntersEngagementAtSimulationPosition()
        {
            TacticalBattleSession session = CreateArrivingSession();
            TacticalUnitState attacker = session.Units.First(unit =>
                unit.Side == TacticalBattleSide.Attacker
            );

            session.Advance(1f);

            Assert.AreEqual(TacticalBattlePhase.Engagement, session.Phase);
            Assert.AreEqual(attacker.Position, session.GetPresentationPosition(attacker));
        }

        [Test]
        public void Advance_ArrivalPhase_DoesNotRechargeOrResolveCombat()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            attackingShip.ShieldRechargeRate = 20;
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateArrivingSession(attackingShip, defendingShip);
            TacticalUnitState attackingUnit = session.Units.Single(unit =>
                unit.Unit == attackingShip
            );
            TacticalUnitState defendingUnit = session.Units.Single(unit =>
                unit.Unit == defendingShip
            );
            attackingUnit.ApplyDamage(100);

            session.Advance(0.5f);

            Assert.AreEqual(150, attackingUnit.Shields);
            Assert.AreEqual(100, defendingUnit.Hull);
        }

        [Test]
        public void Create_CapitalShip_CreatesWeaponBatteriesForEveryPrimaryWeaponType()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                10,
                20,
                30,
                40,
                50,
            };
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(attackingShip),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };

            TacticalBattleSession session = CreateTacticalSession(encounter);

            TacticalUnitState state = session.Units.Single(unit => unit.Unit == attackingShip);
            Assert.AreEqual(3, state.WeaponBatteries.Count);
            Assert.AreEqual(
                40,
                state
                    .WeaponBatteries.Single(battery =>
                        battery.WeaponType == TacticalWeaponType.Turbolaser
                    )
                    .GetCount(TacticalWeaponArc.Starboard)
            );
        }

        [Test]
        public void GetTaskForces_TenCapitalShips_PartitionsShipsAcrossThreeSlots()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(
                    Enumerable.Range(0, 10).Select(_ => CreateShip(600, 250)).ToArray()
                ),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);

            System.Collections.Generic.IReadOnlyList<TacticalShipGroup> groups =
                session.GetTaskForces(TacticalBattleSide.Attacker);

            Assert.AreEqual(3, groups.Count);
            CollectionAssert.AreEqual(new[] { 3, 3, 4 }, groups.Select(group => group.Units.Count));
            Assert.IsTrue(groups.All(group => group.Side == TacticalBattleSide.Attacker));
        }

        [Test]
        public void GetFighterGroups_MultipleSquadronsOfSameType_GroupsByFighterType()
        {
            Starfighter firstXWing = CreateFighters(12, 10, "CSAL001");
            Starfighter secondXWing = CreateFighters(12, 10, "CSAL001");
            Starfighter yWing = CreateFighters(12, 10, "CSAL002");
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250, firstXWing, yWing, secondXWing)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);

            System.Collections.Generic.IReadOnlyList<TacticalShipGroup> groups =
                session.GetFighterGroups(TacticalBattleSide.Attacker);

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(
                new[] { "CSAL001", "CSAL001" },
                groups[0].Units.Select(unit => unit.Unit.TypeID)
            );
            CollectionAssert.AreEqual(
                new[] { "CSAL002" },
                groups[1].Units.Select(unit => unit.Unit.TypeID)
            );
        }

        [Test]
        public void ConfigurePlayerControl_UnconfiguredSession_AutomatesOnlyOpposingSide()
        {
            TacticalBattleSession session = CreateSession();

            session.ConfigurePlayerControl(TacticalBattleSide.Attacker);

            Assert.IsFalse(session.IsAutomated(TacticalBattleSide.Attacker));
            Assert.IsTrue(session.IsAutomated(TacticalBattleSide.Defender));
        }

        [Test]
        public void ConfigurePlayerControl_ExistingControlSelection_PreservesControlSelection()
        {
            TacticalBattleSession session = CreateSession();
            session.ConfigurePlayerControl(TacticalBattleSide.Attacker);
            session.SetAutomated(TacticalBattleSide.Attacker, true);

            session.ConfigurePlayerControl(TacticalBattleSide.Attacker);

            Assert.IsTrue(session.IsAutomated(TacticalBattleSide.Attacker));
        }

        [Test]
        public void Advance_AutomatedSide_AssignsRankedOpposingTargets()
        {
            CapitalShip weakerTarget = CreateShip(100, 0);
            weakerTarget.TypeID = "weaker";
            CapitalShip strongerTarget = CreateShip(500, 200);
            strongerTarget.TypeID = "stronger";
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250)),
                DefenderFleet = CreateFleet(weakerTarget, strongerTarget),
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);
            session.SetAutomated(TacticalBattleSide.Attacker, true);

            session.Advance(1f);
            session.Advance(0.1f);

            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            CollectionAssert.AreEqual(
                new[] { strongerTarget, weakerTarget },
                group.Targets.Select(target => target.Unit)
            );
            Assert.AreEqual(TacticalBehavior.PrimaryTarget, group.Behavior);
        }

        [Test]
        public void Advance_AutomatedDeathStar_FiresAtRankedOpposingTarget()
        {
            CapitalShip weakerTarget = CreateShip(100, 0);
            CapitalShip strongerTarget = CreateShip(500, 200);
            CapitalShip deathStar = CreateShip(1000, 1000);
            deathStar.IsDeathStar = true;
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(weakerTarget, strongerTarget),
                DefenderFleet = CreateFleet(deathStar),
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);
            session.ConfigurePlayerControl(TacticalBattleSide.Attacker);
            TacticalUnitState strongerTargetState = session.Units.Single(unit =>
                unit.Unit == strongerTarget
            );
            TacticalUnitState deathStarState = session.Units.Single(unit => unit.Unit == deathStar);

            session.Advance(0.1f);

            Assert.AreEqual(0, strongerTargetState.Hull);
            Assert.AreEqual(0f, session.GetSuperlaserCharge(deathStarState));
            Assert.IsTrue(
                session
                    .DrainEvents()
                    .Any(combatEvent =>
                        combatEvent.Kind == TacticalCombatEventKind.SuperlaserFired
                        && combatEvent.Target.Unit == strongerTarget
                    )
            );
        }

        [Test]
        public void TryFireSuperlaser_CarrierIsDestroyed_DestroysHeldFighters()
        {
            Starfighter fighters = CreateFighters(12, 0);
            fighters.Hyperdrive = 0;
            CapitalShip carrier = CreateShip(600, 0, fighters);
            CapitalShip deathStar = CreateShip(1000, 1000);
            deathStar.IsDeathStar = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(carrier),
                    DefenderFleet = CreateFleet(deathStar),
                }
            );
            TacticalUnitState carrierState = session.Units.Single(unit => unit.Unit == carrier);
            TacticalUnitState fighterState = session.Units.Single(unit => unit.Unit == fighters);
            TacticalUnitState deathStarState = session.Units.Single(unit => unit.Unit == deathStar);

            bool fired = session.TryFireSuperlaser(deathStarState, carrierState);

            Assert.IsTrue(fired);
            Assert.AreEqual(0, carrierState.Hull);
            Assert.AreEqual(0, fighterState.Hull);
            Assert.IsTrue(session.IsComplete);
        }

        [Test]
        public void SetAutomated_DisabledSide_PreservesExistingOrders()
        {
            TacticalBattleSession session = CreateSession();
            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            group.SetBehavior(TacticalBehavior.Hold);

            session.SetAutomated(TacticalBattleSide.Attacker, false);
            session.Advance(1f);
            session.Advance(1f);

            Assert.AreEqual(TacticalBehavior.Hold, group.Behavior);
            Assert.IsEmpty(group.Targets);
        }

        [Test]
        public void Pause_MultiplePauseHolds_RequiresMatchingResumeCalls()
        {
            TacticalBattleSession session = CreateSession();

            session.Pause();
            session.Pause();
            session.Resume();

            Assert.IsTrue(session.IsPaused);

            session.Resume();

            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void Resume_RunningSession_RemainsRunning()
        {
            TacticalBattleSession session = CreateSession();

            session.Resume();

            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void ResolveImmediately_PausedWithdrawingSide_CompletesExistingBattle()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            attackingShip.SublightSpeed = 10;
            TacticalBattleSession session = CreateSession(attackingShip);
            session.OrderWithdrawal(TacticalBattleSide.Attacker);
            session.Pause();

            session.ResolveImmediately();

            Assert.IsTrue(session.IsComplete);
            Assert.IsFalse(session.IsPaused);
            Assert.AreEqual(
                SpaceCombatSideOutcome.Withdrawn,
                session.BuildResult().AttackerOutcome
            );
        }

        [Test]
        public void OrderWithdrawal_ActiveSide_AssignsEveryCommandGroup()
        {
            Starfighter fighters = CreateFighters(12, 10);
            TacticalBattleSession session = CreateSession(CreateShip(600, 250, fighters));

            session.OrderWithdrawal(TacticalBattleSide.Attacker);

            Assert.IsTrue(
                session
                    .Groups.Where(group => group.Side == TacticalBattleSide.Attacker)
                    .All(group => group.Behavior == TacticalBehavior.Withdraw)
            );
            Assert.IsTrue(
                session
                    .Groups.Where(group => group.Side == TacticalBattleSide.Defender)
                    .All(group => group.Behavior != TacticalBehavior.Withdraw)
            );
        }

        [Test]
        public void OrderWithdrawal_ActiveOpposingGravityWell_ReturnsFalse()
        {
            CapitalShip interdictor = CreateShip(450, 175);
            interdictor.HasGravityWell = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(interdictor),
                }
            );

            bool ordered = session.OrderWithdrawal(TacticalBattleSide.Attacker);

            Assert.IsFalse(ordered);
        }

        [Test]
        public void OrderWithdrawal_ActiveOpposingGravityWell_PreservesCurrentOrders()
        {
            CapitalShip interdictor = CreateShip(450, 175);
            interdictor.HasGravityWell = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(interdictor),
                }
            );

            session.OrderWithdrawal(TacticalBattleSide.Attacker);

            Assert.IsTrue(
                session
                    .Groups.Where(group => group.Side == TacticalBattleSide.Attacker)
                    .All(group => group.Behavior != TacticalBehavior.Withdraw)
            );
        }

        [Test]
        public void IsWithdrawalBlocked_ActiveOpposingGravityWell_ReturnsTrue()
        {
            CapitalShip interdictor = CreateShip(450, 175);
            interdictor.HasGravityWell = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(interdictor),
                }
            );

            bool blocked = session.IsWithdrawalBlocked(TacticalBattleSide.Attacker);

            Assert.IsTrue(blocked);
        }

        [Test]
        public void IsWithdrawalBlocked_DestroyedOpposingGravityWell_ReturnsFalse()
        {
            CapitalShip interdictor = CreateShip(450, 175);
            interdictor.HasGravityWell = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(interdictor),
                }
            );
            session.Units.Single(unit => unit.Unit == interdictor).Hull = 0;

            bool blocked = session.IsWithdrawalBlocked(TacticalBattleSide.Attacker);

            Assert.IsFalse(blocked);
        }

        [Test]
        public void OrderWithdrawal_UndefinedSide_ThrowsArgumentOutOfRangeException()
        {
            TacticalBattleSession session = CreateSession();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.OrderWithdrawal((TacticalBattleSide)99)
            );
        }

        [Test]
        public void Advance_PausedSession_DoesNotAdvanceUnitRecharge()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            attackingShip.ShieldRechargeRate = 20;
            TacticalBattleSession session = CreateSession(attackingShip);
            TacticalUnitState attackingUnit = session.Units.Single(unit =>
                unit.Unit == attackingShip
            );
            attackingUnit.ApplyDamage(100);
            session.Pause();

            session.Advance(1f);

            Assert.AreEqual(150, attackingUnit.Shields);
        }

        [Test]
        public void Advance_RunningSession_AdvancesUnitRecharge()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            attackingShip.ShieldRechargeRate = 20;
            TacticalBattleSession session = CreateSession(attackingShip);
            TacticalUnitState attackingUnit = session.Units.Single(unit =>
                unit.Unit == attackingShip
            );
            attackingUnit.ApplyDamage(100);

            session.Advance(1f);

            Assert.AreEqual(170, attackingUnit.Shields);
        }

        [Test]
        public void Advance_PrimaryTargetInWeaponRange_FiresStrongestEligibleArc()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );

            session.Advance(0.1f);

            Assert.AreEqual(70, session.Units.Single(unit => unit.Unit == defendingShip).Hull);
        }

        [Test]
        public void Advance_HoldBehavior_FiresWithoutMoving()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.Maneuverability = 10;
            attackingShip.SublightSpeed = 10;
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );
            TacticalUnitState attackingUnit = session.Units.Single(unit =>
                unit.Unit == attackingShip
            );
            Vector3 initialPosition = attackingUnit.Position;
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Hold);

            session.Advance(0.1f);

            Assert.AreEqual(initialPosition, attackingUnit.Position);
            Assert.AreEqual(70, session.Units.Single(unit => unit.Unit == defendingShip).Hull);
        }

        [Test]
        public void Advance_PrimaryTargetWithoutAssignment_TargetsNearestOpposingUnit()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip firstDefendingShip = CreateShip(100, 0);
            CapitalShip lastDefendingShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(firstDefendingShip, lastDefendingShip),
                }
            );
            session.Units.Single(unit => unit.Unit == firstDefendingShip).Position = new Vector3(
                0f,
                0f,
                10f
            );
            session.Units.Single(unit => unit.Unit == lastDefendingShip).Position = new Vector3(
                0f,
                0f,
                40f
            );

            session.Advance(0.1f);

            Assert.AreEqual(70, session.Units.Single(unit => unit.Unit == firstDefendingShip).Hull);
            Assert.AreEqual(100, session.Units.Single(unit => unit.Unit == lastDefendingShip).Hull);
        }

        [Test]
        public void Advance_TargetWithinTractorRange_ReducesTargetMovement()
        {
            CapitalShip tractorShip = CreateShip(600, 0);
            tractorShip.TractorBeamPower = 6;
            tractorShip.TractorBeamnRange = 200;
            CapitalShip movingShip = CreateShip(600, 0);
            movingShip.Maneuverability = 10;
            movingShip.SublightSpeed = 10;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(tractorShip),
                    DefenderFleet = CreateFleet(movingShip),
                }
            );
            TacticalUnitState movingUnit = session.Units.Single(unit => unit.Unit == movingShip);
            Vector3 initialPosition = movingUnit.Position;

            session.Advance(0.5f);

            Assert.AreEqual(initialPosition - Vector3.UnitZ * 2f, movingUnit.Position);
            Assert.IsTrue(
                session
                    .DrainEvents()
                    .Any(combatEvent =>
                        combatEvent.Kind == TacticalCombatEventKind.TractorLock
                        && combatEvent.Source.Unit == tractorShip
                        && combatEvent.Target.Unit == movingShip
                    )
            );
        }

        [Test]
        public void Advance_OrderedTargetAssignment_AttacksFirstEligibleTarget()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip firstDefendingShip = CreateShip(100, 0);
            CapitalShip secondDefendingShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(firstDefendingShip, secondDefendingShip),
                }
            );
            TacticalUnitState firstTarget = session.Units.Single(unit =>
                unit.Unit == firstDefendingShip
            );
            TacticalUnitState secondTarget = session.Units.Single(unit =>
                unit.Unit == secondDefendingShip
            );
            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            group.ReplaceTargets(new[] { secondTarget, firstTarget });

            session.Advance(0.1f);

            Assert.AreEqual(100, firstTarget.Hull);
            Assert.AreEqual(70, secondTarget.Hull);
        }

        [Test]
        public void Advance_LeftHookBehavior_ApproachesRelativeToTargetBearing()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.Maneuverability = 10;
            attackingShip.SublightSpeed = 10;
            CapitalShip defendingShip = CreateShip(600, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );
            TacticalUnitState attackingUnit = session.Units.Single(unit =>
                unit.Unit == attackingShip
            );
            TacticalUnitState defendingUnit = session.Units.Single(unit =>
                unit.Unit == defendingShip
            );
            attackingUnit.Position = Vector3.Zero;
            attackingUnit.Forward = Vector3.UnitX;
            defendingUnit.Position = new Vector3(100f, 0f, 0f);
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.LeftHook);

            session.Advance(1f);

            Assert.Greater(attackingUnit.Position.Z, 0f);
        }

        [Test]
        public void Advance_SurroundFormation_DistributesMembersAcrossMultipleAxes()
        {
            CapitalShip firstShip = CreateShip(600, 0);
            firstShip.Maneuverability = 10;
            firstShip.SublightSpeed = 10;
            CapitalShip secondShip = CreateShip(600, 0);
            secondShip.Maneuverability = 10;
            secondShip.SublightSpeed = 10;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(firstShip, secondShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            group.SetFormation(TacticalFormation.Surround);

            session.Advance(1f);

            TacticalUnitState firstUnit = session.Units.Single(unit => unit.Unit == firstShip);
            TacticalUnitState secondUnit = session.Units.Single(unit => unit.Unit == secondShip);
            Assert.Less(firstUnit.Position.Y, 0f);
            Assert.AreEqual(0f, secondUnit.Position.Y, 0.001f);
        }

        [Test]
        public void Advance_MovementWouldCollide_UsesVerticalClearanceLane()
        {
            CapitalShip movingShip = CreateShip(600, 0);
            movingShip.Maneuverability = 10;
            movingShip.SublightSpeed = 10;
            CapitalShip stationaryShip = CreateShip(600, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(movingShip, stationaryShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalUnitState movingUnit = session.Units.Single(unit => unit.Unit == movingShip);
            TacticalUnitState stationaryUnit = session.Units.Single(unit =>
                unit.Unit == stationaryShip
            );
            movingUnit.Position = Vector3.Zero;
            movingUnit.Forward = Vector3.UnitZ;
            movingUnit.SetCollisionExtents(2.5f, 1f);
            stationaryUnit.Position = new Vector3(0f, 0f, 5f);
            stationaryUnit.SetCollisionExtents(2.5f, 1f);
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .ReplaceNavigationPoints(new[] { new TacticalNavPoint(0f, 0f, 10f) });

            session.Advance(0.5f);

            Assert.AreEqual(new Vector3(0f, 2f, 5f), movingUnit.Position);
        }

        [Test]
        public void Advance_AttackFightersBehavior_TargetsOnlyOpposingFighters()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 6, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(100, 0, CreateFighters(12, 0));
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );
            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            group.SetBehavior(TacticalBehavior.AttackFighters);

            session.Advance(0.1f);

            Assert.AreEqual(
                6,
                session.Units.Single(unit => unit.Kind == TacticalUnitKind.Fighters).Hull
            );
            Assert.AreEqual(100, session.Units.Single(unit => unit.Unit == defendingShip).Hull);
        }

        [Test]
        public void Advance_AttackDeathStarBehavior_UsesDedicatedAttackRun()
        {
            Starfighter attackingFighters = CreateFighters(12, 0);
            attackingFighters.LaserCannon = 10;
            attackingFighters.SublightSpeed = 100;
            CapitalShip deathStar = CreateShip(100, 0);
            deathStar.IsDeathStar = true;
            CapitalShip ordinaryShip = CreateShip(100, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(CreateShip(600, 0, attackingFighters)),
                    DefenderFleet = CreateFleet(deathStar, ordinaryShip),
                }
            );
            session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.AttackDeathStar);
            session.Units.Single(unit => unit.Unit == attackingFighters).Position = session
                .Units.Single(unit => unit.Unit == deathStar)
                .Position;

            session.Advance(0.1f);

            Assert.AreEqual(0, session.Units.Single(unit => unit.Unit == deathStar).Hull);
            Assert.AreEqual(100, session.Units.Single(unit => unit.Unit == ordinaryShip).Hull);
            Assert.AreEqual(12, session.Units.Single(unit => unit.Unit == attackingFighters).Hull);
        }

        [Test]
        public void Advance_AttackDeathStarBehavior_DoesNotCommandCapitalShips()
        {
            CapitalShip attackingShip = CreateShip(100, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 6, 0, 0, 0, 200 };
            CapitalShip deathStar = CreateShip(100, 0);
            deathStar.IsDeathStar = true;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(deathStar),
                }
            );
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.AttackDeathStar);

            session.Advance(0.1f);

            Assert.AreEqual(100, session.Units.Single(unit => unit.Unit == deathStar).Hull);
        }

        [Test]
        public void Advance_AttackDeathStarBehavior_UsesAssignedCommanderCombatRating()
        {
            Starfighter attackingFighters = CreateFighters(12, 0);
            CapitalShip attackingShip = CreateShip(600, 0, attackingFighters);
            Officer commander = new Officer { CurrentRank = OfficerRank.Commander };
            commander.SetBaseRating(OfficerRating.Combat, 100);
            attackingShip.Officers.Add(commander);
            CapitalShip deathStar = CreateShip(100, 0);
            deathStar.IsDeathStar = true;
            TacticalBattleSession session = TacticalBattleSession.Create(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(deathStar),
                },
                new FixedRandomProvider(new[] { 0.02d })
            );
            session.Advance(1f);
            session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.AttackDeathStar);
            session.Units.Single(unit => unit.Unit == attackingFighters).Position = session
                .Units.Single(unit => unit.Unit == deathStar)
                .Position;

            session.Advance(0.1f);

            Assert.AreEqual(0, session.Units.Single(unit => unit.Unit == deathStar).Hull);
        }

        [Test]
        public void Advance_RecoverBehavior_ReturnsFightersToTheirDeployingCapitalShip()
        {
            Starfighter fighters = CreateFighters(12, 0);
            fighters.Agility = 10;
            fighters.SublightSpeed = 10;
            CapitalShip deployingShip = CreateShip(600, 0, fighters);
            CapitalShip nearbyShip = CreateShip(600, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(deployingShip, nearbyShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalUnitState deployingUnit = session.Units.Single(unit =>
                unit.Unit == deployingShip
            );
            TacticalUnitState nearbyUnit = session.Units.Single(unit => unit.Unit == nearbyShip);
            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            deployingUnit.Position = new Vector3(100f, 0f, 0f);
            nearbyUnit.Position = new Vector3(-1f, 0f, 0f);
            fighterUnit.Position = Vector3.Zero;
            session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Recover);

            session.Advance(1f);

            Assert.Greater(fighterUnit.Position.X, 0f);
        }

        [Test]
        public void Advance_RecoverBehaviorWithDestroyedCarrier_ReturnsFightersToFormationMarker()
        {
            Starfighter fighters = CreateFighters(12, 0);
            fighters.Agility = 10;
            fighters.SublightSpeed = 10;
            CapitalShip deployingShip = CreateShip(600, 0, fighters);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(deployingShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            TacticalUnitState carrierUnit = session.Units.Single(unit =>
                unit.Unit == deployingShip
            );
            TacticalShipGroup group = session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single();
            Vector3 marker = group.MarkerPosition;
            fighterUnit.Position = marker + Vector3.UnitZ * 20f;
            fighterUnit.Forward = -Vector3.UnitZ;
            carrierUnit.Hull = 0;
            group.SetBehavior(TacticalBehavior.Recover);

            session.Advance(0.1f);

            Assert.Less(Vector3.Distance(fighterUnit.Position, marker), 20f);
            Assert.IsTrue(fighterUnit.IsActive);
        }

        [Test]
        public void Advance_RecoverBehavior_StoresFightersAtDeployingCapitalShip()
        {
            Starfighter fighters = CreateFighters(12, 0);
            CapitalShip deployingShip = CreateShip(600, 0, fighters);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(deployingShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalUnitState deployingUnit = session.Units.Single(unit =>
                unit.Unit == deployingShip
            );
            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            fighterUnit.Position = deployingUnit.Position;
            session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Recover);

            session.Advance(0.1f);

            Assert.IsTrue(fighterUnit.HasWithdrawn);
        }

        [Test]
        public void DrainEvents_RecoveredFighters_ReturnsRecoveryEvent()
        {
            Starfighter fighters = CreateFighters(12, 0);
            CapitalShip deployingShip = CreateShip(600, 0, fighters);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(deployingShip),
                    DefenderFleet = CreateFleet(CreateShip(600, 0)),
                }
            );
            TacticalUnitState deployingUnit = session.Units.Single(unit =>
                unit.Unit == deployingShip
            );
            TacticalUnitState fighterUnit = session.Units.Single(unit => unit.Unit == fighters);
            fighterUnit.Position = deployingUnit.Position;
            session
                .GetFighterGroups(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Recover);

            session.Advance(0.1f);
            TacticalCombatEvent recovery = session
                .DrainEvents()
                .Single(combatEvent =>
                    combatEvent.Kind == TacticalCombatEventKind.FightersRecovered
                );

            Assert.AreSame(fighterUnit, recovery.Source);
        }

        [Test]
        public void Advance_OpposingLethalAttacks_ResolvesBothAttacksBeforeCompletingBattle()
        {
            CapitalShip attackingShip = CreateShip(30, 0);
            CapitalShip defendingShip = CreateShip(30, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            defendingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );

            session.Advance(0.1f);
            SpaceCombatResult result = session.BuildResult();

            Assert.AreEqual(CombatSide.Draw, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
        }

        [Test]
        public void DrainEvents_LethalWeaponAttack_ReturnsImpactBeforeDestruction()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(30, 0);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );

            session.Advance(0.1f);
            IReadOnlyList<TacticalCombatEvent> events = session.DrainEvents();

            Assert.AreEqual(TacticalCombatEventKind.WeaponImpact, events[0].Kind);
            Assert.AreEqual(TacticalCombatEventKind.UnitDestroyed, events[1].Kind);
            Assert.AreSame(attackingShip, events[0].Source.Unit);
            Assert.AreSame(defendingShip, events[0].Target.Unit);
            Assert.AreEqual(TacticalWeaponType.Turbolaser, events[0].WeaponType);
            Assert.AreEqual(TacticalImpactState.Destroyed, events[0].ImpactState);
            Assert.IsTrue(events[0].PenetratedShields);
        }

        [Test]
        public void DrainEvents_AttackExactlyExhaustsShields_ReturnsContainedImpact()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            CapitalShip defendingShip = CreateShip(600, 30);
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(defendingShip),
                }
            );

            session.Advance(0.1f);
            TacticalCombatEvent impact = session
                .DrainEvents()
                .Single(combatEvent => combatEvent.Kind == TacticalCombatEventKind.WeaponImpact);

            Assert.IsFalse(impact.PenetratedShields);
            Assert.AreEqual(TacticalImpactState.Shield, impact.ImpactState);
        }

        [Test]
        public void DrainEvents_PreviouslyDrainedEvents_ReturnsEmptyCollection()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 30, 0, 0, 0, 200 };
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(CreateShip(30, 0)),
                }
            );
            session.Advance(0.1f);
            session.DrainEvents();

            IReadOnlyList<TacticalCombatEvent> events = session.DrainEvents();

            Assert.IsEmpty(events);
        }

        [Test]
        public void Advance_WithdrawBehavior_WithdrawsGroupAndCompletesSideOutcome()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.SublightSpeed = 100;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(CreateShip(450, 0)),
                }
            );
            TacticalShipGroup group = session.GetTaskForces(TacticalBattleSide.Attacker).Single();
            group.SetBehavior(TacticalBehavior.Withdraw);

            session.Advance(2f);
            SpaceCombatResult result = session.BuildResult();

            Assert.AreEqual(SpaceCombatSideOutcome.Withdrawn, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.DefenderOutcome);
            Assert.AreEqual(CombatSide.Defender, result.Winner);
        }

        [Test]
        public void Advance_WithdrawBehavior_UsesQuadraticExitCurve()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.SublightSpeed = 1;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(CreateShip(450, 0)),
                }
            );
            TacticalUnitState unit = session.Units.Single(candidate =>
                candidate.Unit == attackingShip
            );
            Vector3 origin = unit.Position;
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Withdraw);

            session.Advance(0.5f);

            Assert.AreEqual(origin - Vector3.UnitZ * 10f, unit.Position);
            Assert.AreEqual(-Vector3.UnitZ, unit.Forward);
            Assert.IsTrue(unit.IsWithdrawing);
            Assert.IsFalse(unit.HasWithdrawn);
        }

        [Test]
        public void Advance_WithdrawBehaviorFullyTractorLocked_DoesNotBeginWithdrawal()
        {
            CapitalShip withdrawingShip = CreateShip(600, 0);
            withdrawingShip.SublightSpeed = 5;
            CapitalShip tractorShip = CreateShip(450, 0);
            tractorShip.TractorBeamPower = 5;
            tractorShip.TractorBeamnRange = 200;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(withdrawingShip),
                    DefenderFleet = CreateFleet(tractorShip),
                }
            );
            TacticalUnitState withdrawingUnit = session.Units.Single(unit =>
                unit.Unit == withdrawingShip
            );
            Vector3 initialPosition = withdrawingUnit.Position;
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Withdraw);

            session.Advance(2f);

            Assert.AreEqual(initialPosition, withdrawingUnit.Position);
            Assert.IsFalse(withdrawingUnit.IsWithdrawing);
        }

        [Test]
        public void Advance_WithdrawBehavior_UsesStaggeredExitDistances()
        {
            CapitalShip firstShip = CreateShip(600, 0);
            CapitalShip secondShip = CreateShip(600, 0);
            firstShip.SublightSpeed = 1;
            secondShip.SublightSpeed = 100;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(firstShip, secondShip),
                    DefenderFleet = CreateFleet(CreateShip(450, 0)),
                }
            );
            TacticalUnitState firstUnit = session.Units.Single(unit => unit.Unit == firstShip);
            TacticalUnitState secondUnit = session.Units.Single(unit => unit.Unit == secondShip);
            Vector3 firstOrigin = firstUnit.Position;
            Vector3 secondOrigin = secondUnit.Position;
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Withdraw);

            session.Advance(0.5f);

            Assert.AreEqual(firstOrigin - Vector3.UnitZ * 10f, firstUnit.Position);
            Assert.AreEqual(secondOrigin - Vector3.UnitZ * 14.375f, secondUnit.Position);
        }

        [Test]
        public void DrainEvents_CompletedWithdrawal_ReturnsWithdrawalEvent()
        {
            CapitalShip attackingShip = CreateShip(600, 0);
            attackingShip.SublightSpeed = 100;
            TacticalBattleSession session = CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip),
                    DefenderFleet = CreateFleet(CreateShip(450, 0)),
                }
            );
            session
                .GetTaskForces(TacticalBattleSide.Attacker)
                .Single()
                .SetBehavior(TacticalBehavior.Withdraw);

            session.Advance(2f);
            TacticalCombatEvent withdrawal = session
                .DrainEvents()
                .Single(combatEvent => combatEvent.Kind == TacticalCombatEventKind.UnitWithdrawn);

            Assert.AreSame(attackingShip, withdrawal.Source.Unit);
        }

        [Test]
        public void BuildResult_DestroyedDefender_RecordsWinnerAndUnitLosses()
        {
            CapitalShip attackingShip = CreateShip(600, 250);
            Starfighter attackingFighters = CreateFighters(8, 12);
            attackingShip.Starfighters.Add(attackingFighters);
            CapitalShip defendingShip = CreateShip(450, 175);
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(attackingShip),
                DefenderFleet = CreateFleet(defendingShip),
                AttackerOwnerInstanceID = "attacker",
                DefenderOwnerInstanceID = "defender",
                Tick = 42,
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);
            session.Units.Single(unit => unit.Unit == attackingShip).Hull = 500;
            session.Units.Single(unit => unit.Unit == attackingFighters).Hull = 6;
            session.Units.Single(unit => unit.Unit == defendingShip).Hull = 0;

            SpaceCombatResult result = session.BuildResult();

            Assert.AreEqual(CombatSide.Attacker, result.Winner);
            Assert.AreEqual(SpaceCombatSideOutcome.Active, result.AttackerOutcome);
            Assert.AreEqual(SpaceCombatSideOutcome.Destroyed, result.DefenderOutcome);
            Assert.AreEqual(42, result.Tick);
            Assert.AreEqual(
                500,
                result.ShipDamage.Single(damage => damage.Ship == attackingShip).HullAfter
            );
            Assert.AreEqual(
                0,
                result.ShipDamage.Single(damage => damage.Ship == defendingShip).HullAfter
            );
            Assert.AreEqual(
                6,
                result.FighterLosses.Single(loss => loss.Fighter == attackingFighters).SquadsAfter
            );
        }

        [Test]
        public void BuildResult_BothSidesActive_ThrowsInvalidOperationException()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = CreateTacticalSession(encounter);

            Assert.Throws<InvalidOperationException>(() => session.BuildResult());
        }

        private static CapitalShip CreateShip(int hull, int shields, params Starfighter[] fighters)
        {
            CapitalShip ship = new CapitalShip
            {
                CurrentHullStrength = hull,
                MaxHullStrength = Math.Max(hull, 1),
                MaxShieldStrength = shields,
                Hyperdrive = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            ship.Starfighters.AddRange(fighters);
            return ship;
        }

        private static TacticalBattleSession CreateSession(CapitalShip attackingShip = null)
        {
            return CreateTacticalSession(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip ?? CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(CreateShip(450, 175)),
                }
            );
        }

        private static TacticalBattleSession CreateTacticalSession(PendingCombatResult encounter)
        {
            TacticalBattleSession session = TacticalBattleSession.Create(
                encounter,
                new FixedRandomProvider(new[] { 0d })
            );
            session.Advance(1f);
            return session;
        }

        private static TacticalBattleSession CreateArrivingSession(
            CapitalShip attackingShip = null,
            CapitalShip defendingShip = null
        )
        {
            return TacticalBattleSession.Create(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip ?? CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(defendingShip ?? CreateShip(450, 175)),
                },
                new FixedRandomProvider(new[] { 0d })
            );
        }

        private static Starfighter CreateFighters(
            int squadronSize,
            int shieldStrength,
            string typeId = null
        )
        {
            return new Starfighter
            {
                TypeID = typeId,
                CurrentSquadronSize = squadronSize,
                MaxSquadronSize = Math.Max(squadronSize, 1),
                ShieldStrength = shieldStrength,
                Hyperdrive = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Fleet CreateFleet(params CapitalShip[] ships)
        {
            return new Fleet { CapitalShips = ships.ToList() };
        }
    }
}
