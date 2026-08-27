using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class NamingSystemTests
    {
        private GameRoot _game;
        private Faction _faction;
        private NamingSystem _system;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot();
            _faction = new Faction { InstanceID = "FACTION" };
            _faction.ShipNamePools.Add(
                new FactionNamePool
                {
                    NamePoolID = "POOL",
                    Names = new List<string> { "First", "Second", "Third" },
                }
            );
            _game.GetFactions().Add(_faction);
            _system = new NamingSystem(_game);
        }

        [Test]
        public void ProcessTick_AIControlledFaction_AssignsName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            _system.ProcessTick();

            Assert.AreEqual("First", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_EligibleShips_AssignsSequentialNames()
        {
            CapitalShip firstShip = AddShip("FIRST", ManufacturingStatus.Complete);
            CapitalShip secondShip = AddShip("SECOND", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(2, assignedCount);
            Assert.AreEqual("First", firstShip.DisplayName);
            Assert.AreEqual("Second", secondShip.DisplayName);
            Assert.IsTrue(firstShip.HasAssignedName);
            Assert.IsTrue(secondShip.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_PlayerFactionWithManagement_AssignsName()
        {
            _faction.PlayerID = "PLAYER";
            _faction.ManageNaming = true;
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(1, assignedCount);
            Assert.AreEqual("First", ship.DisplayName);
        }

        [Test]
        public void ProcessFaction_ExhaustedPools_AssignsGenericName()
        {
            _faction.ShipNamePools.Single().NextNameIndex = 3;
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(1, assignedCount);
            Assert.AreEqual("Generic SHIP 1", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_MoreThanTenEligibleShips_AssignsTenNames()
        {
            _faction.ShipNamePools.Single().Names = Enumerable
                .Range(1, 11)
                .Select(index => $"Ship {index}")
                .ToList();
            List<CapitalShip> ships = Enumerable
                .Range(1, 11)
                .Select(index => AddShip($"SHIP_{index}", ManufacturingStatus.Complete))
                .ToList();

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(10, assignedCount);
            Assert.AreEqual(10, ships.Count(ship => ship.HasAssignedName));
        }

        [Test]
        public void ProcessFaction_ShipUnderConstruction_DoesNotAssignName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Building);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_AlreadyNamedShip_DoesNotReplaceName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);
            ship.AssignName("Existing Name");

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.AreEqual("Existing Name", ship.DisplayName);
        }

        [Test]
        public void ProcessFaction_ShipWithoutNamePool_DoesNotAssignName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);
            ship.ShipNamePoolID = null;

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_PlayerFactionWithoutManagement_DoesNotAssignName()
        {
            _faction.PlayerID = "PLAYER";
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        [Test]
        public void ProcessFaction_NullFaction_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _system.ProcessFaction(null));
        }

        private CapitalShip AddShip(string instanceId, ManufacturingStatus manufacturingStatus)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = "SHIP_TYPE",
                DisplayName = $"Generic {instanceId}",
                OwnerInstanceID = _faction.InstanceID,
                ShipNamePoolID = "POOL",
                ManufacturingStatus = manufacturingStatus,
            };
            _faction.AddOwnedUnit(ship);
            return ship;
        }
    }
}
