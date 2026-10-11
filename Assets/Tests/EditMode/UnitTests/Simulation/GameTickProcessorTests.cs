using System;
using System.Collections;
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
    public sealed class GameTickProcessorTests
    {
        private GameRoot _game;
        private GameSession _session;
        private GameTickProcessor _tick;
        private Func<IEnumerable<GameResult>, bool, List<GameResult>> _processResults;

        /// <summary>
        /// Creates a prepared game and the runtime dependencies used by tick scheduling.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.SetGameSpeed(TickSpeed.Fast);
            _session = new GameSession(_game, TestGameData.Create(_game.Config));
            _processResults = (results, _) => _session.Results.Publish(results);
            _tick = CreateTickProcessor();
        }

        /// <summary>
        /// Releases the result subscriptions owned by the test runtime.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
        }

        [Test]
        public void ProcessTick_PausedGame_DoesNotAdvanceTick()
        {
            _game.SetGameSpeed(TickSpeed.Paused);

            _tick.ProcessTick();

            Assert.AreEqual(0, _game.CurrentTick);
        }

        [Test]
        public void ProcessTick_CompletedTick_NotifiesOnce()
        {
            int completions = 0;
            _tick.TickCompleted += () => completions++;

            _tick.ProcessTick();

            Assert.AreEqual(1, completions);
            Assert.AreEqual(1, _game.CurrentTick);
        }

        [Test]
        public void ProcessTickIncrementally_MultipleReadyFacilities_YieldsAfterEachProductionPoint()
        {
            Building order = CreateManufacturingScenario();
            int progressNotifications = 0;
            _tick.TickProgressed += () => progressNotifications++;
            IEnumerator tick = _tick.ProcessTickIncrementally();

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(1, order.ManufacturingProgress);
            Assert.AreEqual(1, progressNotifications);

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(2, order.ManufacturingProgress);
            Assert.AreEqual(2, progressNotifications);

            while (tick.MoveNext()) { }
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTickIncrementally_ManufacturingPresentationYield_BuffersResults()
        {
            CreateManufacturingScenario();
            int deliveredPoints = 0;
            using IDisposable observation =
                _session.Results.Observe<ManufacturingPointsCompletedResult>(results =>
                    deliveredPoints += results.Sum(result => result.Points)
                );
            IEnumerator tick = _tick.ProcessTickIncrementally();

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(0, deliveredPoints);
            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(0, deliveredPoints);

            while (tick.MoveNext()) { }
            Assert.AreEqual(2, deliveredPoints);
        }

        [Test]
        public void ProcessTickIncrementally_CompletedManufacturingYield_BuffersLifecycleResults()
        {
            CreateManufacturingScenario(1);
            int createdCount = 0;
            int deployedCount = 0;
            using IDisposable creationObservation =
                _session.Results.Observe<GameObjectCreatedResult>(results =>
                    createdCount += results.Count
                );
            using IDisposable deploymentObservation =
                _session.Results.Observe<GameObjectDeployedResult>(results =>
                    deployedCount += results.Count
                );
            IEnumerator tick = _tick.ProcessTickIncrementally();

            Assert.IsTrue(tick.MoveNext());
            Assert.AreEqual(0, createdCount);
            Assert.AreEqual(0, deployedCount);

            while (tick.MoveNext()) { }
            Assert.AreEqual(1, createdCount);
            Assert.AreEqual(1, deployedCount);
        }

        [Test]
        public void ProcessTickIncrementally_DisposedBeforeCompletion_ReleasesBusyGuard()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            (tick as IDisposable)?.Dispose();

            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_CompletionObserverThrows_RestoresIdleState()
        {
            InvalidOperationException expected = new("observer failure");
            _tick.TickCompleted += () => throw expected;

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_ResultDeliveryThrows_ReleasesBusyGuard()
        {
            InvalidOperationException expected = new("delivery failure");
            _processResults = (_, _) => throw expected;
            _tick = CreateTickProcessor();

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                _tick.ProcessTick
            );

            Assert.AreSame(expected, actual);
            Assert.IsFalse(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
        }

        [Test]
        public void ProcessTick_OpposingDiplomacyCompletesOnSameTick_PreservesFirstControlChange()
        {
            (Faction firstFaction, Faction secondFaction, Planet planet) =
                CreateOpposingDiplomacyScenario();
            Mission firstMission = CreateReadyDiplomacyMission(firstFaction, planet);
            Mission secondMission = CreateReadyDiplomacyMission(secondFaction, planet);
            List<MissionCompletedResult> completions = new();
            using IDisposable observation = _session.Results.Observe<MissionCompletedResult>(
                results => completions.AddRange(results)
            );

            _tick.ProcessTick();

            Assert.AreEqual(firstFaction.InstanceID, planet.GetOwnerInstanceID());
            MissionCompletedResult secondCompletion = completions.Single(result =>
                result.MissionInstanceID == secondMission.InstanceID
            );
            Assert.AreEqual(
                MissionCompletionReason.TargetChangedSides,
                secondCompletion.CompletionReason
            );
            Assert.IsNull(secondMission.GetParent());
            Assert.AreSame(planet, firstMission.GetParent());
        }

        [Test]
        public void Reset_SuspendedIterator_PreservesBusyGuardUntilDisposal()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "AI", DisplayName = "AI" });
            IEnumerator tick = _tick.ProcessTickIncrementally();
            Assert.IsTrue(tick.MoveNext());

            _tick.Reset();

            Assert.IsTrue(_tick.IsBusy);
            Assert.IsTrue(_tick.IsSettled);
            (tick as IDisposable)?.Dispose();
            Assert.IsFalse(_tick.IsBusy);
        }

        [Test]
        public void ReconcileLoadedState_NoEncounter_DoesNotAdvanceTick()
        {
            _tick.ReconcileLoadedState();

            Assert.AreEqual(0, _game.CurrentTick);
            Assert.IsTrue(_tick.IsSettled);
        }

        /// <summary>
        /// Connects tick scheduling to the current session's services.
        /// </summary>
        /// <returns>The tick processor under test.</returns>
        private GameTickProcessor CreateTickProcessor()
        {
            GameTickProcessor tick = new GameTickProcessor(_processResults, _ => { });
            tick.ConnectRuntime(_session);
            return tick;
        }

        /// <summary>
        /// Creates one manufacturing order served by two facilities that each complete a point.
        /// </summary>
        /// <param name="constructionCost">The production points required by the order.</param>
        /// <returns>The queued manufacturing order.</returns>
        private Building CreateManufacturingScenario(int constructionCost = 100)
        {
            const string factionId = "EMPIRE";
            Faction faction = new Faction
            {
                InstanceID = factionId,
                DisplayName = "Empire",
                RefinedMaterialStockpile = 1000,
            };
            _game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, sector);
            for (int index = 0; index < 2; index++)
            {
                _game.AttachNode(
                    new Building
                    {
                        InstanceID = $"SHIPYARD_{index}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.ConstructionFacility,
                        ProductionType = ManufacturingType.Building,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
            }

            Building order = new Building
            {
                InstanceID = "ORDER",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Mine,
                ConstructionCost = constructionCost,
            };
            Assert.IsTrue(
                _session.GetService<ManufacturingCommands>().Enqueue(planet, order, planet)
            );
            return order;
        }

        /// <summary>
        /// Creates two human factions and one evenly divided neutral diplomacy target.
        /// </summary>
        /// <returns>The opposing factions and their shared diplomacy target.</returns>
        private (
            Faction FirstFaction,
            Faction SecondFaction,
            Planet Planet
        ) CreateOpposingDiplomacyScenario()
        {
            Faction firstFaction = new Faction { InstanceID = "FIRST", DisplayName = "First" };
            Faction secondFaction = new Faction { InstanceID = "SECOND", DisplayName = "Second" };
            _game.GetFactions().Add(firstFaction);
            _game.GetFactions().Add(secondFaction);
            _game.SetFactionController(
                firstFaction.InstanceID,
                "FIRST_PLAYER",
                PlayerControllerType.Human
            );
            _game.SetFactionController(
                secondFaction.InstanceID,
                "SECOND_PLAYER",
                PlayerControllerType.Human
            );
            _game.Config.ProbabilityTables.Mission.Diplomacy = new Dictionary<int, int>
            {
                { -10000, 100 },
            };
            _game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int>
            {
                { -10000, 0 },
            };
            _game.Config.SupportShift.OwnershipTransferThreshold = 60;
            _game.Config.SupportShift.DiplomacyNeutralPlanetSupportBase = 10;
            _game.Config.SupportShift.DiplomacyNeutralPlanetSupportRange = 0;
            _game.Config.SupportShift.ControlChangeSupportShift = 0;

            PlanetSector sector = new PlanetSector { InstanceID = "DIPLOMACY_SECTOR" };
            _game.AttachNode(sector, _game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "DIPLOMACY_PLANET",
                DisplayName = "Diplomacy Planet",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { firstFaction.InstanceID, 50 },
                    { secondFaction.InstanceID, 50 },
                },
            };
            planet.AddVisitor(firstFaction.InstanceID);
            planet.AddVisitor(secondFaction.InstanceID);
            _game.AttachNode(planet, sector);
            return (firstFaction, secondFaction, planet);
        }

        /// <summary>
        /// Creates an attached diplomacy mission that will resolve on the next tick.
        /// </summary>
        /// <param name="faction">The faction conducting diplomacy.</param>
        /// <param name="planet">The neutral target planet.</param>
        /// <returns>The ready diplomacy mission.</returns>
        private Mission CreateReadyDiplomacyMission(Faction faction, Planet planet)
        {
            Officer officer = EntityFactory.CreateOfficer(
                $"{faction.InstanceID}_DIPLOMAT",
                faction.InstanceID
            );
            officer.SetBaseRating(SkillRating.Diplomacy, 100);
            Mission mission = MissionTestFactory.TryCreate(
                DiplomacyMission.MissionTypeID,
                _game,
                faction.InstanceID,
                planet,
                new List<IMissionParticipant> { officer },
                new List<IMissionParticipant>()
            );
            Assert.IsNotNull(mission);
            _game.AttachNode(mission, planet);
            _game.AttachNode(officer, mission);
            mission.Initiate(0);
            return mission;
        }
    }
}
