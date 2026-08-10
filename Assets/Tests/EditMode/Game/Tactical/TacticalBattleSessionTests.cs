using System;
using System.Linq;
using NUnit.Framework;
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
