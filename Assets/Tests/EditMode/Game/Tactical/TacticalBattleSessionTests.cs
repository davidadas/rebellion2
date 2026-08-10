using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Tactical;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Tactical
{
    [TestFixture]
    public class TacticalBattleSessionTests
    {
        [Test]
        public void Create_NullEncounter_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TacticalBattleSession.Create(null));
        }

        [Test]
        public void Create_MissingFleet_ThrowsArgumentException()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(),
            };

            Assert.Throws<ArgumentException>(() => TacticalBattleSession.Create(encounter));
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

            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

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

            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

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

            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

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

            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

            TacticalUnitState state = session.Units.Single(unit => unit.Unit == attackingShip);
            Assert.AreEqual(3, state.WeaponBatteries.Count);
            Assert.AreEqual(
                40,
                state
                    .WeaponBatteries.Single(battery =>
                        battery.WeaponType == PrimaryWeaponType.Turbolaser
                    )
                    .GetCount(TacticalWeaponArc.Starboard)
            );
        }

        [Test]
        public void CreateGroup_UnitsFromOneBattleSide_CreatesTrackedGroup()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250), CreateShip(500, 200)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = TacticalBattleSession.Create(encounter);
            TacticalUnitState[] attackingUnits = session
                .Units.Where(unit => unit.Side == TacticalBattleSide.Attacker)
                .ToArray();

            TacticalShipGroup group = session.CreateGroup(attackingUnits);

            Assert.AreEqual(TacticalBattleSide.Attacker, group.Side);
            Assert.AreEqual(2, group.Units.Count);
            Assert.AreSame(group, session.Groups.Single());
        }

        [Test]
        public void CreateGroup_UnitsFromOpposingSides_ThrowsArgumentException()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

            Assert.Throws<ArgumentException>(() => session.CreateGroup(session.Units));
        }

        [Test]
        public void DeleteGroup_TrackedGroup_RemovesGroup()
        {
            PendingCombatResult encounter = new PendingCombatResult
            {
                AttackerFleet = CreateFleet(CreateShip(600, 250)),
                DefenderFleet = CreateFleet(CreateShip(450, 175)),
            };
            TacticalBattleSession session = TacticalBattleSession.Create(encounter);
            TacticalShipGroup group = session.CreateGroup(
                session.Units.Where(unit => unit.Side == TacticalBattleSide.Attacker)
            );

            bool removed = session.DeleteGroup(group);

            Assert.IsTrue(removed);
            Assert.IsEmpty(session.Groups);
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
            TacticalBattleSession session = TacticalBattleSession.Create(encounter);
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
            TacticalBattleSession session = TacticalBattleSession.Create(encounter);

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
            return TacticalBattleSession.Create(
                new PendingCombatResult
                {
                    AttackerFleet = CreateFleet(attackingShip ?? CreateShip(600, 250)),
                    DefenderFleet = CreateFleet(CreateShip(450, 175)),
                }
            );
        }

        private static Starfighter CreateFighters(int squadronSize, int shieldStrength)
        {
            return new Starfighter
            {
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
