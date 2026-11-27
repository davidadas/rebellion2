using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class BlockadeQueriesTests
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
            _game.Summary.StartingFactionIDs = new[] { "alpha", "beta" };
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
        public void GetWithdrawalChance_MixedOrbitalReadiness_CountsOnlyPresentUnits()
        {
            _game.Config.Blockade.CapitalShipWithdrawalPenaltyPercent = 7;
            _game.Config.Blockade.FighterWithdrawalPenaltyPercent = 3;
            _enemyShip.StarfighterCapacity = 2;
            for (int index = 0; index < 2; index++)
                _game.AttachNode(
                    new Starfighter
                    {
                        InstanceID = "carried-" + index,
                        OwnerInstanceID = "beta",
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    _enemyShip
                );
            CapitalShip moving = AddCapital(_enemyFleet);
            moving.Movement = TestMovement.Create(transitTicks: 10);
            AddCapital(_enemyFleet).ManufacturingStatus = ManufacturingStatus.Building;
            Fleet departing = AddFleet("beta");
            AddCapital(departing);
            departing.Movement = TestMovement.Create(transitTicks: 10);
            CapitalShip disabled = AddCapital(_enemyFleet);
            disabled.IsEnabled = false;
            _game.AttachNode(
                new Starfighter
                {
                    InstanceID = "inbound",
                    OwnerInstanceID = "alpha",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = TestMovement.Create(transitTicks: 10),
                },
                _planet
            );

            Assert.AreEqual(84, BlockadeQueries.GetWithdrawalChance(_game, _planet));
        }

        [Test]
        public void GetWithdrawalChance_OperationalIonCannon_ReturnsFullSurvival()
        {
            _planet.EnergyCapacity = 1;
            _game.AttachNode(
                new Building
                {
                    InstanceID = "ion-cannon",
                    OwnerInstanceID = "alpha",
                    BuildingType = BuildingType.Weapon,
                    DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );

            Assert.AreEqual(100, BlockadeQueries.GetWithdrawalChance(_game, _planet));
        }

        [Test]
        public void GetWithdrawalChance_HeavyBlockade_ClampsSurvivalToZero()
        {
            _game.Config.Blockade.CapitalShipWithdrawalPenaltyPercent = 100;

            Assert.AreEqual(0, BlockadeQueries.GetWithdrawalChance(_game, _planet));
        }

        [Test]
        public void IsBlockaded_ReadyDirectFriendlyFighter_SuppressesHostileFleet()
        {
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
            Assert.IsFalse(BlockadeQueries.IsBlockadedFor(_game, _planet, "alpha"));
        }

        [Test]
        public void IsBlockaded_OnlyDirectHostileFighter_DoesNotEstablishBlockade()
        {
            _game.DeleteNode(_enemyFleet);
            _game.ChangeOwnership(_fighter, "beta");

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IsBlockaded_NeutralPlanet_RequiresPopulationAndOneOrbitalSide(bool populated)
        {
            _game.ChangeOwnership(_planet, null);
            _planet.IsColonized = populated;
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));

            _game.DeleteNode(_fighter);

            Assert.AreEqual(populated, BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_PopulatedNeutralWithBothReadyFleets_ReturnsFalse()
        {
            _game.ChangeOwnership(_planet, null);
            _game.DeleteNode(_fighter);
            AddCapital(AddFleet("alpha"));

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
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

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
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

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
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

            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_FighterInsideDepartingCarrier_DoesNotCountAsDirectPresence()
        {
            Fleet ownFleet = AddFleet("alpha");
            CapitalShip carrier = AddCapital(ownFleet);
            ownFleet.Movement = new MovementState();
            _game.MoveNode(_fighter, carrier);

            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_ZeroHullAndSquadronPools_RetainsReadinessUntilRemoval()
        {
            _enemyShip.CurrentHullStrength = 0;
            _fighter.CurrentSquadronSize = 0;
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));

            _game.DeleteNode(_fighter);

            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_FighterDepartsAndReturns_ReadsCurrentOrbit()
        {
            _fighter.Movement = new MovementState();
            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));

            _fighter.Movement = null;
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
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
            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));

            factions.Clear();
            first.InstanceID = "alpha";
            second.InstanceID = "beta";
            factions.Add(first);
            factions.Add(second);
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [TestCase("alpha", "gamma")]
        [TestCase("alpha", "alpha")]
        [TestCase("alpha", "")]
        public void IsBlockaded_ConfiguredFactionPairDoesNotMatch_UsesFleetPredicate(
            string first,
            string second
        )
        {
            _game.Summary.StartingFactionIDs = new[] { first, second };

            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_LiveFactionOrderChanges_RetainsConfiguredPair()
        {
            _game.GetFactions().Reverse();

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_OwnerOutsideCurrentFactionPair_UsesExistingFleetPredicate()
        {
            _planet.OwnerInstanceID = "unknown-owner";

            Assert.IsTrue(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_DetachedRecursiveCopy_UsesTheSameCampaignRules()
        {
            Planet copy = (Planet)_planet.CreateCopy(recursive: true);

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, copy));
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_DetachedThenRegisteredPlanet_UsesTheSameCampaignRules()
        {
            PlanetSector parent = _planet.GetParentOfType<PlanetSector>();
            _game.DetachNode(_planet);
            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));

            _game.AttachNode(_planet, parent);

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_ReplacedGalaxy_UsesTheSameCampaignRulesForOldPlanet()
        {
            _game.Galaxy = new GalaxyMap();

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_RebuiltSceneState_RetainsCampaignRules()
        {
            _game.RebuildSceneState();

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, _planet));
        }

        [Test]
        public void IsBlockaded_PlanetRegisteredAfterSession_UsesCampaignRules()
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

            Assert.IsFalse(BlockadeQueries.IsBlockaded(_game, next));
        }

        [Test]
        public void IsBlockaded_RestoredCampaign_UsesCampaignRulesBeforeAndAfterSessionStarts()
        {
            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(_game)
            );
            Planet restoredPlanet = restored.GetSceneNodeByInstanceID<Planet>(_planet.InstanceID);
            Assert.IsFalse(BlockadeQueries.IsBlockaded(restored, restoredPlanet));

            using GameSession runtime = new(restored, _catalog);

            Assert.IsFalse(BlockadeQueries.IsBlockaded(restored, restoredPlanet));
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
