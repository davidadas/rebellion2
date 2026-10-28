using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting.Events;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Generation;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class FactionAutomationObserverTests
    {
        private const string _factionId = "faction";

        private GameRoot _game;
        private Faction _faction;
        private Planet _planet;
        private Building _mineTemplate;
        private ManufacturingCommands _manufacturing;
        private ManufacturingObserver _manufacturingObserver;
        private FactionAutomationObserver _observer;

        /// <summary>
        /// Creates a faction, an owned planet, and the production services used by each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameConfig config = new GameConfig();
            GameDataCatalog gameData = CreateGameData(config);
            _mineTemplate = gameData.Buildings.Single(building => building.TypeID == "mine");
            _game = TestGame.Create(config);
            _faction = new Faction { InstanceID = _factionId, ManageProduction = true };
            _game.GetFactions().Add(_faction);

            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            _game.AttachNode(sector, _game.Galaxy);
            _planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = _factionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(_planet, sector);
            AddBuilding(
                "CONSTRUCTION",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AddBuilding("EXISTING_MINE", BuildingType.Mine);
            AddBuilding("EXISTING_REFINERY", BuildingType.Refinery);

            MovementCommands movement = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            _manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game),
                movement
            );
            FactionAutomationCommands automation = new FactionAutomationCommands(
                _game,
                gameData,
                _manufacturing,
                new GarrisonAutomationCommands(_game, gameData, _manufacturing)
            );
            _manufacturingObserver = new ManufacturingObserver(_game, _manufacturing);
            _observer = new FactionAutomationObserver(automation);
        }

        [Test]
        public void HandleResults_NewFinalOwnerWithProductionEnabled_QueuesWorkImmediately()
        {
            _observer.HandleResults(new[] { CreateOwnershipResult(_faction) });

            Assert.AreEqual(1, GetBuildingQueue().Count);
        }

        [Test]
        public void Connect_OwnershipResult_CleansCapturedQueueBeforeQueuingNewWork()
        {
            Assert.IsTrue(
                _manufacturing.StartManufacturing(_planet, _mineTemplate, _planet, 1, _factionId)
            );
            Building capturedOrder = (Building)GetBuildingQueue().Single();
            GameResultBus results = new GameResultBus();
            _manufacturingObserver.Connect(results);
            _observer.Connect(results);

            results.Publish(CreateOwnershipResult(_faction));

            Assert.IsNull(capturedOrder.GetParent());
            Assert.AreEqual(1, GetBuildingQueue().Count);
            Assert.AreNotSame(capturedOrder, GetBuildingQueue().Single());
        }

        [Test]
        public void HandleResults_SupersededNewOwner_DoesNotRunAutomation()
        {
            Faction supersededOwner = new Faction
            {
                InstanceID = "superseded",
                ManageProduction = true,
            };

            _observer.HandleResults(new[] { CreateOwnershipResult(supersededOwner) });

            Assert.IsEmpty(GetBuildingQueue());
        }

        [Test]
        public void HandleResults_ProductionDisabled_DoesNotQueueWork()
        {
            _faction.ManageProduction = false;

            _observer.HandleResults(new[] { CreateOwnershipResult(_faction) });

            Assert.IsEmpty(GetBuildingQueue());
        }

        /// <summary>
        /// Adds a completed building to the test planet.
        /// </summary>
        /// <param name="instanceId">The building instance identifier.</param>
        /// <param name="buildingType">The building's strategic type.</param>
        /// <param name="productionType">The manufacturing category provided by the building.</param>
        private void AddBuilding(
            string instanceId,
            BuildingType buildingType,
            ManufacturingType productionType = ManufacturingType.None
        )
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _factionId,
                    BuildingType = buildingType,
                    ProductionType = productionType,
                    ProcessRate = productionType == ManufacturingType.None ? 0 : 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _planet
            );
        }

        /// <summary>
        /// Creates the minimum game-data catalog required by faction production automation.
        /// </summary>
        /// <param name="config">The game configuration shared with the test game.</param>
        /// <returns>The configured game-data catalog.</returns>
        private static GameDataCatalog CreateGameData(GameConfig config)
        {
            List<string> manufacturingFactionIds = new List<string> { _factionId };
            Building mine = new Building
            {
                TypeID = "mine",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Building refinery = new Building
            {
                TypeID = "refinery",
                BuildingType = BuildingType.Refinery,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            return new GameDataCatalog(
                config,
                new GameGenerationConfig(),
                new[] { new Faction { InstanceID = _factionId } },
                Array.Empty<PlanetSector>(),
                new[] { mine, refinery },
                Array.Empty<CapitalShip>(),
                Array.Empty<Starfighter>(),
                Array.Empty<Regiment>(),
                Array.Empty<SpecialForces>(),
                Array.Empty<Officer>(),
                Array.Empty<GameEvent>(),
                Array.Empty<MessageDefinition>(),
                new EncyclopediaEntries(),
                new FactionThemes()
            );
        }

        /// <summary>
        /// Creates an ownership result for the test planet.
        /// </summary>
        /// <param name="newOwner">The owner recorded by the result.</param>
        /// <returns>The ownership-change result.</returns>
        private PlanetOwnershipChangedResult CreateOwnershipResult(Faction newOwner)
        {
            return new PlanetOwnershipChangedResult
            {
                Planet = _planet,
                NewOwner = newOwner,
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Gets the test planet's building manufacturing queue.
        /// </summary>
        /// <returns>The existing queue, or an empty list when no queue exists.</returns>
        private List<IManufacturable> GetBuildingQueue()
        {
            return _planet
                .GetManufacturingQueue()
                .TryGetValue(ManufacturingType.Building, out List<IManufacturable> queue)
                ? queue
                : new List<IManufacturable>();
        }
    }
}
