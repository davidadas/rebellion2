using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class NamingCommandsTests
    {
        private GameRoot _game;
        private Faction _faction;
        private NamingCommands _system;

        /// <summary>
        /// Sets up.
        /// </summary>
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
            _system = new NamingCommands(_game);
        }

        /// <summary>
        /// Verifies aicontrolled faction: assigns name.
        /// </summary>
        [Test]
        public void ProcessTick_AIControlledFaction_AssignsName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            _system.ProcessTick();

            Assert.AreEqual("First", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
        }

        /// <summary>
        /// Verifies eligible ships: assigns sequential names.
        /// </summary>
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

        /// <summary>
        /// Verifies player faction with management: assigns name.
        /// </summary>
        [Test]
        public void ProcessFaction_PlayerFactionWithManagement_AssignsName()
        {
            _game
                .GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "PLAYER",
                        FactionID = _faction.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            _faction.ManageNaming = true;
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(1, assignedCount);
            Assert.AreEqual("First", ship.DisplayName);
        }

        /// <summary>
        /// Verifies exhausted pools: assigns generic name.
        /// </summary>
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

        /// <summary>
        /// Verifies more than ten eligible ships: assigns all names.
        /// </summary>
        [Test]
        public void ProcessFaction_MoreThanTenEligibleShips_AssignsAllNames()
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

            Assert.AreEqual(11, assignedCount);
            Assert.AreEqual(11, ships.Count(ship => ship.HasAssignedName));
        }

        /// <summary>
        /// Verifies ship under construction: does not assign name.
        /// </summary>
        [Test]
        public void ProcessFaction_ShipUnderConstruction_DoesNotAssignName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Building);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        /// <summary>
        /// Verifies already named ship: does not replace name.
        /// </summary>
        [Test]
        public void ProcessFaction_AlreadyNamedShip_DoesNotReplaceName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);
            ship.AssignName("Existing Name");

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.AreEqual("Existing Name", ship.DisplayName);
        }

        /// <summary>
        /// Verifies ship without name pool: does not assign name.
        /// </summary>
        [Test]
        public void ProcessFaction_ShipWithoutNamePool_DoesNotAssignName()
        {
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);
            ship.ShipNamePoolID = null;

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        /// <summary>
        /// Verifies player faction without management: does not assign name.
        /// </summary>
        [Test]
        public void ProcessFaction_PlayerFactionWithoutManagement_DoesNotAssignName()
        {
            _game
                .GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "PLAYER",
                        FactionID = _faction.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            CapitalShip ship = AddShip("SHIP", ManufacturingStatus.Complete);

            int assignedCount = _system.ProcessFaction(_faction);

            Assert.AreEqual(0, assignedCount);
            Assert.IsFalse(ship.HasAssignedName);
        }

        /// <summary>
        /// Verifies null faction: throws argument null exception.
        /// </summary>
        [Test]
        public void ProcessFaction_NullFaction_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _system.ProcessFaction(null));
        }

        /// <summary>
        /// Adds ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="manufacturingStatus">The manufacturing status.</param>
        /// <returns>The result of add ship.</returns>
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
