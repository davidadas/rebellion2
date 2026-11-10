using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public sealed class SpaceCombatEntryTrackerTests
    {
        private GameRoot _game;
        private GameSession _session;
        private GameDataCatalog _catalog;
        private Planet _origin;
        private Planet _destination;
        private int _nextID;

        /// <summary>
        /// Builds two locations and binds their runtime faction definitions.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = "alpha" });
            _game.GetFactions().Add(new Faction { InstanceID = "beta" });
            PlanetSector sector = new() { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = "alpha",
                IsColonized = true,
                PositionX = 10,
            };
            _destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = "alpha",
                IsColonized = true,
                PositionX = 60,
            };
            _game.AttachNode(_origin, sector);
            _game.AttachNode(_destination, sector);
            _catalog = TestGameData.Create(_game.Config);
            _catalog.TacticalCombat.Factions.Add(
                new TacticalCombatFactionDefinition
                {
                    FactionInstanceID = "alpha",
                    FactionIndex = 0,
                }
            );
            _catalog.TacticalCombat.Factions.Add(
                new TacticalCombatFactionDefinition { FactionInstanceID = "beta", FactionIndex = 1 }
            );
            _session = new GameSession(_game, _catalog);
        }

        /// <summary>
        /// Disconnects the runtime used by the fixture.
        /// </summary>
        [TearDown]
        public void TearDown() => _session.Dispose();

        [TestCase("alpha", 1)]
        [TestCase("beta", 2)]
        public void AttachNode_CompletedCapitalActivatesFleet_RecordsAuthoredFaction(
            string owner,
            int expected
        )
        {
            Fleet fleet = AddFleet(owner);

            AddCapital(fleet);

            Assert.AreEqual(expected, _origin.SpaceCombatEntryFaction);
        }

        [TestCase(false, ManufacturingStatus.Complete, false)]
        [TestCase(true, ManufacturingStatus.Building, false)]
        [TestCase(true, ManufacturingStatus.Delivering, false)]
        [TestCase(true, ManufacturingStatus.Complete, true)]
        public void AttachNode_CapitalNotReady_PreservesEntryFaction(
            bool enabled,
            ManufacturingStatus status,
            bool moving
        )
        {
            Fleet fleet = AddFleet();
            _origin.SpaceCombatEntryFaction = 2;
            CapitalShip ship = CreateCapital(fleet.OwnerInstanceID);
            ship.IsEnabled = enabled;
            ship.ManufacturingStatus = status;
            ship.Movement = moving ? new MovementState() : null;

            _game.AttachNode(ship, fleet);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void AttachNode_FleetUnavailable_PreservesEntryFaction(bool enabled, bool moving)
        {
            Fleet fleet = AddFleet();
            fleet.IsEnabled = enabled;
            fleet.Movement = moving ? new MovementState() : null;
            _origin.SpaceCombatEntryFaction = 2;

            AddCapital(fleet);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void ManufacturingStatus_FirstCapitalCompletes_RecordsFleetFaction()
        {
            Fleet fleet = AddFleet();
            CapitalShip ship = AddCapital(fleet, ManufacturingStatus.Building);
            _origin.SpaceCombatEntryFaction = 2;

            ship.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void ManufacturingStatus_AdditionalCapitalCompletes_PreservesExistingEntryFaction()
        {
            Fleet fleet = AddFleet();
            AddCapital(fleet);
            CapitalShip ship = AddCapital(fleet, ManufacturingStatus.Building);
            _origin.SpaceCombatEntryFaction = 2;

            ship.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void Movement_FleetTransitEndsInPlace_RecordsFleetFaction()
        {
            Fleet fleet = AddFleet();
            fleet.Movement = new MovementState();
            AddCapital(fleet);
            _origin.SpaceCombatEntryFaction = 2;

            fleet.Movement = null;

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void Movement_CapitalTransitEndsInMovingFleet_WaitsForFleetArrival()
        {
            Fleet fleet = AddFleet();
            fleet.Movement = new MovementState();
            CapitalShip ship = CreateCapital("alpha");
            ship.Movement = new MovementState();
            _game.AttachNode(ship, fleet);
            _origin.SpaceCombatEntryFaction = 2;

            ship.Movement = null;

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
            fleet.Movement = null;
            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void Movement_LastTransferringCapitalArrives_ActivatesStationaryFleet()
        {
            Fleet fleet = AddFleet();
            CapitalShip ship = CreateCapital("alpha");
            ship.Movement = new MovementState();
            _game.AttachNode(ship, fleet);
            _origin.SpaceCombatEntryFaction = 2;

            ship.Movement = null;

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [TestCase(false, ManufacturingStatus.Complete, false)]
        [TestCase(true, ManufacturingStatus.Building, false)]
        [TestCase(true, ManufacturingStatus.Delivering, false)]
        [TestCase(true, ManufacturingStatus.Complete, true)]
        public void AttachNode_DirectFighterNotReady_PreservesEntryFaction(
            bool enabled,
            ManufacturingStatus status,
            bool moving
        )
        {
            Starfighter fighter = CreateFighter();
            fighter.IsEnabled = enabled;
            fighter.ManufacturingStatus = status;
            fighter.Movement = moving ? new MovementState() : null;
            _origin.SpaceCombatEntryFaction = 2;

            _game.AttachNode(fighter, _origin);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void AttachNode_ReadyDirectFighter_RecordsFaction()
        {
            _origin.SpaceCombatEntryFaction = 2;

            _game.AttachNode(CreateFighter(), _origin);

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void AttachNode_CarriedFighter_PreservesEntryFaction()
        {
            CapitalShip carrier = AddCapital(AddFleet());
            _origin.SpaceCombatEntryFaction = 2;

            _game.AttachNode(CreateFighter(), carrier);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void ManufacturingStatus_DirectFighterCompletes_RecordsFaction()
        {
            Starfighter fighter = CreateFighter();
            fighter.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(fighter, _origin);
            _origin.SpaceCombatEntryFaction = 2;

            fighter.ManufacturingStatus = ManufacturingStatus.Complete;

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void Movement_DirectFighterArrivesInPlace_RecordsFaction()
        {
            Starfighter fighter = CreateFighter();
            fighter.Movement = new MovementState();
            _game.AttachNode(fighter, _origin);
            _origin.SpaceCombatEntryFaction = 2;

            fighter.Movement = null;

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void ChangeOwnership_ActiveDirectFighter_PreservesEntryFaction()
        {
            Starfighter fighter = CreateFighter();
            _game.AttachNode(fighter, _origin);
            _origin.SpaceCombatEntryFaction = 1;

            _game.ChangeOwnership(fighter, "beta");

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void ChangeOwnership_CapitalBecomesSameOwner_ActivatesFleet()
        {
            Fleet fleet = AddFleet();
            CapitalShip ship = AddCapital(fleet);
            _game.ChangeOwnership(ship, "beta");
            _origin.SpaceCombatEntryFaction = 2;

            _game.ChangeOwnership(ship, "alpha");

            Assert.AreEqual(1, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void DeleteNode_LastCapitalRemoved_PreservesEntryFaction()
        {
            CapitalShip ship = AddCapital(AddFleet());
            _origin.SpaceCombatEntryFaction = 2;

            _game.DeleteNode(ship);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void DetachNode_FighterChangesAfterRemoval_DoesNotRecordEntry()
        {
            Starfighter fighter = CreateFighter();
            _game.AttachNode(fighter, _origin);
            _game.DetachNode(fighter);
            _origin.SpaceCombatEntryFaction = 2;

            fighter.IsEnabled = false;
            fighter.IsEnabled = true;

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void MoveNode_ReadyFleetPlacedAtAnotherPlanet_RecordsOnlyDestination()
        {
            Fleet fleet = AddFleet();
            AddCapital(fleet);
            _origin.SpaceCombatEntryFaction = 2;
            _destination.SpaceCombatEntryFaction = 2;

            _game.MoveNode(fleet, _destination);

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
            Assert.AreEqual(1, _destination.SpaceCombatEntryFaction);
        }

        [Test]
        public void MoveNode_DestinationThrows_RetainsLocationAndEntryValues()
        {
            Fleet fleet = AddFleet();
            AddCapital(fleet);
            RejectingPlanet rejected = new() { InstanceID = "rejected" };
            _game.AttachNode(rejected, _origin.GetParent());
            _origin.SpaceCombatEntryFaction = 2;
            rejected.SpaceCombatEntryFaction = 2;

            Assert.Throws<InvalidOperationException>(() => _game.MoveNode(fleet, rejected));

            Assert.AreSame(_origin, fleet.GetParent());
            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
            Assert.AreEqual(2, rejected.SpaceCombatEntryFaction);
        }

        [Test]
        public void RebuildSceneState_ActiveOrbiters_PreservesSavedEntryFaction()
        {
            AddCapital(AddFleet());
            _game.AttachNode(CreateFighter(), _origin);
            _origin.SpaceCombatEntryFaction = 2;

            _game.RebuildSceneState();

            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void Constructor_RestoredGraph_PreservesEntryFactionUntilNextActivation()
        {
            Fleet fleet = AddFleet();
            AddCapital(fleet);
            _origin.SpaceCombatEntryFaction = 2;
            GameSerializer serializer = new(typeof(GameRoot));
            using MemoryStream data = new();
            serializer.Serialize(data, _game);
            data.Position = 0;
            GameRoot restored = (GameRoot)serializer.Deserialize(data);

            using GameSession runtime = new(restored, _catalog);

            Planet origin = restored.GetSceneNodeByInstanceID<Planet>(_origin.InstanceID);
            Assert.AreEqual(2, origin.SpaceCombatEntryFaction);
            Fleet restoredFleet = restored.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID);
            restoredFleet.IsEnabled = false;
            restoredFleet.IsEnabled = true;
            Assert.AreEqual(1, origin.SpaceCombatEntryFaction);
        }

        [Test]
        public void TryRequestMove_DepartingFleet_DoesNotRecordDestinationUntilArrival()
        {
            Fleet fleet = AddFleet();
            AddCapital(fleet);
            _origin.SpaceCombatEntryFaction = 2;
            _destination.SpaceCombatEntryFaction = 2;
            MovementCommands movement = _session.GetService<MovementCommands>();

            Assert.IsTrue(
                movement.TryRequestMove(new ISceneNode[] { fleet }, _destination, "alpha")
            );

            Assert.IsNotNull(fleet.Movement);
            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
            Assert.AreEqual(2, _destination.SpaceCombatEntryFaction);
            fleet.Movement.Advance(
                fleet.Movement.TransitTicks - 1,
                new System.Drawing.Point(_destination.PositionX, _destination.PositionY)
            );
            new MovementTickProcessor(movement).ProcessTick(_game);
            Assert.IsNull(fleet.Movement);
            Assert.AreEqual(1, _destination.SpaceCombatEntryFaction);
        }

        [Test]
        public void TryRequestMove_DepartingDirectFighter_DoesNotRecordDestination()
        {
            Starfighter fighter = CreateFighter();
            _game.AttachNode(fighter, _origin);
            _origin.SpaceCombatEntryFaction = 2;
            _destination.SpaceCombatEntryFaction = 2;

            Assert.IsTrue(
                _session
                    .GetService<MovementCommands>()
                    .TryRequestMove(new ISceneNode[] { fighter }, _destination, "alpha")
            );

            Assert.IsNotNull(fighter.Movement);
            Assert.AreEqual(2, _origin.SpaceCombatEntryFaction);
            Assert.AreEqual(2, _destination.SpaceCombatEntryFaction);
        }

        /// <summary>
        /// Creates an attached empty fleet owned by the requested faction.
        /// </summary>
        /// <param name="owner">The owning faction.</param>
        /// <returns>The attached fleet.</returns>
        private Fleet AddFleet(string owner = "alpha")
        {
            Fleet fleet = new() { InstanceID = $"fleet-{++_nextID}", OwnerInstanceID = owner };
            _game.AttachNode(fleet, _origin);
            return fleet;
        }

        /// <summary>
        /// Attaches one capital ship with the requested construction state.
        /// </summary>
        /// <param name="fleet">The destination fleet.</param>
        /// <param name="status">The initial construction state.</param>
        /// <returns>The attached capital ship.</returns>
        private CapitalShip AddCapital(
            Fleet fleet,
            ManufacturingStatus status = ManufacturingStatus.Complete
        )
        {
            CapitalShip ship = CreateCapital(fleet.OwnerInstanceID);
            ship.ManufacturingStatus = status;
            _game.AttachNode(ship, fleet);
            return ship;
        }

        /// <summary>
        /// Creates a completed capital ship that can travel between planets.
        /// </summary>
        /// <param name="owner">The owning faction.</param>
        /// <returns>The detached ship.</returns>
        private CapitalShip CreateCapital(string owner) =>
            new()
            {
                InstanceID = $"capital-{++_nextID}",
                OwnerInstanceID = owner,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 1,
                StarfighterCapacity = 8,
            };

        /// <summary>
        /// Creates a completed fighter that can travel independently.
        /// </summary>
        /// <returns>The detached fighter.</returns>
        private Starfighter CreateFighter() =>
            new()
            {
                InstanceID = $"fighter-{++_nextID}",
                OwnerInstanceID = "alpha",
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 1,
            };

        /// <summary>
        /// Accepts validation but refuses the subsequent containment change.
        /// </summary>
        private sealed class RejectingPlanet : Planet
        {
            /// <summary>
            /// Rejects the attempted child insertion.
            /// </summary>
            /// <param name="child">The incoming node.</param>
            public override void AddChild(ISceneNode child) =>
                throw new InvalidOperationException("Containment rejected.");
        }
    }
}
