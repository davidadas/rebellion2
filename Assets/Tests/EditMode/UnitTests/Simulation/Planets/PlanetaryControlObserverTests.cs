using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class PlanetaryControlObserverTests
    {
        private GameRoot _game;
        private Faction _rebels;
        private Faction _empire;
        private Planet _targetPlanet;
        private Planet _empirePlanet;
        private MovementCommands _movementSystem;
        private PlanetaryControlCommands _commands;
        private PlanetaryControlObserver _observer;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());

            _rebels = new Faction { InstanceID = "rebels", DisplayName = "Rebels" };
            _empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            _game.GetFactions().Add(_rebels);
            _game.GetFactions().Add(_empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

            // Planet being transferred — starts neutral
            _targetPlanet = new Planet
            {
                InstanceID = "target",
                DisplayName = "Target",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_targetPlanet, planetSector);

            // Empire's home planet — fallback destination for evicted units
            _empirePlanet = new Planet
            {
                InstanceID = "empire-home",
                DisplayName = "Empire Home",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
            };
            _game.AttachNode(_empirePlanet, planetSector);

            _movementSystem = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            _commands = new PlanetaryControlCommands(
                _game,
                _movementSystem,
                new ManufacturingCommands(
                    _game,
                    new FleetCommands(_game),
                    new ManufacturingQueries(_game)
                ),
                new FogOfWarCommands(_game),
                new PlanetaryControlQueries(_game),
                new FogOfWarQueries(_game)
            );
            _observer = new PlanetaryControlObserver(_commands);
        }

        [Test]
        public void HandleResults_SequentialSupportTransfers_PreservesResultOrderAndTicks()
        {
            _game.CurrentTick = 50;
            _game.Config.SupportShift.OwnershipTransferThreshold = 60;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 50 },
                { _rebels.InstanceID, 50 },
            };

            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new PopularSupportShiftResult
                    {
                        Planet = _targetPlanet,
                        Faction = _empire,
                        Shift = 20,
                        Tick = 4,
                    },
                    new PopularSupportShiftResult
                    {
                        Planet = _targetPlanet,
                        Faction = _empire,
                        Shift = -20,
                        Tick = 5,
                    },
                }
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    typeof(PlanetStatChangedResult),
                    typeof(PlanetOwnershipChangedResult),
                    typeof(PlanetStatChangedResult),
                    typeof(PlanetOwnershipChangedResult),
                },
                results.Select(result => result.GetType())
            );
            CollectionAssert.AreEqual(new[] { 4, 4, 5, 5 }, results.Select(result => result.Tick));
        }

        [Test]
        public void HandleResults_ResistanceEliminatesShift_DoesNotReconcileOwnership()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _game.ChangeOwnership(_targetPlanet, _rebels.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 60 },
                { _rebels.InstanceID, 40 },
            };
            _empire.Settings.SupportResistance = SupportChange.Increase;
            _game.Config.SupportShift.WeakSupportPenaltyDivisor = 2;

            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new PopularSupportShiftResult
                    {
                        Planet = _targetPlanet,
                        Faction = _empire,
                        Shift = 1,
                    },
                }
            );

            Assert.IsEmpty(results);
            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.OwnerInstanceID);
        }

        [Test]
        public void HandleResults_NullGarrisonEntryAfterChange_ThrowsAfterApplyingEarlierChange()
        {
            _targetPlanet.SetPopularSupport(_empire.InstanceID, 100);

            Assert.Throws<System.NullReferenceException>(() =>
                _observer.HandleResults(
                    new PlanetGarrisonChangedResult[]
                    {
                        new PlanetGarrisonChangedResult { Planet = _targetPlanet },
                        null,
                    }
                )
            );

            Assert.AreEqual(_empire.InstanceID, _targetPlanet.OwnerInstanceID);
        }

        [Test]
        public void HandleResults_NullGarrisonBatch_ReturnsNoReactions()
        {
            Assert.IsEmpty(
                _observer.HandleResults((IReadOnlyList<PlanetGarrisonChangedResult>)null)
            );
        }

        [Test]
        public void HandleResults_NullSupportBatch_ReturnsNoReactions()
        {
            Assert.IsEmpty(_observer.HandleResults((IReadOnlyList<PopularSupportShiftResult>)null));
        }

        [TestCase("empire", 60, "empire")]
        [TestCase("empire", 59, null)]
        [TestCase("rebels", 60, "rebels")]
        public void HandleResults_LastStationedRegiment_ReconcilesControl(
            string supportFactionId,
            int support,
            string expectedOwnerId
        )
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport.Clear();
            _targetPlanet.SetPopularSupport(supportFactionId, support);

            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            Fleet fleet = EntityFactory.CreateFleet("carrier-fleet", _empire.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);

            _movementSystem.RequestMove(regiment, ship);
            List<GameResult> movementResults = _movementSystem.ProcessTick();
            List<GameResult> controlResults = _observer.HandleResults(
                movementResults.OfType<PlanetGarrisonChangedResult>().ToList()
            );

            Assert.AreEqual(expectedOwnerId, _targetPlanet.GetOwnerInstanceID());
            Assert.IsTrue(
                movementResults
                    .OfType<PlanetGarrisonChangedResult>()
                    .Any(result => result.Planet == _targetPlanet)
            );

            List<PlanetOwnershipChangedResult> changes = controlResults
                .OfType<PlanetOwnershipChangedResult>()
                .Where(result => result.Planet == _targetPlanet)
                .ToList();

            if (expectedOwnerId == _empire.InstanceID)
            {
                Assert.IsEmpty(changes);
                return;
            }

            PlanetOwnershipChangedResult change = changes.Single();
            Assert.AreEqual(_empire, change.PreviousOwner);
            Assert.AreEqual(expectedOwnerId, change.NewOwner?.InstanceID);
            Assert.AreEqual(PlanetOwnershipChangeReason.PopularSupport, change.Reason);
            Assert.IsEmpty(
                _commands
                    .ProcessTick()
                    .OfType<PlanetOwnershipChangedResult>()
                    .Where(result => result.Planet == _targetPlanet)
            );
        }

        [Test]
        public void HandleResults_LastStationedRegiment_PreservesMissionForLifecycleValidation()
        {
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 40 },
                { _rebels.InstanceID, 60 },
            };
            _targetPlanet.AddVisitor(_empire.InstanceID);

            Officer officer = EntityFactory.CreateOfficer("diplomat", _empire.InstanceID);
            Mission diplomacyMission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                _game,
                _empire.InstanceID,
                _targetPlanet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            _game.AttachNode(diplomacyMission, _targetPlanet);
            _game.AttachNode(officer, diplomacyMission);

            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            Fleet fleet = EntityFactory.CreateFleet("carrier-fleet", _empire.InstanceID);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            _game.AttachNode(fleet, _targetPlanet);
            _game.AttachNode(ship, fleet);

            _movementSystem.RequestMove(regiment, ship);
            List<GameResult> movementResults = _movementSystem.ProcessTick();
            _observer.HandleResults(movementResults.OfType<PlanetGarrisonChangedResult>().ToList());

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.GetOwnerInstanceID());
            Assert.AreSame(_targetPlanet, diplomacyMission.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.AreSame(diplomacyMission, officer.GetParent());
        }

        [Test]
        public void HandleResults_CorePopularSupportShift_AppliesResistanceAndReportsChange()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 50 },
                { _rebels.InstanceID, 50 },
            };
            _empire.Settings.SupportResistance = SupportChange.Increase;
            _game.Config.SupportShift.WeakSupportPenaltyDivisor = 2;

            List<GameResult> reactions = _observer.HandleResults(
                new[]
                {
                    new PopularSupportShiftResult
                    {
                        Planet = _targetPlanet,
                        Faction = _empire,
                        Shift = 6,
                        Tick = 14,
                    },
                }
            );

            Assert.AreEqual(53, _targetPlanet.GetPopularSupport(_empire.InstanceID));
            PlanetStatChangedResult change = reactions.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(PlanetChangeCategory.Loyalty, change.Category);
            Assert.AreEqual(50, change.OldValue);
            Assert.AreEqual(53, change.NewValue);
            Assert.AreEqual(14, change.Tick);
        }

        [Test]
        public void HandleResults_SupportCrossesThreshold_ReportsPopularSupportOwnershipChange()
        {
            _game.Config.SupportShift.OwnershipTransferThreshold = 60;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 59 },
                { _rebels.InstanceID, 41 },
            };

            List<GameResult> reactions = _observer.HandleResults(
                new[]
                {
                    new PopularSupportShiftResult
                    {
                        Planet = _targetPlanet,
                        Faction = _empire,
                        Shift = 2,
                        Tick = 18,
                    },
                }
            );

            Assert.AreEqual(_empire.InstanceID, _targetPlanet.GetOwnerInstanceID());
            PlanetOwnershipChangedResult change = reactions
                .OfType<PlanetOwnershipChangedResult>()
                .Single();
            Assert.AreEqual(PlanetOwnershipChangeReason.PopularSupport, change.Reason);
            Assert.AreEqual(18, change.Tick);
        }
    }
}
