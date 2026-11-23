using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;
using Rebellion.Tactical;

namespace Rebellion.Tests.Gameplay
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
        public void ProcessTickIncrementally_NoProductionOrAI_YieldsWithoutReleasingMutationGuard()
        {
            int completions = 0;
            _tick.TickCompleted += () => completions++;
            IEnumerator tick = _tick.ProcessTickIncrementally();
            try
            {
                Assert.IsTrue(tick.MoveNext());
                do
                {
                    Assert.IsTrue(_tick.IsBusy);
                    Assert.IsFalse(_tick.CanAcceptPlayerActions);
                    Assert.IsFalse(_tick.IsSettled);
                    Assert.AreEqual(0, completions);
                } while (tick.MoveNext());

                Assert.AreEqual(1, completions);
                Assert.AreEqual(1, _game.CurrentTick);
                Assert.IsFalse(_tick.IsBusy);
                Assert.IsTrue(_tick.CanAcceptPlayerActions);
                Assert.IsTrue(_tick.IsSettled);
            }
            finally
            {
                (tick as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void ProcessTickIncrementally_MultipleReadyFacilities_YieldsAfterEachProductionPoint()
        {
            Building order = CreateManufacturingScenario();
            int progressNotifications = 0;
            _tick.TickProgressed += _ => progressNotifications++;
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
        public void ProcessTickIncrementally_ManufacturingProgress_IdentifiesProducingPlanet()
        {
            CreateManufacturingScenario();
            Planet observedProducer = null;
            _tick.TickProgressed += producer => observedProducer = producer;
            IEnumerator tick = _tick.ProcessTickIncrementally();
            try
            {
                Assert.IsTrue(tick.MoveNext());

                Assert.AreSame(_game.GetSceneNodeByInstanceID<Planet>("PLANET"), observedProducer);
            }
            finally
            {
                (tick as IDisposable)?.Dispose();
            }
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
        public void ProcessTick_OpposingDiplomacyCompletesOnSameTick_DoesNotReverseFirstControlChange()
        {
            (Faction firstFaction, Faction secondFaction, Planet planet) =
                CreateOpposingDiplomacyScenario();
            CreateReadyDiplomacyMission(firstFaction, planet);
            CreateReadyDiplomacyMission(secondFaction, planet);

            _tick.ProcessTick();

            Assert.AreEqual(firstFaction.InstanceID, planet.GetOwnerInstanceID());
        }

        /// <summary>
        /// Verifies a subsequent tick does not replay a command's immediately published start.
        /// </summary>
        [Test]
        public void ProcessTick_AlreadyPublishedMissionStart_DoesNotPublishItAgain()
        {
            (Faction faction, _, Planet planet) = CreateOpposingDiplomacyScenario();
            CreateReadyDiplomacyMission(faction, planet);
            Planet origin = new Planet
            {
                InstanceID = "PENDING_MISSION_ORIGIN",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                PositionX = 100,
            };
            _game.AttachNode(origin, planet.GetParent());
            Officer officer = EntityFactory.CreateOfficer("PENDING_DIPLOMAT", faction.InstanceID);
            officer.SetBaseRating(SkillRating.Diplomacy, 100);
            _game.AttachNode(officer, origin);
            MissionCommands commands = _session.GetService<MissionCommands>();
            int startCount = 0;
            using IDisposable observation = _session.Results.Observe<MissionStartedResult>(
                results => startCount += results.Count
            );
            Assert.IsTrue(
                commands.InitiateMission(
                    new MissionContext
                    {
                        Game = _game,
                        MissionTypeID = DiplomacyMission.MissionTypeID,
                        OwnerInstanceId = faction.InstanceID,
                        Location = planet,
                        MainParticipants = new List<IMissionParticipant> { officer },
                        DecoyParticipants = new List<IMissionParticipant>(),
                    }
                )
            );
            Assert.AreEqual(1, startCount);

            _tick.ProcessTick();

            Assert.AreEqual(1, startCount);
        }

        [Test]
        public void ProcessTick_FleetContinuesFromBlockadedWaypoint_DoesNotPublishEndedBlockadeStart()
        {
            (Planet blockadedPlanet, Planet nextPlanet, Fleet fleet, Building delivery) =
                CreateTransientBlockadeScenario();
            List<bool> observedStartStates = new();
            using IDisposable observation = _session.Results.Observe<BlockadeChangedResult>(
                results =>
                {
                    foreach (BlockadeChangedResult result in results)
                        if (result.Blockaded && ReferenceEquals(result.Planet, blockadedPlanet))
                            observedStartStates.Add(result.Planet.IsBlockaded());
                }
            );

            _tick.ProcessTick();

            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(delivery.InstanceID));
            Assert.AreSame(nextPlanet, fleet.GetParentOfType<Planet>());
            Assert.IsNotNull(fleet.Movement);
            CollectionAssert.DoesNotContain(
                observedStartStates,
                false,
                "A waypoint must not replay a blockade start after its fleet has continued."
            );
        }

        [Test]
        public void ProcessTick_TransientWaypointBlockade_DoesNotRedirectInboundRegiment()
        {
            (Planet blockadedPlanet, _, _, _) = CreateTransientBlockadeScenario();
            const string empireId = "TRANSIENT_EMPIRE";
            Planet sourcePlanet = new Planet
            {
                InstanceID = "TRANSIENT_REGIMENT_SOURCE",
                OwnerInstanceID = empireId,
                IsColonized = true,
                PositionX = 10,
                PositionY = 100,
            };
            _game.AttachNode(sourcePlanet, blockadedPlanet.GetParent());
            sourcePlanet.SetFullPopularSupport(empireId);
            Regiment regiment = EntityFactory.CreateRegiment(
                "TRANSIENT_INBOUND_REGIMENT",
                empireId
            );
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            _game.AttachNode(regiment, sourcePlanet);
            MovementCommands movement = _session.GetService<MovementCommands>();
            movement.RequestMove(regiment, blockadedPlanet);
            regiment.Movement = TestMovement.WithDuration(regiment.Movement, 10);

            _tick.ProcessTick();

            Assert.AreSame(regiment, _game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.AreSame(blockadedPlanet, regiment.GetParentOfType<Planet>());
            Assert.IsNotNull(regiment.Movement);
        }

        [Test]
        public void ProcessTick_FighterArrivesAtFriendlyShipyard_StartsFastIntervalOnArrivalTick()
        {
            const string factionId = "RECOVERY_FACTION";
            _game.Config.Recovery.FastReplacementIntervalTicks = 2;
            _game.Config.Recovery.NormalReplacementIntervalTicks = 30;
            AddHumanFaction(factionId);
            PlanetSector sector = new PlanetSector { InstanceID = "RECOVERY_SECTOR" };
            Planet origin = new Planet
            {
                InstanceID = "RECOVERY_ORIGIN",
                OwnerInstanceID = factionId,
                IsColonized = true,
                PositionX = 0,
            };
            Planet destination = new Planet
            {
                InstanceID = "RECOVERY_DESTINATION",
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 10,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            origin.SetFullPopularSupport(factionId);
            destination.SetFullPopularSupport(factionId);
            _game.AttachNode(
                new Building
                {
                    InstanceID = "RECOVERY_SHIPYARD",
                    OwnerInstanceID = factionId,
                    BuildingType = BuildingType.Shipyard,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                destination
            );
            Fleet fleet = AttachOperationalFleet(origin, factionId, "RECOVERY_FLEET");
            CapitalShip carrier = fleet.GetChildren<CapitalShip>().Single();
            carrier.StarfighterCapacity = 20;
            Starfighter squadron = new Starfighter
            {
                InstanceID = "RECOVERY_SQUADRON",
                OwnerInstanceID = factionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 8,
            };
            _game.AttachNode(squadron, carrier);
            _game.CurrentTick = 19;
            squadron.ScheduleReplacement(_game.CurrentTick, 30);
            MovementCommands movement = _session.GetService<MovementCommands>();
            Assert.IsTrue(movement.TryRequestMove(fleet, destination));
            fleet.Movement = TestMovement.WithDuration(fleet.Movement, 1);

            _tick.ProcessTick();

            Assert.AreEqual(20, _game.CurrentTick);
            Assert.AreEqual(2, squadron.ReplacementIntervalTicks);
            Assert.AreEqual(22, squadron.NextReplacementTick);
            Assert.AreEqual(8, squadron.CurrentSquadronSize);

            _tick.ProcessTick();
            _tick.ProcessTick();

            Assert.AreEqual(9, squadron.CurrentSquadronSize);
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

        [Test]
        public void ProcessTick_AutomaticEncounters_SettlesEachBeforeResolvingNext()
        {
            AssertAutomaticEncounterSettlement(reconcileLoadedState: false);
        }

        [Test]
        public void ReconcileLoadedState_AutomaticEncounters_SettlesEachBeforeResolvingNext()
        {
            AssertAutomaticEncounterSettlement(reconcileLoadedState: true);
        }

        [Test]
        public void ReconcileLoadedState_OrdinaryReactionQueuesManufacturing_SettlesBeforeCombatResult()
        {
            CreateAutomaticCombatScenario(1);
            Building existingOrder = CreateManufacturingScenario();
            ManufacturingCommands manufacturing = _session.GetService<ManufacturingCommands>();
            _session.Results.Publish(manufacturing.TakePendingResults());
            Planet producer = existingOrder.GetParentOfType<Planet>();
            Building order = new()
            {
                InstanceID = "AFTERMATH_ORDER",
                OwnerInstanceID = existingOrder.OwnerInstanceID,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            bool queued = false;
            bool created = false;
            bool combatObserved = false;
            using IDisposable ordinary = _session.Results.Subscribe<GameObjectDestroyedResult>(_ =>
            {
                if (!queued)
                {
                    queued = manufacturing.Enqueue(producer, order, producer);
                    Assert.IsTrue(queued);
                }
            });
            using IDisposable creation = _session.Results.Subscribe<GameObjectCreatedResult>(
                results =>
                    created |= results.Any(result => ReferenceEquals(result.GameObject, order))
            );
            using IDisposable observation =
                _session.Results.SubscribeAfterReactions<SpaceCombatResult>(_ =>
                {
                    combatObserved = true;
                    Assert.IsTrue(created);
                    Assert.IsFalse(manufacturing.HasPendingResults);
                });

            _tick.ReconcileLoadedState();

            Assert.IsTrue(combatObserved);
        }

        [Test]
        public void ReconcileLoadedState_CombatEventPlacesOfficer_SettlesCommandFactsBeforeAfterReactions()
        {
            CreateAutomaticCombatScenario(1);
            PlanetSector sector = _game.Galaxy.GetChildren<PlanetSector>().Single();
            Planet origin = new()
            {
                InstanceID = "COMMAND_ORIGIN",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            Planet destination = new()
            {
                InstanceID = "COMMAND_DESTINATION",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            Officer officer = EntityFactory.CreateOfficer("COMMANDING_OFFICER", "FNALL1");
            officer.CurrentRank = OfficerRank.Commander;
            _game.AttachNode(officer, origin);
            _session.GameEventCommands.InstallDefinitions(
                new[]
                {
                    new GameEvent
                    {
                        InstanceID = "PLACE_AFTER_COMBAT",
                        MaximumActivations = 1,
                        Triggers = new List<GameTrigger> { new SpaceCombatCompletedTrigger() },
                        Actions = new List<GameAction>
                        {
                            new PlaceUnitsAction
                            {
                                UnitInstanceID = officer.InstanceID,
                                DestinationInstanceID = destination.InstanceID,
                            },
                        },
                    },
                }
            );
            bool commandObserved = false;
            bool combatObserved = false;
            using IDisposable command = _session.Results.Subscribe<CommandKindChangedResult>(
                results => commandObserved |= results.Any(result => result.Officer == officer)
            );
            using IDisposable combat = _session.Results.SubscribeAfterReactions<SpaceCombatResult>(
                _ =>
                {
                    combatObserved = true;
                    Assert.IsTrue(commandObserved);
                    Assert.AreSame(destination, officer.GetParent());
                    Assert.AreEqual(OfficerRank.None, officer.CurrentRank);
                    Assert.IsFalse(_session.GetService<MovementCommands>().HasPendingResults);
                }
            );

            _tick.ReconcileLoadedState();

            Assert.IsTrue(combatObserved);
        }

        [Test]
        public void ReconcileLoadedState_CombatReactionMovesUnrelatedFleet_DefersItsBlockade()
        {
            CreateAutomaticCombatScenario(1);
            PlanetSector sector = _game.Galaxy.GetChildren<PlanetSector>().Single();
            Planet origin = new()
            {
                InstanceID = "UNRELATED_ORIGIN",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            Planet destination = new()
            {
                InstanceID = "UNRELATED_DESTINATION",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                PositionX = 10,
            };
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            Fleet fleet = AttachOperationalFleet(origin, "FNEMP1", "UNRELATED_FLEET");
            Assert.IsTrue(origin.IsBlockaded());
            MovementCommands movement = _session.GetService<MovementCommands>();
            List<BlockadeChangedResult> blockades = new();
            bool departureObserved = false;
            bool combatObserved = false;
            using IDisposable reaction = _session.Results.Subscribe<SpaceCombatResult>(_ =>
                movement.RequestMove(fleet, destination)
            );
            using IDisposable departure = _session.Results.Subscribe<GameObjectEnrouteActiveResult>(
                _ => departureObserved = true
            );
            using IDisposable blockade = _session.Results.Subscribe<BlockadeChangedResult>(
                results => blockades.AddRange(results.Where(result => result.Planet == origin))
            );
            using IDisposable combat = _session.Results.SubscribeAfterReactions<SpaceCombatResult>(
                _ =>
                {
                    combatObserved = true;
                    Assert.IsTrue(departureObserved);
                    Assert.IsFalse(movement.HasPendingResults);
                    Assert.IsEmpty(blockades);
                }
            );

            _tick.ReconcileLoadedState();

            Assert.IsTrue(combatObserved);
            Assert.AreEqual(1, blockades.Count);
            Assert.IsFalse(blockades[0].Blockaded);
        }

        [Test]
        public void ReconcileLoadedState_CombatBlockadeReactionQueuesMovement_SettlesBeforeAfterReactions()
        {
            CreateTacticalCombatScenario("FNEMP1", 1);
            _game.SetFactionController("FNEMP1", "PLAYER", PlayerControllerType.AI);
            PlanetSector sector = _game.Galaxy.GetChildren<PlanetSector>().Single();
            Planet origin = new()
            {
                InstanceID = "FOLLOW_UP_ORIGIN",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                PositionX = 10,
            };
            Planet destination = new()
            {
                InstanceID = "FOLLOW_UP_DESTINATION",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                PositionX = 20,
            };
            _game.AttachNode(origin, sector);
            _game.AttachNode(destination, sector);
            Officer officer = EntityFactory.CreateOfficer("FOLLOW_UP_OFFICER", "FNEMP1");
            officer.CurrentRank = OfficerRank.Commander;
            _game.AttachNode(officer, origin);
            Fleet winner = _game.GetSceneNodeByInstanceID<Fleet>("TACTICAL_0_FNEMP1");
            ConfigureDecisiveAutomaticFire(winner.GetChildren<CapitalShip>().Single());
            MovementCommands movement = _session.GetService<MovementCommands>();
            bool blockadeEnded = false;
            bool commandObserved = false;
            bool combatObserved = false;
            using IDisposable reaction = _session.Results.Subscribe<SpaceCombatResult>(_ =>
                movement.RequestMove(winner, origin)
            );
            using IDisposable blockade = _session.Results.Subscribe<BlockadeChangedResult>(
                results =>
                {
                    if (
                        results.Any(result =>
                            !result.Blockaded && result.Planet.InstanceID == "TACTICAL_PLANET_0"
                        )
                    )
                    {
                        blockadeEnded = true;
                        movement.RequestMove(officer, destination);
                    }
                }
            );
            using IDisposable command = _session.Results.Subscribe<CommandKindChangedResult>(
                results => commandObserved |= results.Any(result => result.Officer == officer)
            );
            using IDisposable combat = _session.Results.SubscribeAfterReactions<SpaceCombatResult>(
                _ =>
                {
                    combatObserved = true;
                    Assert.IsTrue(blockadeEnded);
                    Assert.IsTrue(commandObserved);
                    Assert.IsFalse(movement.HasPendingResults);
                }
            );

            _tick.ReconcileLoadedState();

            Assert.IsTrue(combatObserved);
        }

        [Test]
        public void ResolveCombatRetreat_WithdrawalFacts_SettleBeforeCombatResult()
        {
            CreateRetreatCombatScenario();
            MovementCommands movement = _session.GetService<MovementCommands>();
            bool departureObserved = false;
            bool combatObserved = false;
            using IDisposable departure = _session.Results.Subscribe<GameObjectEnrouteActiveResult>(
                _ => departureObserved = true
            );
            using IDisposable observation =
                _session.Results.SubscribeAfterReactions<SpaceCombatResult>(_ =>
                {
                    combatObserved = true;
                    Assert.IsTrue(departureObserved);
                    Assert.IsFalse(movement.HasPendingResults);
                });

            SpaceCombatResult result = _tick.ResolveCombatRetreat("FNALL1");

            Assert.IsNotNull(result);
            Assert.IsTrue(combatObserved);
        }

        [Test]
        public void ResolveCombatRetreat_FinalBlockade_SettlesBeforeCombatResult()
        {
            CreateRetreatCombatScenario();
            List<BlockadeChangedResult> blockades = new();
            bool combatObserved = false;
            using IDisposable blockade = _session.Results.Subscribe<BlockadeChangedResult>(
                results => blockades.AddRange(results)
            );
            using IDisposable observation =
                _session.Results.SubscribeAfterReactions<SpaceCombatResult>(results =>
                {
                    combatObserved = true;
                    Assert.AreEqual(1, blockades.Count);
                    Assert.AreSame(results.Single().Planet, blockades[0].Planet);
                    Assert.IsTrue(blockades[0].Blockaded);
                });

            SpaceCombatResult result = _tick.ResolveCombatRetreat("FNALL1");

            Assert.IsNotNull(result);
            Assert.IsTrue(combatObserved);
            Assert.AreEqual(1, blockades.Count);
        }

        [Test]
        public void CompleteTacticalCombat_AutomaticEncounters_SettleEachBeforeResolvingNext()
        {
            SpaceCombatCommands commands = CreateTacticalCombatScenario("FNALL1", 3);
            _tick.ReconcileLoadedState();
            TacticalCombatSession battle = commands.BeginTacticalCombat();
            CompleteTacticalEncounter(battle);
            _game.SetFactionController("FNALL1", "PLAYER", PlayerControllerType.AI);
            int observed = 0;
            HashSet<Planet> completed = new();
            using IDisposable observation = _session.Results.Observe<SpaceCombatResult>(results =>
            {
                foreach (SpaceCombatResult result in results)
                {
                    completed.Add(result.Planet);
                    if (++observed == 2)
                    {
                        Planet next = _game
                            .GetSceneNodesByType<Planet>()
                            .Single(planet => !completed.Contains(planet));
                        Assert.AreEqual(2, next.GetChildren<Fleet>().Count);
                        Assert.IsTrue(next.GetChildren<Fleet>().All(fleet => !fleet.IsInCombat));
                    }
                }
            });

            _tick.CompleteTacticalCombat(battle);

            Assert.AreEqual(3, observed);
            Assert.IsTrue(_tick.IsSettled);
        }

        [TestCase("FNEMP1", 1)]
        [TestCase("FNEMP1", 2)]
        [TestCase("FNALL1", 1)]
        [TestCase("FNALL1", 2)]
        public void CompleteTacticalCombat_ManualEncounters_ResumesOnlyAfterFinalDecision(
            string playerFactionId,
            int encounterCount
        )
        {
            SpaceCombatCommands commands = CreateTacticalCombatScenario(
                playerFactionId,
                encounterCount
            );
            int decisionsRequired = 0;
            int completedTicks = 0;
            int resumptions = 0;
            _tick.CombatDecisionRequired += () => decisionsRequired++;
            _tick.TickCompleted += () => completedTicks++;
            _tick.CombatResumed += () => resumptions++;

            _tick.ReconcileLoadedState();

            for (int encounter = 0; encounter < encounterCount; encounter++)
            {
                Assert.AreEqual(encounter + 1, decisionsRequired);
                Assert.IsFalse(_tick.IsSettled);
                Assert.IsTrue(commands.CanTakeCommand);
                TacticalCombatSession battle = commands.BeginTacticalCombat();

                _tick.ProcessTick();

                Assert.AreEqual(40, _game.CurrentTick);
                Assert.AreEqual(0, completedTicks);
                Assert.AreEqual(0, resumptions);
                CompleteTacticalEncounter(battle);

                SpaceCombatResult result = _tick.CompleteTacticalCombat(battle);

                Assert.AreEqual(battle.PlayerSide, result.Winner);
                Assert.AreEqual(40, _game.CurrentTick);
                if (encounter + 1 < encounterCount)
                {
                    Assert.IsTrue(commands.HasPendingDecision);
                    Assert.IsFalse(_tick.IsSettled);
                    Assert.AreEqual(0, completedTicks);
                    Assert.AreEqual(0, resumptions);
                }
            }

            Assert.IsFalse(commands.HasPendingDecision);
            Assert.IsTrue(_tick.IsSettled);
            Assert.AreEqual(1, completedTicks);
            Assert.AreEqual(1, resumptions);
        }

        [TestCase("FNEMP1")]
        [TestCase("FNALL1")]
        [Category("RequiresMedia")]
        public void CompleteTacticalCombat_AuthoredCarrierBattle_CommitsOneResultAndResumesOneTick(
            string playerFactionId
        )
        {
            UnitFactory units = new(
                TestContent.Data.Buildings,
                TestContent.Data.CapitalShips,
                TestContent.Data.Starfighters,
                TestContent.Data.Regiments,
                TestContent.Data.SpecialForces,
                TestContent.Data.TacticalCombat
            );
            SpaceCombatCommands commands = CreateTacticalCombatScenario(
                playerFactionId,
                1,
                fleet => AttachAuthoredCarrierFleet(fleet, units)
            );
            int completedTicks = 0;
            int resumptions = 0;
            int combatResults = 0;
            _tick.TickCompleted += () => completedTicks++;
            _tick.CombatResumed += () => resumptions++;
            using IDisposable observation = _session.Results.Observe<SpaceCombatResult>(results =>
                combatResults += results.Count
            );
            _tick.ReconcileLoadedState();
            TacticalCombatSession battle = commands.BeginTacticalCombat();
            CapitalShip[] ships = battle
                .Units.Select(unit => _game.NodesByInstanceID[unit.InstanceID])
                .OfType<CapitalShip>()
                .ToArray();
            Starfighter[] fighters = battle
                .Units.Select(unit => _game.NodesByInstanceID[unit.InstanceID])
                .OfType<Starfighter>()
                .ToArray();
            Dictionary<ISceneNode, ISceneNode> parents = battle.Units.ToDictionary(
                unit => _game.NodesByInstanceID[unit.InstanceID],
                unit => _game.NodesByInstanceID[unit.InstanceID].GetParent()
            );
            Dictionary<TacticalCombatUnit, int> shots = battle.Units.ToDictionary(
                unit => unit,
                _ => 0
            );
            battle.ShotFired += shot => shots[shot.Source]++;
            Assert.AreEqual(2, ships.Length);
            Assert.AreEqual(4, fighters.Length);
            Assert.AreEqual(
                2,
                battle.Units.Count(unit => unit.Status == TacticalCombatUnitStatus.Docked)
            );

            for (int frame = 0; frame < 120000 && !battle.IsComplete; frame++)
            {
                if (frame % 20 == 0)
                    TargetActiveOpponents(battle);
                battle.Advance(50);
            }

            Assert.IsTrue(battle.IsComplete, DescribeCarrierBattle(battle, shots));
            foreach (CombatSide side in new[] { CombatSide.Attacker, CombatSide.Defender })
            {
                Assert.Greater(
                    shots
                        .Where(entry => entry.Key.Side == side && !entry.Key.IsFighter)
                        .Sum(entry => entry.Value),
                    0,
                    $"No capital-ship discharge for {side}."
                );
                Assert.Greater(
                    shots
                        .Where(entry => entry.Key.Side == side && entry.Key.IsFighter)
                        .Sum(entry => entry.Value),
                    0,
                    $"No fighter discharge for {side}."
                );
            }
            foreach (CapitalShip ship in ships)
            {
                Assert.AreEqual(ship.MaxHullStrength, ship.CurrentHullStrength);
                Assert.AreEqual(0, ship.SystemDamage.Total);
                Assert.AreSame(parents[ship], ship.GetParent());
                Assert.AreSame(ship, _game.GetSceneNodeByInstanceID<CapitalShip>(ship.InstanceID));
            }
            foreach (Starfighter fighter in fighters)
            {
                Assert.AreEqual(fighter.MaxSquadronSize, fighter.CurrentSquadronSize);
                Assert.AreSame(parents[fighter], fighter.GetParent());
                Assert.AreSame(
                    fighter,
                    _game.GetSceneNodeByInstanceID<Starfighter>(fighter.InstanceID)
                );
            }
            _tick.ProcessTick();
            Assert.AreEqual(40, _game.CurrentTick);
            Assert.IsTrue(commands.HasPendingDecision);
            Assert.IsFalse(_tick.IsSettled);
            Assert.AreEqual(0, completedTicks);
            Assert.AreEqual(0, resumptions);
            Assert.AreEqual(0, combatResults);

            SpaceCombatResult result = _tick.CompleteTacticalCombat(battle);

            Assert.AreEqual(battle.Winner, result.Winner);
            Assert.AreNotEqual(CombatSide.Draw, result.Winner);
            Assert.AreEqual(40, result.Tick);
            Assert.AreEqual(40, _game.CurrentTick);
            Assert.IsFalse(commands.HasPendingDecision);
            Assert.IsTrue(_tick.IsSettled);
            Assert.AreEqual(1, completedTicks);
            Assert.AreEqual(1, resumptions);
            Assert.AreEqual(1, combatResults);
            Assert.Throws<InvalidOperationException>(() => _tick.CompleteTacticalCombat(battle));
            Assert.AreEqual(1, completedTicks);
            Assert.AreEqual(1, resumptions);
            Assert.AreEqual(1, combatResults);
        }

        [TestCase("FNEMP1")]
        [TestCase("FNALL1")]
        public void CompleteTacticalCombat_UnfinishedManualEncounter_RetainsSuspendedTick(
            string playerFactionId
        )
        {
            SpaceCombatCommands commands = CreateTacticalCombatScenario(playerFactionId, 1);
            _tick.ReconcileLoadedState();
            TacticalCombatSession battle = commands.BeginTacticalCombat();
            int completedTicks = 0;
            _tick.TickCompleted += () => completedTicks++;

            Assert.Throws<InvalidOperationException>(() => _tick.CompleteTacticalCombat(battle));
            _tick.ProcessTick();

            Assert.AreEqual(40, _game.CurrentTick);
            Assert.IsTrue(commands.HasPendingDecision);
            Assert.IsFalse(_tick.IsSettled);
            Assert.AreEqual(0, completedTicks);
        }

        /// <summary>
        /// Verifies each automatic encounter is delivered while the next remains untouched.
        /// </summary>
        /// <param name="reconcileLoadedState">Whether to scan loaded positions without advancing the tick.</param>
        private void AssertAutomaticEncounterSettlement(bool reconcileLoadedState)
        {
            CreateAutomaticCombatScenario(2);
            int observed = 0;
            using IDisposable observation = _session.Results.Observe<SpaceCombatResult>(results =>
            {
                foreach (SpaceCombatResult result in results)
                {
                    if (observed++ == 0)
                    {
                        Planet next = _game
                            .GetSceneNodesByType<Planet>()
                            .Single(planet => planet != result.Planet);
                        Assert.AreEqual(2, next.GetChildren<Fleet>().Count);
                        Assert.IsTrue(next.GetChildren<Fleet>().All(fleet => !fleet.IsInCombat));
                    }
                }
            });

            if (reconcileLoadedState)
                _tick.ReconcileLoadedState();
            else
                _tick.ProcessTick();

            Assert.AreEqual(2, observed);
            Assert.AreEqual(reconcileLoadedState ? 40 : 41, _game.CurrentTick);
        }

        /// <summary>
        /// Creates pending manual encounters for one human faction without advancing campaign time.
        /// </summary>
        /// <param name="playerFactionId">The faction taking manual command.</param>
        /// <param name="encounterCount">The number of contested planets.</param>
        /// <param name="populateFleet">Optional roster creation for each empty participating fleet.</param>
        /// <returns>The pending encounter commands shared by the tick processor.</returns>
        private SpaceCombatCommands CreateTacticalCombatScenario(
            string playerFactionId,
            int encounterCount,
            Action<Fleet> populateFleet = null
        )
        {
            _session.Dispose();
            _game.SetConfig(
                populateFleet == null
                    ? TacticalTestData.ConfigureCampaignRules(_game.Config)
                    : TestContent.CreateGameConfig()
            );
            _game.CurrentTick = 40;
            _game.Summary.PlayerFactionID = playerFactionId;
            foreach (string factionId in new[] { "FNEMP1", "FNALL1" })
            {
                Faction faction = new()
                {
                    InstanceID = factionId,
                    DisplayName = factionId == "FNEMP1" ? "Empire" : "Alliance",
                };
                if (populateFleet != null)
                    TestContent
                        .Data.Factions.Single(definition => definition.InstanceID == factionId)
                        .CopyDefinitionTo(faction);
                else
                    faction.Settings.BattleLossLoyaltyDivisor =
                        TacticalTestData.BattleLossLoyaltyDivisor;
                _game.GetFactions().Add(faction);
            }
            _game.SetFactionController(playerFactionId, "PLAYER", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "TACTICAL_SECTOR" };
            _game.AttachNode(sector, _game.Galaxy);
            for (int encounter = 0; encounter < encounterCount; encounter++)
            {
                Planet planet = new Planet
                {
                    InstanceID = $"TACTICAL_PLANET_{encounter}",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                };
                planet.InitializeSectorSupportEligibility(true);
                _game.AttachNode(planet, sector);
                foreach (string factionId in new[] { "FNEMP1", "FNALL1" })
                {
                    if (populateFleet != null)
                    {
                        Fleet authoredFleet = EntityFactory.CreateFleet(
                            $"TACTICAL_{encounter}_{factionId}",
                            factionId
                        );
                        _game.AttachNode(authoredFleet, planet);
                        populateFleet(authoredFleet);
                        continue;
                    }
                    Fleet fleet = AttachOperationalFleet(
                        planet,
                        factionId,
                        $"TACTICAL_{encounter}_{factionId}"
                    );
                    CapitalShip ship = fleet.GetChildren<CapitalShip>().Single();
                    ship.TypeID = factionId == "FNEMP1" ? "CSEM006" : "ALCS008";
                    ship.MaxHullStrength = 1000;
                    ship.CurrentHullStrength = 1000;
                    ship.SublightSpeed = 100;
                    ship.WeaponRecharge = 1;
                    if (factionId == playerFactionId)
                        ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
                        {
                            100000,
                            100000,
                            100000,
                            100000,
                            100,
                        };
                }
            }

            _session =
                populateFleet == null
                    ? new GameSession(_game, TacticalTestData.CreateCatalog(_game.Config))
                    : TestContent.CreateGameSession(_game);
            _tick = CreateTickProcessor();
            return _session.GetService<SpaceCombatCommands>();
        }

        /// <summary>
        /// Creates contested planets whose encounters do not require a player decision.
        /// </summary>
        /// <param name="encounterCount">The number of independent encounters.</param>
        private void CreateAutomaticCombatScenario(int encounterCount)
        {
            CreateTacticalCombatScenario("FNALL1", encounterCount);
            _game.SetFactionController("FNALL1", "PLAYER", PlayerControllerType.AI);
            foreach (CapitalShip ship in _game.GetSceneNodesByType<CapitalShip>())
                if (ship.OwnerInstanceID == "FNALL1")
                    ConfigureDecisiveAutomaticFire(ship);
        }

        /// <summary>
        /// Gives the intended winner enough sustained fire to settle the scheduling fixture in one phase.
        /// </summary>
        /// <param name="ship">The ship whose victory drives the ordering assertion.</param>
        private static void ConfigureDecisiveAutomaticFire(CapitalShip ship)
        {
            ship.WeaponRecharge = 1000;
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[]
            {
                1000,
                1000,
                1000,
                1000,
                100,
            };
        }

        /// <summary>
        /// Suspends a defended planet's encounter with a friendly withdrawal destination available.
        /// </summary>
        private void CreateRetreatCombatScenario()
        {
            CreateTacticalCombatScenario("FNALL1", 1);
            PlanetSector sector = _game.Galaxy.GetChildren<PlanetSector>().Single();
            _game.AttachNode(
                new Planet
                {
                    InstanceID = "RETREAT_DESTINATION",
                    OwnerInstanceID = "FNALL1",
                    IsColonized = true,
                    PositionX = 10,
                },
                sector
            );
            _game
                .GetSceneNodeByInstanceID<Planet>("RETREAT_DESTINATION")
                .InitializeSectorSupportEligibility(true);
            _tick.ReconcileLoadedState();
        }

        /// <summary>
        /// Attaches one authored carrier and two complete authored fighter squadrons to its fleet.
        /// </summary>
        /// <param name="fleet">The already attached participating fleet.</param>
        /// <param name="units">The factory cloning the selected content templates.</param>
        private void AttachAuthoredCarrierFleet(Fleet fleet, UnitFactory units)
        {
            bool empire = fleet.OwnerInstanceID == "FNEMP1";
            CapitalShip carrier = units.Create<CapitalShip>(
                empire ? "CSEM006" : "ALCS008",
                fleet.OwnerInstanceID
            );
            carrier.InstanceID = $"{fleet.InstanceID}_SHIP";
            _game.AttachNode(carrier, fleet);
            for (int squadron = 0; squadron < 2; squadron++)
            {
                Starfighter fighter = units.Create<Starfighter>(
                    empire ? "SFEM01" : "SFAL02",
                    fleet.OwnerInstanceID
                );
                fighter.InstanceID = $"{fleet.InstanceID}_FIGHTER_{squadron}";
                _game.AttachNode(fighter, carrier);
            }
        }

        /// <summary>
        /// Assigns an available opposing target when a player's group has no retained live target.
        /// </summary>
        /// <param name="battle">The manually commanded encounter.</param>
        private static void TargetActiveOpponents(TacticalCombatSession battle)
        {
            foreach (TacticalCombatGroup group in battle.Groups.ToArray())
            {
                if (
                    group.Side != battle.PlayerSide
                    || group.PrimaryTarget?.IsTargetable == true
                    || group.RequestedTargets.Any(target => target.IsTargetable)
                )
                    continue;
                TacticalCombatUnit target =
                    battle.Units.FirstOrDefault(unit =>
                        unit.Side != group.Side
                        && unit.IsTargetable
                        && unit.IsFighter == group.IsFighterGroup
                    )
                    ?? battle.Units.FirstOrDefault(unit =>
                        unit.Side != group.Side && unit.IsTargetable
                    );
                if (target != null)
                    battle.SetTarget(group, target);
            }
        }

        /// <summary>
        /// Describes live combat pools, movement, orders, charge, and actual firing when a battle fails to finish.
        /// </summary>
        /// <param name="battle">The bounded encounter whose terminal state is being checked.</param>
        /// <param name="shots">The observed discharge counts for each participant.</param>
        /// <returns>The state needed to investigate a stalled encounter.</returns>
        private static string DescribeCarrierBattle(
            TacticalCombatSession battle,
            IReadOnlyDictionary<TacticalCombatUnit, int> shots
        ) =>
            $"System time: {battle.SystemTime}; "
            + string.Join(
                "; ",
                battle.Units.Select(unit =>
                    $"{unit.InstanceID} ({unit.TypeID}): state={unit.Status}, hull={unit.Systems.Hull}, "
                    + $"shields={unit.Systems.Shields}, position={unit.Position}, heading={unit.Heading}, "
                    + $"order={battle.GetGroup(unit)?.ActiveOrder}, target={battle.GetGroup(unit)?.PrimaryTarget?.InstanceID}, "
                    + $"shots={shots[unit]}, forwardCharge={unit.Weapons.GetCharge(TacticalWeaponArc.Forward)}/"
                    + $"{unit.Weapons.GetMaximumCharge(TacticalWeaponArc.Forward)}, "
                    + $"weaponsDamage={unit.Systems.GetDamage(TacticalShipSystem.Weapons)}"
                )
            );

        /// <summary>
        /// Advances a manually targeted battle through its terminal lifecycle.
        /// </summary>
        /// <param name="battle">The manually commanded encounter.</param>
        private static void CompleteTacticalEncounter(TacticalCombatSession battle)
        {
            TacticalCombatUnit player = battle.Units.Single(unit => unit.Side == battle.PlayerSide);
            TacticalCombatUnit opponent = battle.Units.Single(unit =>
                unit.Side != battle.PlayerSide
            );
            int playerShots = 0;
            battle.ShotFired += shot =>
            {
                if (shot.Source == player)
                    playerShots++;
            };
            Assert.IsTrue(player.Weapons.IsReady(TacticalWeaponArc.Forward));
            Assert.Greater(player.Weapons.GetMaximumCharge(TacticalWeaponArc.Forward), 0);
            battle.SetTarget(
                battle.Groups.Single(group => group.Side == battle.PlayerSide),
                opponent
            );
            for (int frame = 0; frame < 12000 && !battle.IsComplete; frame++)
                battle.Advance(50);
            Assert.IsTrue(
                battle.IsComplete,
                $"Player: {player.Status}, hull={player.Systems.Hull}, position={player.Position}; "
                    + $"opponent: {opponent.Status}, hull={opponent.Systems.Hull}, position={opponent.Position}"
            );
            Assert.Greater(playerShots, 0);
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
        /// Creates a manufactured delivery and hostile fleet that finish the same waypoint leg.
        /// </summary>
        /// <returns>The transient blockade scenario.</returns>
        private (
            Planet BlockadedPlanet,
            Planet NextPlanet,
            Fleet Fleet,
            Building Delivery
        ) CreateTransientBlockadeScenario()
        {
            const string empireId = "TRANSIENT_EMPIRE";
            const string allianceId = "TRANSIENT_ALLIANCE";
            AddHumanFaction(empireId);
            AddHumanFaction(allianceId);
            PlanetSector sector = new PlanetSector { InstanceID = "TRANSIENT_SECTOR" };
            Planet origin = new Planet
            {
                InstanceID = "TRANSIENT_ORIGIN",
                OwnerInstanceID = allianceId,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            Planet blockadedPlanet = new Planet
            {
                InstanceID = "TRANSIENT_BLOCKADED",
                OwnerInstanceID = empireId,
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 10,
                PositionY = 0,
            };
            Planet nextPlanet = new Planet
            {
                InstanceID = "TRANSIENT_NEXT",
                OwnerInstanceID = allianceId,
                IsColonized = true,
                PositionX = 20,
                PositionY = 0,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(origin, sector);
            _game.AttachNode(blockadedPlanet, sector);
            _game.AttachNode(nextPlanet, sector);

            origin.SetFullPopularSupport(allianceId);
            blockadedPlanet.SetFullPopularSupport(empireId);
            nextPlanet.SetFullPopularSupport(allianceId);

            Fleet fleet = AttachOperationalFleet(origin, allianceId, "TRANSIENT_FLEET");
            MovementCommands movement = _session.GetService<MovementCommands>();
            Assert.IsTrue(
                movement.TrySetFleetWaypointRoute(
                    new ISceneNode[] { fleet },
                    new[] { blockadedPlanet.InstanceID, nextPlanet.InstanceID },
                    allianceId
                )
            );
            TestMovement.AdvanceTo(fleet.Movement, fleet.Movement.TransitTicks - 1);

            Building delivery = new Building
            {
                InstanceID = "TRANSIENT_DELIVERY",
                DisplayName = "Advanced training facility",
                OwnerInstanceID = empireId,
                BuildingType = BuildingType.TrainingFacility,
                ManufacturingStatus = ManufacturingStatus.Delivering,
                Movement = TestMovement.Create(
                    transitTicks: 1,
                    ticksElapsed: 0,
                    originPosition: origin.GetPosition(),
                    currentPosition: origin.GetPosition()
                ),
            };
            _game.AttachNode(delivery, blockadedPlanet);

            return (blockadedPlanet, nextPlanet, fleet, delivery);
        }

        /// <summary>
        /// Adds a human-controlled faction to the active test game.
        /// </summary>
        /// <param name="instanceId">The faction identifier.</param>
        /// <returns>The added faction.</returns>
        private Faction AddHumanFaction(string instanceId)
        {
            Faction faction = new Faction { InstanceID = instanceId, DisplayName = instanceId };
            _game.GetFactions().Add(faction);
            _game.SetFactionController(
                instanceId,
                $"{instanceId}_PLAYER",
                PlayerControllerType.Human
            );
            return faction;
        }

        /// <summary>
        /// Attaches a fleet with one completed capital ship to a planet.
        /// </summary>
        /// <param name="planet">The fleet's planet.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <param name="fleetInstanceId">The fleet identifier.</param>
        /// <returns>The attached operational fleet.</returns>
        private Fleet AttachOperationalFleet(
            Planet planet,
            string ownerInstanceId,
            string fleetInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetInstanceId, ownerInstanceId);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{fleetInstanceId}_SHIP",
                OwnerInstanceID = ownerInstanceId,
                Hyperdrive = 100,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, planet);
            _game.AttachNode(ship, fleet);
            return fleet;
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
