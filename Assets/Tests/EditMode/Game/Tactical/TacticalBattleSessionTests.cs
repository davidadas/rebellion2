using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
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
        public void Advance_PrimaryTargetWithoutAssignment_TargetsLastOpposingUnit()
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

            session.Advance(0.1f);

            Assert.AreEqual(
                100,
                session.Units.Single(unit => unit.Unit == firstDefendingShip).Hull
            );
            Assert.AreEqual(70, session.Units.Single(unit => unit.Unit == lastDefendingShip).Hull);
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
        public void Advance_AttackDeathStarBehavior_TargetsOnlyDeathStar()
        {
            Starfighter attackingFighters = CreateFighters(12, 0);
            attackingFighters.LaserCannon = 10;
            attackingFighters.LaserRange = 200;
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

            session.Advance(0.1f);

            Assert.AreEqual(90, session.Units.Single(unit => unit.Unit == deathStar).Hull);
            Assert.AreEqual(100, session.Units.Single(unit => unit.Unit == ordinaryShip).Hull);
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
            return TacticalBattleSession.Create(encounter, new FixedRandomProvider(new[] { 0d }));
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
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private static Fleet CreateFleet(params CapitalShip[] ships)
        {
            return new Fleet { CapitalShips = ships.ToList() };
        }
    }
}
