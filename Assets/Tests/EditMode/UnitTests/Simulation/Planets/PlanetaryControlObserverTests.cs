using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

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
            _game = TestGame.Create(TestConfig.Create());

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

            MovementQueries movementQueries = new MovementQueries(_game);
            _movementSystem = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                movementQueries
            );
            PlanetaryControlQueries controlQueries = new PlanetaryControlQueries(_game);
            _commands = new PlanetaryControlCommands(
                _game,
                controlQueries,
                new FogOfWarQueries(_game)
            );
            _observer = new PlanetaryControlObserver(
                _game,
                _commands,
                _movementSystem,
                controlQueries
            );
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
        public void HandleResults_CoreSinglePointSupportShift_AppliesShiftAndReconcilesOwnership()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _game.ChangeOwnership(_targetPlanet, _rebels.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 60 },
                { _rebels.InstanceID, 40 },
            };
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

            Assert.AreEqual(61, _targetPlanet.GetPopularSupport(_empire.InstanceID));
            Assert.AreEqual(_empire.InstanceID, _targetPlanet.OwnerInstanceID);
            Assert.AreEqual(2, results.Count);
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
            IReadOnlyList<GameResult> movementResults = new MovementTickProcessor(
                _movementSystem
            ).ProcessTick(_game);
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
            Assert.AreEqual(PlanetOwnershipChangeReason.GarrisonRemoved, change.Reason);
            Assert.IsEmpty(
                new PlanetaryControlTickProcessor(_commands)
                    .ProcessTick(_game)
                    .OfType<PlanetOwnershipChangedResult>()
                    .Where(result => result.Planet == _targetPlanet)
            );
        }

        [Test]
        public void HandleResults_LastOuterRimGarrisonRemoved_ReroutesAllInboundUnitsToNearestFleet()
        {
            (Regiment garrison, List<IMovable> inboundUnits) = BuildOuterRimGarrisonRemovalScene(
                unitsPerType: 2
            );
            (_, Fleet fleet, CapitalShip carrier) = AddNearbyCarrier(
                "nearest",
                positionX: 35,
                starfighterCapacity: 2,
                regimentCapacity: 2
            );

            RemoveGarrisonAndReconcile(garrison);

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.GetOwnerInstanceID());
            Assert.IsTrue(inboundUnits.All(unit => unit.GetParentOfType<Fleet>() == fleet));
            Assert.IsTrue(inboundUnits.All(unit => unit.GetParent() == carrier));
        }

        [Test]
        public void HandleResults_NearestFleetHasPartialCapacity_ReroutesOverflowToNextPlanet()
        {
            (Regiment garrison, List<IMovable> inboundUnits) = BuildOuterRimGarrisonRemovalScene(
                unitsPerType: 2
            );
            (_, _, CapitalShip carrier) = AddNearbyCarrier(
                "nearest",
                positionX: 35,
                starfighterCapacity: 1,
                regimentCapacity: 1
            );

            RemoveGarrisonAndReconcile(garrison);

            Assert.AreEqual(
                1,
                inboundUnits.OfType<Starfighter>().Count(unit => unit.GetParent() == carrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Regiment>().Count(unit => unit.GetParent() == carrier)
            );
            Assert.AreEqual(
                2,
                inboundUnits.OfType<Officer>().Count(unit => unit.GetParent() == carrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Starfighter>().Count(unit => unit.GetParent() == _empirePlanet)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Regiment>().Count(unit => unit.GetParent() == _empirePlanet)
            );
        }

        [Test]
        public void HandleResults_TwoNearbyFleetsHavePartialCapacity_DistributesOverflowByProximity()
        {
            (Regiment garrison, List<IMovable> inboundUnits) = BuildOuterRimGarrisonRemovalScene(
                unitsPerType: 3
            );
            (_, _, CapitalShip nearestCarrier) = AddNearbyCarrier(
                "nearest",
                positionX: 35,
                starfighterCapacity: 1,
                regimentCapacity: 1
            );
            (_, _, CapitalShip secondCarrier) = AddNearbyCarrier(
                "second",
                positionX: 45,
                starfighterCapacity: 1,
                regimentCapacity: 1
            );

            RemoveGarrisonAndReconcile(garrison);

            Assert.AreEqual(
                1,
                inboundUnits.OfType<Starfighter>().Count(unit => unit.GetParent() == nearestCarrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Regiment>().Count(unit => unit.GetParent() == nearestCarrier)
            );
            Assert.AreEqual(
                3,
                inboundUnits.OfType<Officer>().Count(unit => unit.GetParent() == nearestCarrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Starfighter>().Count(unit => unit.GetParent() == secondCarrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Regiment>().Count(unit => unit.GetParent() == secondCarrier)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Starfighter>().Count(unit => unit.GetParent() == _empirePlanet)
            );
            Assert.AreEqual(
                1,
                inboundUnits.OfType<Regiment>().Count(unit => unit.GetParent() == _empirePlanet)
            );
        }

        [Test]
        public void HandleResults_LastOuterRimGarrisonRemoved_SkipsBlockadedDestination()
        {
            (Regiment garrison, List<IMovable> inboundUnits) = BuildOuterRimGarrisonRemovalScene(
                unitsPerType: 1
            );
            Planet blockadedPlanet = AddEmpirePlanet("blockaded", positionX: 35);
            AddBlockadingFleet(blockadedPlanet, "blockading");

            RemoveGarrisonAndReconcile(garrison);

            Assert.IsTrue(blockadedPlanet.IsBlockaded());
            Assert.IsTrue(inboundUnits.All(unit => unit.GetParent() == _empirePlanet));
        }

        [Test]
        public void HandleResults_LastOuterRimGarrisonRemovedWithNoSafeDestination_CapturesOfficerAndDestroysCombatUnits()
        {
            (Regiment garrison, List<IMovable> inboundUnits) = BuildOuterRimGarrisonRemovalScene(
                unitsPerType: 1
            );
            AddBlockadingFleet(_empirePlanet, "blockading");
            Officer officer = inboundUnits.OfType<Officer>().Single();
            Starfighter starfighter = inboundUnits.OfType<Starfighter>().Single();
            Regiment regiment = inboundUnits.OfType<Regiment>().Single();

            RemoveGarrisonAndReconcile(garrison);

            Assert.IsTrue(_empirePlanet.IsBlockaded());
            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual(_rebels.InstanceID, officer.CaptorInstanceID);
            Assert.AreSame(_targetPlanet, officer.GetParent());
            Assert.IsNull(
                _game.GetSceneNodeByInstanceID<Starfighter>(starfighter.InstanceID, true)
            );
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID, true));
        }

        [Test]
        public void HandleResults_BlockadedOwnershipChange_ResolvesEvacuationAgainstPriorBlockade()
        {
            _game.Config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            AddBlockadingFleet(_targetPlanet, "capturing");
            Regiment regiment = EntityFactory.CreateRegiment("evacuating", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            MovementQueries movementQueries = new MovementQueries(_game);
            MovementCommands movement = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                movementQueries,
                new FixedRNG()
            );
            PlanetaryControlObserver observer = new PlanetaryControlObserver(
                _game,
                _commands,
                movement,
                new PlanetaryControlQueries(_game)
            );

            PlanetOwnershipChangedResult change = _commands.TransferPlanet(_targetPlanet, _rebels);
            List<GameResult> reactions = observer.HandleResults(new[] { change });

            CollectionAssert.Contains(change.BlockadedFactionInstanceIDs, _empire.InstanceID);
            Assert.IsFalse(_targetPlanet.IsBlockaded());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID, true));
            Assert.AreSame(
                regiment,
                reactions.OfType<EvacuationLossesResult>().Single().LostRegiments.Single()
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
            IReadOnlyList<GameResult> movementResults = new MovementTickProcessor(
                _movementSystem
            ).ProcessTick(_game);
            _observer.HandleResults(movementResults.OfType<PlanetGarrisonChangedResult>().ToList());

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.GetOwnerInstanceID());
            Assert.AreSame(_targetPlanet, diplomacyMission.GetParent());
            Assert.IsNull(officer.Movement);
            Assert.AreSame(diplomacyMission, officer.GetParent());
        }

        [Test]
        public void HandleResults_CorePopularSupportShift_AppliesFullShiftAndReportsChange()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 50 },
                { _rebels.InstanceID, 50 },
            };
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

            Assert.AreEqual(56, _targetPlanet.GetPopularSupport(_empire.InstanceID));
            PlanetStatChangedResult change = reactions.OfType<PlanetStatChangedResult>().Single();
            Assert.AreEqual(PlanetChangeCategory.Loyalty, change.Category);
            Assert.AreEqual(50, change.OldValue);
            Assert.AreEqual(56, change.NewValue);
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

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        public void HandleResults_EnergyInfrastructureDamage_AppliesCivilianBombardmentPenalty(
            int energyCapacityDamage,
            int allocatedEnergyDamage
        )
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 50 },
                { _empire.InstanceID, 50 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 20 },
                { _empire.InstanceID, 80 },
            };
            _game.Config.Combat.Bombardment.CivilianSupportPenalty = -20;
            _rebels.Settings.CivilianBombardmentOuterRimSupportPenalty = -2;
            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);

            _observer.HandleResults(
                new[]
                {
                    new BombardmentResult
                    {
                        Planet = _targetPlanet,
                        AttackingFaction = _rebels,
                        EnergyCapacityDamage = energyCapacityDamage,
                        AllocatedEnergyDamage = allocatedEnergyDamage,
                    },
                }
            );

            Assert.AreEqual(28, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(18, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
        }

        [Test]
        public void Connect_PopularSupportTransfer_ShiftsSectorSupport()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 59 },
                { _empire.InstanceID, 41 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 20 },
                { _empire.InstanceID, 80 },
            };
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new PopularSupportShiftResult
                {
                    Planet = _targetPlanet,
                    Faction = _rebels,
                    Shift = 1,
                    Tick = 10,
                }
            );

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.OwnerInstanceID);
            Assert.AreEqual(61, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(21, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            CollectionAssert.AreEqual(
                new[] { _targetPlanet },
                results.OfType<PlanetOwnershipChangedResult>().Select(result => result.Planet)
            );
        }

        [Test]
        public void Connect_PlanetFullySupportsDisplacedFaction_PreservesSupport()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 59 },
                { _empire.InstanceID, 41 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 0 },
                { _empire.InstanceID, 100 },
            };
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            bus.Publish(
                new PopularSupportShiftResult
                {
                    Planet = _targetPlanet,
                    Faction = _rebels,
                    Shift = 1,
                    Tick = 10,
                }
            );

            Assert.AreEqual(0, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(100, _empirePlanet.GetPopularSupport(_empire.InstanceID));
        }

        [Test]
        public void Connect_NeutralTransfers_CascadesSectorSupport()
        {
            PlanetSector sector = _targetPlanet.GetParentOfType<PlanetSector>();
            sector.SectorType = PlanetSectorType.OuterRim;
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 59 },
                { _empire.InstanceID, 41 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 20 },
                { _empire.InstanceID, 80 },
            };
            Planet secondNeutral = new Planet
            {
                InstanceID = "second-neutral",
                DisplayName = "Second neutral",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { _rebels.InstanceID, 59 },
                    { _empire.InstanceID, 41 },
                },
            };
            _game.AttachNode(secondNeutral, sector);
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new PopularSupportShiftResult
                {
                    Planet = _targetPlanet,
                    Faction = _rebels,
                    Shift = 1,
                    Tick = 10,
                }
            );

            Assert.AreEqual(_rebels.InstanceID, secondNeutral.OwnerInstanceID);
            Assert.AreEqual(62, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(61, secondNeutral.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(22, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            CollectionAssert.AreEqual(
                new[] { _targetPlanet, secondNeutral },
                results.OfType<PlanetOwnershipChangedResult>().Select(result => result.Planet)
            );
        }

        [Test]
        public void Connect_OwnedTransfers_ShiftsOnceForDisplacedFaction()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 59 },
                { _empire.InstanceID, 41 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 59 },
                { _empire.InstanceID, 41 },
            };
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new PopularSupportShiftResult
                {
                    Planet = _targetPlanet,
                    Faction = _rebels,
                    Shift = 1,
                    Tick = 10,
                }
            );

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.OwnerInstanceID);
            Assert.AreEqual(_rebels.InstanceID, _empirePlanet.OwnerInstanceID);
            Assert.AreEqual(62, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(61, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(2, results.OfType<PlanetOwnershipChangedResult>().Count());
        }

        [Test]
        public void Connect_GarrisonRemoved_ShiftsSectorByTen()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 60 },
                { _empire.InstanceID, 40 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 0 },
                { _empire.InstanceID, 100 },
            };
            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);
            _game.DetachNode(regiment);
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new PlanetGarrisonChangedResult { Planet = _targetPlanet, Tick = 10 }
            );

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.OwnerInstanceID);
            Assert.AreEqual(70, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(10, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            PlanetOwnershipChangedResult change = results
                .OfType<PlanetOwnershipChangedResult>()
                .Single();
            Assert.AreEqual(PlanetOwnershipChangeReason.GarrisonRemoved, change.Reason);
        }

        [Test]
        public void Connect_CoreGarrisonRemoved_AppliesFullSectorShift()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.Core;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 60 },
                { _empire.InstanceID, 40 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 0 },
                { _empire.InstanceID, 100 },
            };
            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);
            _game.DetachNode(regiment);
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            bus.Publish(new PlanetGarrisonChangedResult { Planet = _targetPlanet, Tick = 10 });

            Assert.AreEqual(70, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(10, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
        }

        [Test]
        public void Connect_GarrisonRemovalCreatesNeutrality_DrainsResultingTransfer()
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 50 },
                { _empire.InstanceID, 50 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 20 },
                { _empire.InstanceID, 80 },
            };
            Regiment regiment = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, _targetPlanet);
            _game.DetachNode(regiment);
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            List<GameResult> results = bus.Publish(
                new PlanetGarrisonChangedResult { Planet = _targetPlanet, Tick = 10 }
            );

            Assert.AreEqual(_rebels.InstanceID, _targetPlanet.OwnerInstanceID);
            Assert.AreEqual(61, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(31, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
            CollectionAssert.AreEqual(
                new[]
                {
                    PlanetOwnershipChangeReason.GarrisonRemoved,
                    PlanetOwnershipChangeReason.PopularSupport,
                },
                results.OfType<PlanetOwnershipChangedResult>().Select(result => result.Reason)
            );
        }

        [Test]
        public void Connect_UnclassifiedOwnershipChange_DoesNotShiftSectorSupport()
        {
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 60 },
                { _empire.InstanceID, 40 },
            };
            _empirePlanet.PopularSupport = new Dictionary<string, int>
            {
                { _rebels.InstanceID, 20 },
                { _empire.InstanceID, 80 },
            };
            GameResultBus bus = new GameResultBus();
            _observer.Connect(bus);

            bus.Publish(
                new PlanetOwnershipChangedResult
                {
                    Planet = _targetPlanet,
                    PreviousOwner = _empire,
                    NewOwner = _rebels,
                    Reason = PlanetOwnershipChangeReason.None,
                }
            );

            Assert.AreEqual(60, _targetPlanet.GetPopularSupport(_rebels.InstanceID));
            Assert.AreEqual(20, _empirePlanet.GetPopularSupport(_rebels.InstanceID));
        }

        /// <summary>
        /// Builds an outer-rim planet with one stationed regiment and equal groups of inbound
        /// starfighters, officers, and regiments owned by the planet's current faction.
        /// </summary>
        /// <param name="unitsPerType">The number of inbound units to create for each unit type.</param>
        /// <returns>The stationed garrison and all inbound units.</returns>
        private (Regiment garrison, List<IMovable> inboundUnits) BuildOuterRimGarrisonRemovalScene(
            int unitsPerType
        )
        {
            _targetPlanet.GetParentOfType<PlanetSector>().SectorType = PlanetSectorType.OuterRim;
            _game.ChangeOwnership(_targetPlanet, _empire.InstanceID);
            _targetPlanet.PopularSupport = new Dictionary<string, int>
            {
                { _empire.InstanceID, 40 },
                { _rebels.InstanceID, 60 },
            };
            _empirePlanet.PositionX = 100;

            Regiment garrison = EntityFactory.CreateRegiment("garrison", _empire.InstanceID);
            garrison.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(garrison, _targetPlanet);

            List<IMovable> inboundUnits = new List<IMovable>();
            for (int index = 0; index < unitsPerType; index++)
            {
                Starfighter starfighter = EntityFactory.CreateStarfighter(
                    $"inbound-fighter-{index}",
                    _empire.InstanceID
                );
                starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
                starfighter.Hyperdrive = 1;
                Officer officer = EntityFactory.CreateOfficer(
                    $"inbound-officer-{index}",
                    _empire.InstanceID
                );
                Regiment regiment = EntityFactory.CreateRegiment(
                    $"inbound-regiment-{index}",
                    _empire.InstanceID
                );
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;

                inboundUnits.Add(starfighter);
                inboundUnits.Add(officer);
                inboundUnits.Add(regiment);
            }

            foreach (IMovable unit in inboundUnits)
            {
                _game.AttachNode(unit, _empirePlanet);
                _movementSystem.RequestMove(unit, _targetPlanet);
                unit.Movement.CurrentPosition = new Point(30, 0);
            }

            return (garrison, inboundUnits);
        }

        /// <summary>
        /// Adds a stationary friendly carrier at a neutral nearby planet so the fleet, rather
        /// than its host planet, is the valid receiving destination.
        /// </summary>
        /// <param name="id">The identifier prefix for the new scene nodes.</param>
        /// <param name="positionX">The carrier planet's horizontal position.</param>
        /// <param name="starfighterCapacity">The carrier's starfighter capacity.</param>
        /// <param name="regimentCapacity">The carrier's regiment capacity.</param>
        /// <returns>The carrier location, fleet, and capital ship.</returns>
        private (Planet planet, Fleet fleet, CapitalShip carrier) AddNearbyCarrier(
            string id,
            int positionX,
            int starfighterCapacity,
            int regimentCapacity
        )
        {
            Planet planet = new Planet
            {
                InstanceID = $"{id}-carrier-location",
                DisplayName = $"{id} carrier location",
                IsColonized = true,
                PositionX = positionX,
                PositionY = 0,
            };
            Fleet fleet = EntityFactory.CreateFleet($"{id}-fleet", _empire.InstanceID);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = $"{id}-carrier",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Hyperdrive = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                StarfighterCapacity = starfighterCapacity,
                RegimentCapacity = regimentCapacity,
            };

            _game.AttachNode(planet, _targetPlanet.GetParentOfType<PlanetSector>());
            _game.AttachNode(fleet, planet);
            _game.AttachNode(carrier, fleet);
            return (planet, fleet, carrier);
        }

        /// <summary>
        /// Adds a colonized Empire planet used as a relocation destination.
        /// </summary>
        /// <param name="id">The planet identifier prefix.</param>
        /// <param name="positionX">The planet's horizontal position.</param>
        /// <returns>The added Empire planet.</returns>
        private Planet AddEmpirePlanet(string id, int positionX)
        {
            Planet planet = new Planet
            {
                InstanceID = $"{id}-planet",
                DisplayName = $"{id} planet",
                OwnerInstanceID = _empire.InstanceID,
                IsColonized = true,
                PositionX = positionX,
                PositionY = 0,
            };
            _game.AttachNode(planet, _targetPlanet.GetParentOfType<PlanetSector>());
            return planet;
        }

        /// <summary>
        /// Adds a stationary Rebels fleet with an operational capital ship above a planet.
        /// </summary>
        /// <param name="planet">The planet to blockade.</param>
        /// <param name="id">The fleet identifier prefix.</param>
        private void AddBlockadingFleet(Planet planet, string id)
        {
            Fleet fleet = EntityFactory.CreateFleet($"{id}-fleet", _rebels.InstanceID);
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = $"{id}-capital-ship",
                OwnerInstanceID = _rebels.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrive = 1,
            };
            _game.AttachNode(fleet, planet);
            _game.AttachNode(capitalShip, fleet);
        }

        /// <summary>
        /// Removes the planet's last stationed regiment and delivers the resulting garrison
        /// change to planetary control reconciliation.
        /// </summary>
        /// <param name="garrison">The last stationed regiment to remove.</param>
        private void RemoveGarrisonAndReconcile(Regiment garrison)
        {
            _game.DeleteNode(garrison);
            List<GameResult> results = _observer.HandleResults(
                new[]
                {
                    new PlanetGarrisonChangedResult
                    {
                        Planet = _targetPlanet,
                        Tick = _game.CurrentTick,
                    },
                }
            );
            _observer.HandleResults(results.OfType<PlanetOwnershipChangedResult>().ToList());
        }
    }
}
