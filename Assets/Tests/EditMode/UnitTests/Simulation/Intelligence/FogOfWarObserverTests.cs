using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class FogOfWarObserverTests : FogOfWarTestBase
    {
        private FogOfWarObserver _observer;
        private GameResultBus _results;

        /// <summary>
        /// Creates the subject for the arranged faction scene.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _results = new GameResultBus();
            _observer = new FogOfWarObserver(_game, new FogOfWarCommands(_game));
        }

        [Test]
        public void Connect_SabotagedObject_RegistersSnapshotInvalidation()
        {
            _observer.Connect(_results);
            _coruscant.EnergyCapacity = 1;
            Building mine = CreateBuilding("MINE1", _empire);
            _game.AttachNode(mine, _coruscant);

            Officer han = CreateOfficer("HAN", _alliance);
            _game.AttachNode(han, _hoth);

            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(mine);

            _results.Publish(
                new GameObjectSabotagedResult
                {
                    DestroyedObject = mine,
                    DestroyedBy = han,
                    Context = _coruscant,
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.Buildings.Any(b => b.InstanceID == "MINE1"));
        }

        [Test]
        public void Dispose_IntelligenceResult_StopsRecordingObservations()
        {
            _observer.Connect(_results);
            _observer.Dispose();

            _results.Publish(
                new IntelligenceRevealedResult
                {
                    Recipient = _alliance,
                    Observations = new List<ISceneNode> { _coruscant },
                    Tick = 12,
                }
            );

            Assert.IsEmpty(_alliance.Fog.Snapshots);
        }

        [Test]
        public void HandleResults_ObservationForOneFaction_UpdatesOnlyRecipientAtResultTick()
        {
            _game.CurrentTick = 90;

            List<GameResult> reactions = _observer.HandleResults(
                new[]
                {
                    new IntelligenceRevealedResult
                    {
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { _coruscant },
                        Tick = 12,
                    },
                }
            );

            Assert.IsEmpty(reactions);
            Assert.IsEmpty(_empire.Fog.Snapshots);
            Assert.AreEqual(
                12,
                _alliance
                    .Fog
                    .Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .TickCaptured
            );
        }

        [Test]
        public void HandleResults_NullResultAfterValidObservation_KeepsEarlierSnapshotAndThrows()
        {
            Assert.Throws<System.NullReferenceException>(() =>
                _observer.HandleResults(
                    new IntelligenceRevealedResult[]
                    {
                        new IntelligenceRevealedResult
                        {
                            Recipient = _alliance,
                            Observations = new List<ISceneNode> { _coruscant },
                            Tick = 12,
                        },
                        null,
                        new IntelligenceRevealedResult
                        {
                            Recipient = _alliance,
                            Observations = new List<ISceneNode> { _tatooine },
                            Tick = 13,
                        },
                    }
                )
            );

            Assert.IsTrue(
                _alliance
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets.ContainsKey(_coruscant.InstanceID)
            );
            Assert.IsFalse(_alliance.Fog.Snapshots.ContainsKey(_outerRim.InstanceID));
        }

        [Test]
        public void ProcessResults_SabotageObservedByBothFactions_PreservesOtherFactionSnapshot()
        {
            Faction observer = new Faction { InstanceID = "OBSERVER" };
            _game.GetFactions().Add(observer);
            _coruscant.EnergyCapacity = 1;
            Building mine = CreateBuilding("MINE1", _empire);
            _game.AttachNode(mine, _coruscant);
            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            new FogOfWarRecorder().RecordPlanetSnapshot(observer, _coruscant, _coreSector, 10);

            _observer.ProcessResults(
                new[]
                {
                    new GameObjectSabotagedResult
                    {
                        DestroyedObject = mine,
                        DestroyedBy = CreateOfficer("SABOTEUR", _alliance),
                    },
                }
            );

            Assert.IsEmpty(
                _alliance
                    .Fog
                    .Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Buildings
            );
            Assert.AreEqual(
                "MINE1",
                observer
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Buildings.Single()
                    .InstanceID
            );
            Assert.AreSame(_coruscant, mine.GetParent());
        }

        [Test]
        public void HandleResults_SelectedObservation_RevealsOnlySelectedObject()
        {
            _coruscant.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "IMPERIAL_FACILITY",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officer = CreateOfficer("VADER", _empire);
            _game.AttachNode(building, _coruscant);
            _game.AttachNode(officer, _coruscant);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { building },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.AreEqual(42, snapshot.TickCaptured);
            Assert.AreEqual(PlanetIntelligenceCategory.None, snapshot.RevealedCategories);
            Assert.AreEqual("IMPERIAL_FACILITY", snapshot.Buildings.Single().InstanceID);
            Assert.IsEmpty(snapshot.Officers);
            Assert.AreNotEqual(PlanetIntelligenceCategory.All, snapshot.RevealedCategories);
        }

        [Test]
        public void HandleResults_SelectedCapitalShip_RevealsPartialFleetWithoutSiblingsOrCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip selectedShip = AddCapitalShip(fleet, _empire, "SELECTED_SHIP");
            AddCapitalShip(fleet, _empire, "HIDDEN_SHIP");
            _game.AttachNode(CreateOfficer("HIDDEN_OFFICER", _empire), selectedShip);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selectedShip },
                    },
                }
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            CapitalShip knownShip = knownFleet.GetChildren<CapitalShip>().Single();
            Assert.AreEqual("IMPERIAL_FLEET", knownFleet.InstanceID);
            Assert.AreEqual("SELECTED_SHIP", knownShip.InstanceID);
            Assert.IsEmpty(knownShip.GetChildren<Officer>());
        }

        [Test]
        public void HandleResults_SelectedNestedOfficer_RevealsAncestryWithoutSiblings()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            Officer selectedOfficer = CreateOfficer("SELECTED_OFFICER", _empire);
            _game.AttachNode(selectedOfficer, ship);
            _game.AttachNode(CreateOfficer("HIDDEN_OFFICER", _empire), ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selectedOfficer },
                    },
                }
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            CapitalShip knownShip = knownFleet.GetChildren<CapitalShip>().Single();
            Assert.AreEqual("IMPERIAL_FLEET", knownFleet.InstanceID);
            Assert.AreEqual("STAR_DESTROYER", knownShip.InstanceID);
            Assert.AreEqual(
                "SELECTED_OFFICER",
                knownShip.GetChildren<Officer>().Single().InstanceID
            );
        }

        [Test]
        public void HandleResults_SelectedManufacturingOrder_RevealsOnlySelectedOrder()
        {
            Building selected = AddQueuedBuilding(_coruscant, _empire, "SELECTED_ORDER", 25);
            AddQueuedBuilding(_coruscant, _empire, "HIDDEN_ORDER", 10);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selected },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsTrue(snapshot.HasManufacturingIntelligence);
            Assert.AreEqual("SELECTED_ORDER", snapshot.ManufacturingQueueItems.Single().InstanceID);
            Assert.IsFalse(
                snapshot.ManufacturingQueueItems.Any(item => item.InstanceID == "HIDDEN_ORDER")
            );
        }
    }
}
