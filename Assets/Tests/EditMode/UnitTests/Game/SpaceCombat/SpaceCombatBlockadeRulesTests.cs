using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public sealed class SpaceCombatBlockadeRulesTests
    {
        private GameRoot _game;
        private GameSession _session;
        private GameDataCatalog _catalog;
        private Planet _planet;
        private Fleet _enemyFleet;
        private CapitalShip _enemyShip;
        private Starfighter _fighter;
        private int _nextID;

        /// <summary>
        /// Creates a configured pair with one hostile fleet and one friendly direct fighter.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = "alpha" });
            _game.GetFactions().Add(new Faction { InstanceID = "beta" });
            PlanetSector sector = new() { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "alpha",
                IsColonized = true,
            };
            _game.AttachNode(_planet, sector);
            _catalog = TestGameData.Create(_game.Config);
            _session = new GameSession(_game, _catalog);
            _enemyFleet = AddFleet("beta");
            _enemyShip = AddCapital(_enemyFleet);
            _fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = "alpha",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_fighter, _planet);
        }

        /// <summary>
        /// Releases the fixture's session observers.
        /// </summary>
        [TearDown]
        public void TearDown() => _session.Dispose();

        [Test]
        public void IsBlockaded_ReadyDirectFriendlyFighter_SuppressesHostileFleet()
        {
            Assert.IsFalse(_planet.IsBlockaded());
            Assert.IsFalse(_planet.IsBlockadedFor("alpha"));
        }

        [Test]
        public void IsBlockaded_OnlyDirectHostileFighter_DoesNotEstablishBlockade()
        {
            _game.DeleteNode(_enemyFleet);
            _game.ChangeOwnership(_fighter, "beta");

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IsBlockaded_NeutralPlanet_RequiresPopulationAndOneOrbitalSide(bool populated)
        {
            _game.ChangeOwnership(_planet, null);
            _planet.IsColonized = populated;
            Assert.IsFalse(_planet.IsBlockaded());

            _game.DeleteNode(_fighter);

            Assert.AreEqual(populated, _planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_PopulatedNeutralWithBothReadyFleets_ReturnsFalse()
        {
            _game.ChangeOwnership(_planet, null);
            _game.DeleteNode(_fighter);
            AddCapital(AddFleet("alpha"));

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [TestCase(null)]
        [TestCase("")]
        public void IsBlockaded_NeutralFleetAtPopulatedNeutralPlanet_ReturnsFalse(string owner)
        {
            _game.DeleteNode(_enemyFleet);
            _game.DeleteNode(_fighter);
            _game.ChangeOwnership(_planet, null);
            _planet.OwnerInstanceID = owner;
            AddCapital(AddFleet(null));

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [TestCase("fleet")]
        [TestCase("planet")]
        [TestCase("sector")]
        [TestCase("galaxy")]
        public void IsBlockaded_BlockadingFleetOrAncestorDisabled_ReturnsFalse(string disabled)
        {
            _fighter.IsEnabled = false;
            switch (disabled)
            {
                case "fleet":
                    _enemyFleet.IsEnabled = false;
                    break;
                case "planet":
                    _planet.IsEnabled = false;
                    break;
                case "sector":
                    _planet.GetParent().IsEnabled = false;
                    break;
                case "galaxy":
                    _game.Galaxy.IsEnabled = false;
                    break;
            }

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [TestCase(false, ManufacturingStatus.Complete, false)]
        [TestCase(true, ManufacturingStatus.Building, false)]
        [TestCase(true, ManufacturingStatus.Delivering, false)]
        [TestCase(true, ManufacturingStatus.Complete, true)]
        public void IsBlockaded_DirectFighterUnavailable_DoesNotSuppressFleet(
            bool enabled,
            ManufacturingStatus status,
            bool moving
        )
        {
            _fighter.IsEnabled = enabled;
            _fighter.ManufacturingStatus = status;
            _fighter.Movement = moving ? new MovementState() : null;

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_FighterInsideDepartingCarrier_DoesNotCountAsDirectPresence()
        {
            Fleet ownFleet = AddFleet("alpha");
            CapitalShip carrier = AddCapital(ownFleet);
            ownFleet.Movement = new MovementState();
            _game.MoveNode(_fighter, carrier);

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_ZeroHullAndSquadronPools_RetainsReadinessUntilRemoval()
        {
            _enemyShip.CurrentHullStrength = 0;
            _fighter.CurrentSquadronSize = 0;
            Assert.IsFalse(_planet.IsBlockaded());

            _game.DeleteNode(_fighter);

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_NestedSceneUpdates_ReadsImmediatePresenceWithoutCommittingEntry()
        {
            _planet.SpaceCombatEntryFaction = 2;
            using (_game.BeginSceneUpdate())
            {
                _fighter.Movement = new MovementState();
                Assert.IsTrue(_planet.IsBlockaded());
                using (_game.BeginSceneUpdate())
                {
                    _fighter.Movement = null;
                    Assert.IsFalse(_planet.IsBlockaded());
                    Assert.AreEqual(2, _planet.SpaceCombatEntryFaction);
                }
                Assert.AreEqual(2, _planet.SpaceCombatEntryFaction);
            }
        }

        [TestCase("add")]
        [TestCase("remove")]
        [TestCase("rename")]
        [TestCase("duplicate")]
        [TestCase("null")]
        public void IsBlockaded_CampaignFactionDomainMutates_UsesExistingFleetPredicate(
            string change
        )
        {
            List<Faction> factions = _game.GetFactions();
            Faction first = factions[0];
            Faction second = factions[1];
            switch (change)
            {
                case "add":
                    factions.Add(new Faction { InstanceID = "gamma" });
                    break;
                case "remove":
                    factions.Remove(second);
                    break;
                case "rename":
                    second.InstanceID = "gamma";
                    break;
                case "duplicate":
                    second.InstanceID = first.InstanceID;
                    break;
                case "null":
                    factions[1] = null;
                    break;
            }
            Assert.IsTrue(_planet.IsBlockaded());

            factions.Clear();
            first.InstanceID = "alpha";
            second.InstanceID = "beta";
            factions.Add(first);
            factions.Add(second);
            Assert.IsFalse(_planet.IsBlockaded());
        }

        [TestCase("alpha", "gamma")]
        [TestCase("alpha", "alpha")]
        [TestCase("alpha", "")]
        public void IsBlockaded_ConfiguredFactionPairDoesNotMatch_UsesFleetPredicate(
            string first,
            string second
        )
        {
            _game.ConfigureSpaceCombatEntries(new[] { first, second });

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_LiveFactionOrderChanges_RetainsConfiguredPair()
        {
            _game.GetFactions().Reverse();

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_OwnerOutsideCurrentFactionPair_UsesExistingFleetPredicate()
        {
            _planet.OwnerInstanceID = "unknown-owner";

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_DetachedRecursiveCopy_DoesNotRetainLiveGameBinding()
        {
            Planet copy = (Planet)_planet.CreateCopy(recursive: true);

            Assert.IsTrue(copy.IsBlockaded());
            Assert.IsFalse(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_DetachedThenRegisteredPlanet_RebindsOnlyAuthoritativeInstance()
        {
            PlanetSector parent = _planet.GetParentOfType<PlanetSector>();
            _game.DetachNode(_planet);
            Assert.IsTrue(_planet.IsBlockaded());

            _game.AttachNode(_planet, parent);

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_ReplacedGalaxy_ReleasesPreviousPlanetBinding()
        {
            _game.Galaxy = new GalaxyMap();

            Assert.IsTrue(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_RebuiltSceneState_RetainsLiveBinding()
        {
            _game.RebuildSceneState();

            Assert.IsFalse(_planet.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_PlanetRegisteredAfterSession_BindsCurrentRules()
        {
            Planet next = new()
            {
                InstanceID = "next",
                OwnerInstanceID = "alpha",
                IsColonized = true,
            };
            _game.AttachNode(next, _planet.GetParent());
            _game.MoveNode(_enemyFleet, next);
            _game.MoveNode(_fighter, next);

            Assert.IsFalse(next.IsBlockaded());
        }

        [Test]
        public void IsBlockaded_RestoredCampaign_RebindsRulesWithoutPersistingLiveContext()
        {
            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(_game)
            );
            Planet restoredPlanet = restored.GetSceneNodeByInstanceID<Planet>(_planet.InstanceID);
            Assert.IsTrue(restoredPlanet.IsBlockaded());

            using GameSession runtime = new(restored, _catalog);

            Assert.IsFalse(restoredPlanet.IsBlockaded());
        }

        /// <summary>
        /// Adds an empty, stationary fleet to the fixture planet.
        /// </summary>
        /// <param name="owner">The owning faction identity.</param>
        /// <returns>The registered fleet.</returns>
        private Fleet AddFleet(string owner)
        {
            Fleet fleet = new() { InstanceID = $"fleet-{++_nextID}", OwnerInstanceID = owner };
            _game.AttachNode(fleet, _planet);
            return fleet;
        }

        /// <summary>
        /// Adds a completed capital ship to its same-owner fleet.
        /// </summary>
        /// <param name="fleet">The fleet receiving the ship.</param>
        /// <returns>The registered ship.</returns>
        private CapitalShip AddCapital(Fleet fleet)
        {
            CapitalShip ship = new()
            {
                InstanceID = $"ship-{++_nextID}",
                OwnerInstanceID = fleet.OwnerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                StarfighterCapacity = 1,
            };
            _game.AttachNode(ship, fleet);
            return ship;
        }
    }
}
