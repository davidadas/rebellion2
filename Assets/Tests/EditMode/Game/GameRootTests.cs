using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameRootTests
    {
        private GameRoot _game;
        private GameSummary _summary;
        private Faction _faction1;
        private Faction _faction2;
        private GalaxyMap _galaxyMap;
        private PlanetSystem _planetSystem;
        private Planet _planet;
        private Fleet _fleet;

        [SetUp]
        public void SetUp()
        {
            // Initialize game _summary.
            _summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Conquest,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FACTION1",
            };

            // Create factions.
            _faction1 = new Faction { InstanceID = "FACTION1", DisplayName = "Alliance" };
            _faction2 = new Faction { InstanceID = "FACTION2", DisplayName = "Empire" };

            // Create game objects.
            _galaxyMap = new GalaxyMap();
            _planetSystem = new PlanetSystem { InstanceID = "SYSTEM1" };
            _planet = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };
            _fleet = new Fleet { InstanceID = "FLEET1", OwnerInstanceID = "FACTION1" };

            // Initialize the _game.
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            _game = new GameRoot(_summary, config);
            _game.Factions.Add(_faction1);
            _game.Factions.Add(_faction2);
        }

        [Test]
        public void Constructor_WithSummary_InitializesCorrectly()
        {
            // Verify game initialization.
            Assert.AreEqual(
                _summary,
                _game.Summary,
                "Game summary should match the provided summary"
            );
            Assert.IsNotNull(_game.Galaxy, "Galaxy should be initialized");
            Assert.AreEqual(0, _game.CurrentTick, "Current tick should be initialized to 0");
            Assert.IsEmpty(_game.EventPool, "Event pool should be empty initially");
            Assert.IsEmpty(
                _game.CompletedEventIDs,
                "Completed event IDs should be empty initially"
            );
        }

        [Test]
        public void GetFactions_GameWithMultipleFactions_ReturnsAllFactions()
        {
            // Get factions and verify the count and contents.
            List<Faction> factions = _game.GetFactions();
            Assert.AreEqual(2, factions.Count, "Should return two factions");
            Assert.Contains(_faction1, factions, "Should contain faction1");
            Assert.Contains(_faction2, factions, "Should contain faction2");
        }

        [Test]
        public void GetFactionByOwnerInstanceID_RegisteredFaction_ReturnsCorrectFaction()
        {
            // Get faction by ID and verify.
            Faction retrievedFaction = _game.GetFactionByOwnerInstanceID("FACTION1");
            Assert.AreEqual(_faction1, retrievedFaction, "Should return the correct faction");
        }

        [Test]
        public void GetFactionByOwnerInstanceID_ThrowsException_WhenFactionNotFound()
        {
            // Attempt to get non-existent faction.
            Assert.Throws<SceneNodeNotFoundException>(
                () => _game.GetFactionByOwnerInstanceID("NONEXISTENT"),
                "Should throw exception for non-existent faction"
            );
        }

        [Test]
        public void GetGalaxyMap_InitializedGame_ReturnsGalaxyMap()
        {
            // Get galaxy map and verify.
            GalaxyMap retrievedGalaxyMap = _game.GetGalaxyMap();
            Assert.AreEqual(
                _game.Galaxy,
                retrievedGalaxyMap,
                "Should return the correct galaxy map"
            );
        }

        [Test]
        public void AttachNode_ValidNode_AddsToSceneGraph()
        {
            // Attach node and verify.
            _game.AttachNode(_planet, _planetSystem);

            Assert.AreEqual(
                _planetSystem,
                _planet.GetParent(),
                "Planet should have planetSystem as parent"
            );
            Assert.Contains(
                _planet,
                _planetSystem.GetChildren().ToList(),
                "PlanetSystem should contain planet as child"
            );
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_planet.InstanceID),
                "Game should contain planet in NodesByInstanceID"
            );
            Assert.IsTrue(
                _game
                    .GetFactionByOwnerInstanceID(_planet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_planet),
                "Faction should contain planet in owned units"
            );
        }

        [Test]
        public void AttachNode_ThrowsException_WhenNodeAlreadyHasParent()
        {
            // Attach node to a parent.
            _game.AttachNode(_planet, _planetSystem);

            // Attempt to attach the same node to another parent.
            Assert.Throws<InvalidOperationException>(
                () => _game.AttachNode(_planet, new PlanetSystem()),
                "Should throw exception when attaching a node that already has a parent"
            );
        }

        [Test]
        public void DetachNode_AttachedNode_RemovesFromSceneGraph()
        {
            // Attach and then detach node.
            _game.AttachNode(_planet, _planetSystem);
            _game.DetachNode(_planet);

            // Verify detachment is successful and node is removed from all relevant structures.
            Assert.IsNull(_planet.GetParent(), "Planet should have no parent after detachment");
            Assert.IsFalse(
                _planetSystem.GetChildren().Contains(_planet),
                "PlanetSystem should not contain planet as child"
            );
            Assert.IsFalse(
                _game.NodesByInstanceID.ContainsKey(_planet.InstanceID),
                "Game should not contain planet in NodesByInstanceID"
            );
            Assert.IsFalse(
                _game
                    .GetFactionByOwnerInstanceID(_planet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_planet),
                "Faction should not contain planet in owned units"
            );
        }

        [Test]
        public void DetachNode_ThrowsException_WhenNodeHasNoParent()
        {
            // Attempt to detach a node with no parent.
            Assert.Throws<InvalidOperationException>(
                () => _game.DetachNode(_planet),
                "Should throw exception when detaching a node with no parent"
            );
        }

        [Test]
        public void AddSceneNodeByInstanceID_ValidNode_AddsToRegistry()
        {
            // Add node and verify that it is added to the _game.
            _game.AddSceneNodeByInstanceID(_planet);
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_planet.InstanceID),
                "Game should contain planet in NodesByInstanceID"
            );
        }

        [Test]
        public void AddSceneNodeByInstanceID_ThrowsException_WhenDuplicateNodeAdded()
        {
            // Add node to the _game.
            _game.AddSceneNodeByInstanceID(_planet);

            // Attempt to add the same node again.
            Assert.Throws<InvalidOperationException>(
                () => _game.AddSceneNodeByInstanceID(_planet),
                "Should throw exception when adding a duplicate node"
            );
        }

        [Test]
        public void RemoveSceneNodeByInstanceID_RegisteredNode_RemovesFromRegistry()
        {
            // Add and then remove node from the _game.
            _game.AddSceneNodeByInstanceID(_planet);
            _game.RemoveSceneNodeByInstanceID(_planet);

            // Verify removal from all relevant structures.
            Assert.IsFalse(
                _game.NodesByInstanceID.ContainsKey(_planet.InstanceID),
                "Game should not contain planet in NodesByInstanceID after removal"
            );
        }

        [Test]
        public void GetSceneNodeByInstanceID_RegisteredNode_ReturnsNode()
        {
            // Add node and retrieve it.
            _game.AddSceneNodeByInstanceID(_planet);
            Planet retrievedNode = _game.GetSceneNodeByInstanceID<Planet>(_planet.InstanceID);

            // Verify retrieval of the correct node.
            Assert.AreEqual(_planet, retrievedNode, "Should return the correct node");
        }

        [Test]
        public void GetSceneNodeByInstanceID_ReturnsNull_WhenNodeNotFound()
        {
            // Attempt to retrieve non-existent node.
            Planet retrievedNode = _game.GetSceneNodeByInstanceID<Planet>("NONEXISTENT");
            Assert.IsNull(retrievedNode, "Should return null for non-existent node");
        }

        [Test]
        public void GetSceneNodesByInstanceIDs_MultipleRegisteredNodes_ReturnsNodes()
        {
            // Add nodes to the _game.
            _game.AddSceneNodeByInstanceID(_planet);
            _game.AddSceneNodeByInstanceID(_fleet);

            // Retrieve nodes by IDs.
            List<ISceneNode> retrievedNodes = _game.GetSceneNodesByInstanceIDs(
                new List<string> { _planet.InstanceID, _fleet.InstanceID }
            );

            // Verify retrieval of planets and fleets.
            Assert.AreEqual(2, retrievedNodes.Count, "Should return two nodes");
            Assert.Contains(_planet, retrievedNodes, "Should contain planet");
            Assert.Contains(_fleet, retrievedNodes, "Should contain fleet");
        }

        [Test]
        public void GetSceneNodesByOwnerInstanceID_NodesWithMatchingOwner_ReturnsNodes()
        {
            // Add nodes to the _game.
            _game.AddSceneNodeByInstanceID(_planet);
            _game.AddSceneNodeByInstanceID(_fleet);

            // Retrieve nodes by owner ID.
            List<ISceneNode> retrievedNodes = _game.GetSceneNodesByOwnerInstanceID<ISceneNode>(
                "FACTION1"
            );

            // Verify retrieval of planets and fleets.
            Assert.AreEqual(2, retrievedNodes.Count, "Should return two nodes");
            Assert.Contains(_planet, retrievedNodes, "Should contain planet");
            Assert.Contains(_fleet, retrievedNodes, "Should contain fleet");
        }

        [Test]
        public void GetSceneNodesByType_GameWithMixedNodes_ReturnsNodesOfType()
        {
            // Set up galaxy structure.
            _game.Galaxy = _galaxyMap;
            _game.AttachNode(_planetSystem, _galaxyMap);
            _game.AttachNode(_planet, _planetSystem);
            _game.AttachNode(_fleet, _planet);

            // Retrieve planets and verify the count and contents.
            List<Planet> retrievedPlanets = _game.GetSceneNodesByType<Planet>();
            Assert.AreEqual(1, retrievedPlanets.Count, "Should return one planet");
            Assert.Contains(_planet, retrievedPlanets, "Should contain the specific planet");

            // Retrieve fleets and verify the count and contents.
            List<Fleet> retrievedFleets = _game.GetSceneNodesByType<Fleet>();
            Assert.AreEqual(1, retrievedFleets.Count, "Should return one fleet");
            Assert.Contains(_fleet, retrievedFleets, "Should contain the specific fleet");
        }

        [Test]
        public void RegisterOwnedUnit_ValidUnit_AddsUnitToFaction()
        {
            // Register unit.
            _game.RegisterOwnedUnit(_planet);

            // Verify registration is successful.
            Assert.IsTrue(
                _game
                    .GetFactionByOwnerInstanceID(_planet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_planet),
                "Faction should contain planet in owned units after registration"
            );
        }

        [Test]
        public void DeregisterOwnedUnit_RegisteredUnit_RemovesUnitFromFaction()
        {
            // Register and then deregister unit.
            _game.RegisterOwnedUnit(_planet);
            _game.DeregsiterOwnedUnit(_planet);

            // Verify deregistration was successful.
            Assert.IsFalse(
                _game
                    .GetFactionByOwnerInstanceID(_planet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_planet),
                "Faction should not contain planet in owned units after deregistration"
            );
        }

        [Test]
        public void GetEventPool_InitializedGame_ReturnsEventPool()
        {
            // Add events to pool.
            GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
            GameEvent event2 = new GameEvent { InstanceID = "EVENT2" };
            _game.EventPool.Add(event1);
            _game.EventPool.Add(event2);

            // Retrieve event pool and verify the count and contents.
            List<GameEvent> eventPool = _game.GetEventPool();
            Assert.AreEqual(2, eventPool.Count, "Should return two events");
            Assert.Contains(event1, eventPool, "Should contain event1");
            Assert.Contains(event2, eventPool, "Should contain event2");
        }

        [Test]
        public void RemoveEvent_EventInPool_RemovesEventFromPool()
        {
            // Add event and then remove it.
            GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
            _game.EventPool.Add(event1);
            _game.RemoveEvent(event1);

            // Verify removal from the event pool.
            Assert.IsFalse(
                _game.EventPool.Contains(event1),
                "Event pool should not contain the removed event"
            );
        }

        [Test]
        public void GetEventByInstanceID_EventInPool_ReturnsMatchingEvent()
        {
            // Add event and retrieve it.
            GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
            _game.EventPool.Add(event1);

            // Retrieve event and verify.
            GameEvent retrievedEvent = _game.GetEventByInstanceID("EVENT1");
            Assert.AreEqual(event1, retrievedEvent, "Should return the correct event");
        }

        [Test]
        public void AddCompletedEvent_ValidEventID_AddsToCompletedList()
        {
            // Add completed event to the completed list.
            GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
            _game.AddCompletedEvent(event1);

            // Verify addition to the completed list.
            Assert.IsTrue(
                _game.CompletedEventIDs.Contains(event1.InstanceID),
                "Completed event IDs should contain the added event's ID"
            );
        }

        [Test]
        public void IsEventComplete_CompletedEvent_ReturnsTrue()
        {
            // Add completed event to the completed list.
            GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
            _game.AddCompletedEvent(event1);

            // Check completion status.
            Assert.IsTrue(_game.IsEventComplete("EVENT1"), "EVENT1 should be marked as complete");
            Assert.IsFalse(
                _game.IsEventComplete("EVENT2"),
                "EVENT2 should not be marked as complete"
            );
        }

        [Test]
        public void Galaxy_Setter_InitializesGalaxyCorrectly()
        {
            // Set up galaxy structure.
            _game.Galaxy = _galaxyMap;
            _game.AttachNode(_planetSystem, _galaxyMap);
            _game.AttachNode(_planet, _planetSystem);
            _game.AttachNode(_fleet, _planetSystem);

            // Verify galaxy initialization.
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_galaxyMap.InstanceID),
                "Game should contain galaxyMap in NodesByInstanceID"
            );
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_planetSystem.InstanceID),
                "Game should contain planetSystem in NodesByInstanceID"
            );
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_planet.InstanceID),
                "Game should contain planet in NodesByInstanceID"
            );
            Assert.IsTrue(
                _game.NodesByInstanceID.ContainsKey(_fleet.InstanceID),
                "Game should contain fleet in NodesByInstanceID"
            );

            Assert.AreEqual(
                _galaxyMap,
                _planetSystem.GetParent(),
                "PlanetSystem should have galaxyMap as parent"
            );
            Assert.AreEqual(
                _planetSystem,
                _planet.GetParent(),
                "Planet should have planetSystem as parent"
            );
            Assert.AreEqual(
                _planetSystem,
                _fleet.GetParent(),
                "Fleet should have planetSystem as parent"
            );

            Assert.IsTrue(
                _game
                    .GetFactionByOwnerInstanceID(_planet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_planet),
                "Faction should contain planet in owned units"
            );
            Assert.IsTrue(
                _game
                    .GetFactionByOwnerInstanceID(_fleet.OwnerInstanceID)
                    .GetAllOwnedNodes()
                    .Contains(_fleet),
                "Faction should contain fleet in owned units"
            );
        }

        [Test]
        public void GetPlayerFaction_MultiFactionalGame_ReturnsPlayerFaction()
        {
            // Get player faction and verify.
            Faction playerFaction = _game.GetPlayerFaction();
            Assert.AreEqual(_faction1, playerFaction, "Should return the correct player faction");
            Assert.AreEqual(
                "FACTION1",
                playerFaction.InstanceID,
                "Player faction should have correct ID"
            );
        }

        [Test]
        public void GetPlayerFaction_ThrowsException_WhenSummaryIsNull()
        {
            // Create game without _summary.
            GameRoot gameWithoutSummary = new GameRoot();

            // Attempt to get player faction.
            Assert.Throws<InvalidOperationException>(
                () => gameWithoutSummary.GetPlayerFaction(),
                "Should throw exception when GameSummary is null"
            );
        }

        [Test]
        public void GetPlayerFaction_ThrowsException_WhenPlayerFactionIDIsNull()
        {
            // Create game with summary but no player faction ID.
            GameSummary summaryWithoutPlayer = new GameSummary();
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot gameWithoutPlayerID = new GameRoot(summaryWithoutPlayer, config);

            // Attempt to get player faction.
            Assert.Throws<InvalidOperationException>(
                () => gameWithoutPlayerID.GetPlayerFaction(),
                "Should throw exception when PlayerFactionID is null or empty"
            );
        }

        [Test]
        public void GetPlayerFaction_ThrowsException_WhenPlayerFactionNotFound()
        {
            // Create game with invalid player faction ID.
            GameSummary summaryWithInvalidPlayer = new GameSummary
            {
                PlayerFactionID = "NONEXISTENT",
            };
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot gameWithInvalidPlayer = new GameRoot(summaryWithInvalidPlayer, config);
            gameWithInvalidPlayer.Factions.Add(_faction1);

            // Attempt to get player faction.
            Assert.Throws<InvalidOperationException>(
                () => gameWithInvalidPlayer.GetPlayerFaction(),
                "Should throw exception when player faction does not exist"
            );
        }

        [Test]
        public void SetGameSpeed_ValidSpeed_UpdatesGameSpeed()
        {
            // Set game speed and verify.
            _game.SetGameSpeed(TickSpeed.Fast);
            Assert.AreEqual(TickSpeed.Fast, _game.GameSpeed, "Game speed should be set to Fast");

            _game.SetGameSpeed(TickSpeed.Medium);
            Assert.AreEqual(
                TickSpeed.Medium,
                _game.GameSpeed,
                "Game speed should be set to Medium"
            );

            _game.SetGameSpeed(TickSpeed.Slow);
            Assert.AreEqual(TickSpeed.Slow, _game.GameSpeed, "Game speed should be set to Slow");

            _game.SetGameSpeed(TickSpeed.Paused);
            Assert.AreEqual(
                TickSpeed.Paused,
                _game.GameSpeed,
                "Game speed should be set to Paused"
            );
        }

        [Test]
        public void GetGameSpeed_DefaultGame_ReturnsDefaultSpeed()
        {
            // Set game speed and verify getter returns correct value.
            _game.SetGameSpeed(TickSpeed.Fast);
            TickSpeed speed = _game.GetGameSpeed();
            Assert.AreEqual(TickSpeed.Fast, speed, "GetGameSpeed should return Fast");

            _game.SetGameSpeed(TickSpeed.Medium);
            speed = _game.GetGameSpeed();
            Assert.AreEqual(TickSpeed.Medium, speed, "GetGameSpeed should return Medium");
        }

        [Test]
        public void GetGameSpeed_InitialState_ReturnsDefaultSpeed()
        {
            // Verify default speed is Medium.
            TickSpeed speed = _game.GetGameSpeed();
            Assert.AreEqual(TickSpeed.Medium, speed, "Default game speed should be Medium");
        }

        [Test]
        public void ChangeUnitOwnership_PlanetOwnedByFaction_ChangesOwnershipCorrectly()
        {
            // Register planet to _faction1.
            _game.RegisterOwnedUnit(_planet);

            // Verify planet is owned by _faction1.
            Assert.IsTrue(
                _faction1.GetAllOwnedNodes().Contains(_planet),
                "Faction1 should initially own the planet"
            );
            Assert.IsFalse(
                _faction2.GetAllOwnedNodes().Contains(_planet),
                "Faction2 should not initially own the planet"
            );

            // Change ownership to _faction2.
            _game.ChangeUnitOwnership(_planet, "FACTION2");

            // Verify planet is now owned by _faction2.
            Assert.IsFalse(
                _faction1.GetAllOwnedNodes().Contains(_planet),
                "Faction1 should no longer own the planet"
            );
            Assert.IsTrue(
                _faction2.GetAllOwnedNodes().Contains(_planet),
                "Faction2 should now own the planet"
            );
            Assert.AreEqual(
                "FACTION2",
                _planet.OwnerInstanceID,
                "Planet.OwnerInstanceID should reflect the new owner"
            );
        }

        [Test]
        public void ChangeUnitOwnership_ThrowsException_WhenNewOwnerNotFound()
        {
            // Register planet to _faction1.
            _game.RegisterOwnedUnit(_planet);

            // Attempt to change ownership to non-existent faction.
            Assert.Throws<SceneNodeNotFoundException>(
                () => _game.ChangeUnitOwnership(_planet, "NONEXISTENT"),
                "Should throw exception when new owner faction does not exist"
            );
        }

        [Test]
        public void GetUnrecruitedOfficers_GameWithUnrecruitedOfficers_ReturnsOfficers()
        {
            // Create officers with different allowed owner IDs.
            Officer officer1 = new Officer
            {
                InstanceID = "OFFICER1",
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1", "FACTION2" },
            };
            Officer officer2 = new Officer
            {
                InstanceID = "OFFICER2",
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Officer officer3 = new Officer
            {
                InstanceID = "OFFICER3",
                AllowedOwnerInstanceIDs = new List<string> { "FACTION2" },
            };

            _game.UnrecruitedOfficers.Add(officer1);
            _game.UnrecruitedOfficers.Add(officer2);
            _game.UnrecruitedOfficers.Add(officer3);

            // Get unrecruited officers for _faction1.
            List<Officer> faction1Officers = _game.GetUnrecruitedOfficers("FACTION1");
            Assert.AreEqual(2, faction1Officers.Count, "Faction1 should have access to 2 officers");
            Assert.Contains(officer1, faction1Officers, "Should contain officer1");
            Assert.Contains(officer2, faction1Officers, "Should contain officer2");

            // Get unrecruited officers for _faction2.
            List<Officer> faction2Officers = _game.GetUnrecruitedOfficers("FACTION2");
            Assert.AreEqual(2, faction2Officers.Count, "Faction2 should have access to 2 officers");
            Assert.Contains(officer1, faction2Officers, "Should contain officer1");
            Assert.Contains(officer3, faction2Officers, "Should contain officer3");
        }

        [Test]
        public void GetUnrecruitedOfficers_ReturnsEmptyList_WhenNoOfficersAvailable()
        {
            // Get unrecruited officers for faction with no available officers.
            List<Officer> officers = _game.GetUnrecruitedOfficers("FACTION1");
            Assert.IsEmpty(officers, "Should return empty list when no officers are available");
        }

        [Test]
        public void RemoveUnrecruitedOfficer_OfficerInList_RemovesOfficer()
        {
            // Create and add officer.
            Officer officer = new Officer
            {
                InstanceID = "OFFICER1",
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            _game.UnrecruitedOfficers.Add(officer);

            // Verify officer is in the list.
            Assert.Contains(
                officer,
                _game.UnrecruitedOfficers,
                "Officer should be in unrecruited list"
            );

            // Remove officer.
            _game.RemoveUnrecruitedOfficer(officer);

            // Verify officer is removed.
            Assert.IsFalse(
                _game.UnrecruitedOfficers.Contains(officer),
                "Officer should be removed from unrecruited list"
            );
        }

        [Test]
        public void RemoveUnrecruitedOfficer_OfficerNotInList_DoesNotThrow()
        {
            // Create officer that is not in the list.
            Officer officer = new Officer
            {
                InstanceID = "OFFICER1",
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };

            // Remove non-existent officer (should not throw exception).
            Assert.DoesNotThrow(() => _game.RemoveUnrecruitedOfficer(officer));
        }
    }
} // namespace Rebellion.Tests.Game
