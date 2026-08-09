using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameActionsTests
    {
        private GameRoot BuildGame(out Planet empPlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            empPlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empPlanet, system);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, system);
            return game;
        }

        private static InformantIntelligenceAction CreateInformantAction()
        {
            return new InformantIntelligenceAction
            {
                FactionRoutes = new List<InformantFactionRoute>
                {
                    new InformantFactionRoute
                    {
                        ControllerFactionInstanceID = "empire",
                        RecipientFactionInstanceID = "rebels",
                    },
                },
                IntelligenceChoices = new List<PlanetIntelligenceCategory>
                {
                    PlanetIntelligenceCategory.System,
                    PlanetIntelligenceCategory.CapitalShips,
                    PlanetIntelligenceCategory.Starfighters,
                    PlanetIntelligenceCategory.GroundForces,
                    PlanetIntelligenceCategory.Buildings,
                    PlanetIntelligenceCategory.Officers,
                    PlanetIntelligenceCategory.All,
                },
            };
        }

        [Test]
        public void ResolveOfficerEncounter_ValidIDs_EmitsRequest()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            ResolveOfficerEncounterAction action = new ResolveOfficerEncounterAction
            {
                EncounteredOfficerInstanceID = "a1",
                OpposingOfficerInstanceID = "d1",
            };

            List<GameResult> results = action.Execute(game);

            OfficerEncounterRequestedResult request = results
                .OfType<OfficerEncounterRequestedResult>()
                .Single();
            Assert.AreSame(attacker, request.EncounteredOfficer);
            Assert.AreSame(defender, request.OpposingOfficer);
        }

        [Test]
        public void ResolveOfficerEncounter_ArrivingSecondOfficer_ReversesAuthoredOrder()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            game.AttachNode(luke, rebelPlanet);
            vader.IsCaptured = true;
            game.AttachNode(vader, rebelPlanet);
            vader.IsCaptured = false;
            UnitArrivedResult arrival = new UnitArrivedResult
            {
                Unit = vader,
                Destination = rebelPlanet,
            };
            ResolveOfficerEncounterAction action = new ResolveOfficerEncounterAction
            {
                EncounteredOfficerInstanceID = "luke",
                OpposingOfficerInstanceID = "vader",
                EncounteredOfficerIsArrivingParticipant = true,
                VoicePath = "encounter-voice",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                arrival
            );

            OfficerEncounterRequestedResult request = action
                .Execute(game, game.Random, context)
                .OfType<OfficerEncounterRequestedResult>()
                .Single();

            Assert.AreSame(vader, request.EncounteredOfficer);
            Assert.AreSame(luke, request.OpposingOfficer);
            Assert.AreEqual("encounter-voice", request.VoicePath);
        }

        [Test]
        public void ResolveOfficerEncounter_ForceRankChance_UsesSummedRankThreshold()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 60;
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            vader.ForceValue = 60;
            luke.IsCaptured = true;
            game.AttachNode(luke, empirePlanet);
            luke.IsCaptured = false;
            game.AttachNode(vader, empirePlanet);
            ResolveOfficerEncounterAction action = new ResolveOfficerEncounterAction
            {
                EncounteredOfficerInstanceID = luke.InstanceID,
                OpposingOfficerInstanceID = vader.InstanceID,
                UseForceRankDetectionChance = true,
                ForceRankDetectionChanceModifier = -100,
            };

            Assert.IsEmpty(action.Execute(game, new SequenceRNG(new[] { 20 }), null));
            Assert.AreEqual(
                1,
                action
                    .Execute(game, new SequenceRNG(new[] { 19 }), null)
                    .OfType<OfficerEncounterRequestedResult>()
                    .Count()
            );

            luke.ForceValue = 0;
            Assert.IsEmpty(action.Execute(game, new SequenceRNG(new[] { 0 }), null));
        }

        [Test]
        public void OfficerPairArrival_OfficerInsideArrivingFleet_MatchesPair()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(vader, ship);
            OfficerPairArrivalConditional conditional = new OfficerPairArrivalConditional
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = fleet, Destination = empirePlanet }
            );

            Assert.IsTrue(matches);
        }

        [Test]
        public void UnitArrival_OfficerInsideArrivingFleetAtAuthoredDestination_Matches()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(emperor, ship);
            UnitArrivalConditional conditional = new UnitArrivalConditional
            {
                UnitInstanceID = emperor.InstanceID,
                DestinationInstanceID = empirePlanet.InstanceID,
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = fleet, Destination = empirePlanet }
            );

            Assert.IsTrue(matches);
        }

        [Test]
        public void UnitArrival_WrongDestination_DoesNotMatch()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer emperor = EntityFactory.CreateOfficer("emperor", "empire");
            game.AttachNode(emperor, empirePlanet);
            UnitArrivalConditional conditional = new UnitArrivalConditional
            {
                UnitInstanceID = emperor.InstanceID,
                DestinationInstanceID = empirePlanet.InstanceID,
            };

            bool matches = conditional.IsMet(
                game,
                new UnitArrivedResult { Unit = emperor, Destination = rebelPlanet }
            );

            Assert.IsFalse(matches);
        }

        [Test]
        public void ReportForceDetection_OpposingRevealedJediArrive_ReportsToBothFactions()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer arriving = EntityFactory.CreateOfficer("arriving-jedi", "rebels");
            arriving.DisplayName = "Rebel Jedi";
            arriving.IsJedi = true;
            arriving.IsForceEligible = true;
            arriving.ForceValue = 60;
            arriving.MessageImagePath = "rebel-message";
            arriving.EnemyDetectedVoicePaths.Add("rebel-detects");
            Officer present = EntityFactory.CreateOfficer("present-jedi", "empire");
            present.DisplayName = "Imperial Jedi";
            present.IsJedi = true;
            present.IsForceEligible = true;
            present.ForceValue = 60;
            present.MessageImagePath = "empire-message";
            present.EnemyDetectedVoicePaths.Add("empire-detects");
            Fleet arrivingFleet = EntityFactory.CreateFleet("arriving-fleet", "rebels");
            CapitalShip arrivingShip = new CapitalShip
            {
                InstanceID = "arriving-ship",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(arrivingFleet, empirePlanet);
            game.AttachNode(arrivingShip, arrivingFleet);
            game.AttachNode(arriving, arrivingShip);
            game.AttachNode(present, empirePlanet);
            ReportForceDetectionAction action = new ReportForceDetectionAction
            {
                Title = "{subject} Detects Enemy",
                Body = "{subject} detected {relatedSubject}.",
                DetailImageKey = "mission_report",
                VoicePaths = new Dictionary<string, string>
                {
                    { "rebels", "rebel-report" },
                    { "empire", "empire-report" },
                },
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent { InstanceID = "FORCE_DETECTION" },
                new GameEventState(),
                null,
                new UnitArrivedResult { Unit = arrivingFleet, Destination = empirePlanet }
            );

            List<NarrativeMessageResult> messages = action
                .Execute(game, new FixedRandomProvider(new[] { 0.0 }), context)
                .OfType<NarrativeMessageResult>()
                .ToList();

            Assert.AreEqual(2, messages.Count);
            NarrativeMessageResult rebelMessage = messages.Single(message =>
                message.Recipient.InstanceID == "rebels"
            );
            Assert.AreSame(arriving, rebelMessage.Subject);
            Assert.AreSame(present, rebelMessage.RelatedSubject);
            Assert.AreEqual("rebel-message", rebelMessage.OverlayImagePath);
            Assert.AreEqual("rebel-report", rebelMessage.VoicePath);
            Assert.AreEqual("rebel-detects", rebelMessage.OfficerVoicePath);
            NarrativeMessageResult empireMessage = messages.Single(message =>
                message.Recipient.InstanceID == "empire"
            );
            Assert.AreSame(present, empireMessage.Subject);
            Assert.AreSame(arriving, empireMessage.RelatedSubject);
            Assert.AreEqual("empire-detects", empireMessage.OfficerVoicePath);
        }

        [Test]
        public void ReportForceDetection_ForceRankChance_RollsOnceForBothReports()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer arriving = EntityFactory.CreateOfficer("arriving", "rebels");
            arriving.IsJedi = true;
            arriving.IsForceEligible = true;
            arriving.ForceValue = 60;
            Officer present = EntityFactory.CreateOfficer("present", "empire");
            present.IsJedi = true;
            present.IsForceEligible = true;
            present.ForceValue = 60;
            Fleet fleet = EntityFactory.CreateFleet("fleet", "rebels");
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "rebels" };
            game.AttachNode(fleet, empirePlanet);
            game.AttachNode(ship, fleet);
            game.AttachNode(arriving, ship);
            game.AttachNode(present, empirePlanet);
            ReportForceDetectionAction action = new ReportForceDetectionAction
            {
                UseForceRankDetectionChance = true,
                ForceRankDetectionChanceModifier = -100,
                Title = "Detected",
                Body = "Detected",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                new UnitArrivedResult { Unit = fleet, Destination = empirePlanet }
            );

            Assert.IsEmpty(action.Execute(game, new SequenceRNG(new[] { 20 }), context));
            Assert.AreEqual(
                2,
                action
                    .Execute(game, new SequenceRNG(new[] { 19 }), context)
                    .OfType<NarrativeMessageResult>()
                    .Count()
            );
        }

        [Test]
        public void ReportForceDetection_ExcludedOrUnrevealedPair_EmitsNothing()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer arriving = EntityFactory.CreateOfficer("luke", "rebels");
            arriving.IsJedi = true;
            arriving.IsForceEligible = true;
            Officer present = EntityFactory.CreateOfficer("vader", "empire");
            present.IsJedi = true;
            present.IsForceEligible = true;
            Fleet arrivingFleet = EntityFactory.CreateFleet("arriving-fleet", "rebels");
            CapitalShip arrivingShip = new CapitalShip
            {
                InstanceID = "arriving-ship",
                OwnerInstanceID = "rebels",
            };
            game.AttachNode(arrivingFleet, empirePlanet);
            game.AttachNode(arrivingShip, arrivingFleet);
            game.AttachNode(arriving, arrivingShip);
            game.AttachNode(present, empirePlanet);
            ReportForceDetectionAction action = new ReportForceDetectionAction
            {
                Title = "Detected",
                Body = "Detected",
                ExcludedPairs = new List<OfficerPairReference>
                {
                    new OfficerPairReference
                    {
                        FirstOfficerInstanceID = "luke",
                        SecondOfficerInstanceID = "vader",
                    },
                },
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                new UnitArrivedResult { Unit = arrivingFleet, Destination = empirePlanet }
            );

            Assert.IsEmpty(action.Execute(game, new FixedRandomProvider(new[] { 0.0 }), context));

            action.ExcludedPairs.Clear();
            present.IsForceEligible = false;
            Assert.IsEmpty(action.Execute(game, new FixedRandomProvider(new[] { 0.0 }), context));
        }

        [Test]
        public void InformantIntelligence_OwnerSupportFails_EmitsOpposingFactionIntelligence()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            empirePlanet.PopularSupport["empire"] = 20;
            InformantIntelligenceAction action = CreateInformantAction();
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState(),
                empirePlanet
            );

            List<GameResult> results = action.Execute(
                game,
                new FixedRandomProvider(new[] { 0.8, 0.5 }),
                context
            );

            PlanetIntelligenceResult intelligence = results
                .OfType<PlanetIntelligenceResult>()
                .Single();
            NarrativeMessageResult message = results.OfType<NarrativeMessageResult>().Single();
            Assert.AreEqual("rebels", intelligence.Recipient.InstanceID);
            Assert.AreSame(empirePlanet, intelligence.Planet);
            Assert.AreEqual(PlanetIntelligenceCategory.GroundForces, intelligence.Categories);
            Assert.AreSame(intelligence.Recipient, message.Recipient);
            Assert.AreSame(empirePlanet, message.Location);
        }

        [Test]
        public void InformantIntelligence_OwnerSupportSucceeds_EmitsNothing()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            empirePlanet.PopularSupport["empire"] = 20;
            InformantIntelligenceAction action = CreateInformantAction();
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState(),
                empirePlanet
            );

            List<GameResult> results = action.Execute(
                game,
                new FixedRandomProvider(new[] { 0.19 }),
                context
            );

            Assert.IsEmpty(results);
        }

        [Test]
        public void AddMessage_RecipientFromSubject_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.DisplayName = "Luke Skywalker";
            game.AttachNode(luke, rebelPlanet);
            AddMessageAction action = new AddMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                MessageType = MessageType.Advice,
                Title = "A message for {subject}",
                Body = "Report from {location}",
                VoicePath = "Audio/Luke/dialogue",
            };

            NarrativeMessageResult result = action
                .Execute(game)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("rebels", result.Recipient.InstanceID);
            Assert.AreSame(luke, result.Subject);
            Assert.AreSame(rebelPlanet, result.Location);
            Assert.AreEqual("Audio/Luke/dialogue", result.VoicePath);
        }

        [Test]
        public void AddMessage_ConditionalBodySegments_ComposeFromOfficerState()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.InjuryPoints = 12;
            game.AttachNode(luke, rebelPlanet);
            AddMessageAction action = new AddMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                Body = "Luke learned the truth. ",
                BodySegments = new List<NarrativeBodySegment>
                {
                    new NarrativeBodySegment
                    {
                        Conditionals = new List<GameConditional>
                        {
                            new OfficerStateConditional
                            {
                                OfficerInstanceID = luke.InstanceID,
                                State = OfficerStateKind.Injured,
                            },
                        },
                        Body = "Luke was injured.",
                        ElseBody = "Luke escaped unharmed.",
                    },
                },
            };

            NarrativeMessageResult result = action
                .Execute(game)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("Luke learned the truth. Luke was injured.", result.BodyTemplate);
        }

        [Test]
        public void AddMessage_EncounterVoice_UsesTriggerResultPath()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            AddMessageAction action = new AddMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                VoicePath = "fallback",
                VoicePathFromOfficerEncounter = true,
            };
            OfficerEncounterResult encounter = new OfficerEncounterResult
            {
                EncounteredOfficer = luke,
                VoicePath = "selected-encounter-voice",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                encounter
            );

            NarrativeMessageResult result = action
                .Execute(game, game.Random, context)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("selected-encounter-voice", result.VoicePath);
        }

        [Test]
        public void GameEvent_NestedRandomTrigger_UsesOneProviderAndPreservesChildSource()
        {
            GameRoot game = BuildGame(out _, out _);
            GameEvent child = new GameEvent
            {
                InstanceID = "child",
                Actions = new List<GameAction>
                {
                    new AddMessageAction { RecipientFactionInstanceID = "rebels", Title = "Child" },
                },
            };
            GameEvent root = new GameEvent
            {
                InstanceID = "root",
                Actions = new List<GameAction>
                {
                    new RandomOutcomeAction
                    {
                        Probability = 1,
                        Actions = new List<GameAction>
                        {
                            new TriggerEventAction { EventInstanceID = child.InstanceID },
                        },
                    },
                },
            };
            game.EventPool.Add(child);
            game.EventPool.Add(root);

            NarrativeMessageResult result = root.Execute(
                    game,
                    new FixedRandomProvider(new[] { 0d })
                )
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual(child.InstanceID, result.SourceEventInstanceID);
        }

        [Test]
        public void ConditionalAction_EventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.SetEventVariable("luke.stage", 2);
            ConditionalAction action = new ConditionalAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = EventVariableComparison.GreaterThanOrEqual,
                        Value = 2,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "luke.stage",
                        Operation = EventVariableOperation.Add,
                        Value = 1,
                    },
                },
                ElseActions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Value = 1 },
                },
            };

            EventVariableChangedResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0d }))
                .OfType<EventVariableChangedResult>()
                .Single();

            Assert.AreEqual(2, result.PreviousValue);
            Assert.AreEqual(3, result.CurrentValue);
            Assert.AreEqual(3, game.GetEventVariable("luke.stage"));
            Assert.AreEqual(0, game.GetEventVariable("wrong"));
        }

        [Test]
        public void RequestMovement_ValidReferences_EmitsAuthoritativeRequest()
        {
            GameRoot game = BuildGame(out Planet destination, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            RequestMovementAction action = new RequestMovementAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            UnitMovementRequestedResult result = action
                .Execute(game)
                .OfType<UnitMovementRequestedResult>()
                .Single();

            Assert.AreSame(officer, result.Unit);
            Assert.AreSame(destination, result.Destination);
            Assert.AreSame(origin, officer.GetParent());
        }

        [Test]
        public void AddToVoid_ActiveOfficer_RemovesOfficerFromSceneGraph()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);

            new AddToVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            Assert.IsNull(luke.GetParent());
            Assert.IsTrue(game.GetFactionByOwnerInstanceID("rebels").VoidPool.Contains(luke));
        }

        [Test]
        public void SetStatus_OfficerInVoid_SetsVoidStatus()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);
            new AddToVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            new SetStatusAction
            {
                UnitInstanceID = luke.InstanceID,
                Status = VoidStatus.Training,
            }.Execute(game);

            Assert.AreEqual(VoidStatus.Training, luke.VoidState.Status);
        }

        [Test]
        public void ReturnFromVoid_OfficerWithPreviousLocation_ReturnsOfficerToLocation()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);
            new AddToVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            new ReturnFromVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            Assert.AreSame(origin, luke.GetParent());
        }

        [Test]
        public void UpdateOfficerPresentation_ConfiguredValues_UpdatesOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            UpdateOfficerPresentationAction action = new UpdateOfficerPresentationAction
            {
                OfficerInstanceID = luke.InstanceID,
                DisplayImagePath = "jedi-display",
                SmallDisplayImagePath = "jedi-small-display",
                EncyclopediaImagePath = "jedi-encyclopedia",
                UsesAdvancedVoiceLines = true,
            };

            Assert.IsEmpty(action.Execute(game));

            Assert.AreEqual("jedi-display", luke.DisplayImagePath);
            Assert.AreEqual("jedi-small-display", luke.SmallDisplayImagePath);
            Assert.AreEqual("jedi-encyclopedia", luke.EncyclopediaImagePath);
            Assert.IsTrue(luke.UsesAdvancedVoiceLines);
        }

        [Test]
        public void StartStoryCapture_ValidTarget_EmitsConfiguredRequest()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(han, rebelPlanet);
            StartStoryCaptureAction action = new StartStoryCaptureAction
            {
                TargetOfficerInstanceID = han.InstanceID,
                DurationTicks = 1,
                CanEscape = false,
                AttackRating = 12,
                ResistanceRating = OfficerRating.Combat,
                ProbabilityTableKey = AbductionMission.MissionTypeID,
                DisplayName = "Bounty Hunters",
            };

            StoryCaptureRequestedResult result = action
                .Execute(game)
                .OfType<StoryCaptureRequestedResult>()
                .Single();

            Assert.AreSame(han, result.Target);
            Assert.AreEqual(12, result.AttackRating);
            Assert.AreEqual(OfficerRating.Combat, result.ResistanceRating);
            Assert.AreEqual(AbductionMission.MissionTypeID, result.ProbabilityTableKey);
            Assert.AreEqual(1, result.DurationTicks);
            Assert.IsFalse(result.CanEscape);
            Assert.AreEqual("Bounty Hunters", result.DisplayName);
        }

        [Test]
        public void BountyAttack_ValidOfficer_EmitsBountyResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(han, rebelPlanet);

            BountyAttackResult result = new BountyAttackAction
            {
                OfficerInstanceID = han.InstanceID,
            }
                .Execute(game)
                .OfType<BountyAttackResult>()
                .Single();

            Assert.AreSame(han, result.Officer);
        }

        [Test]
        public void StartStoryRescue_ResolvesAuthoredOfficerReferences()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(han, rebelPlanet);
            game.AttachNode(luke, rebelPlanet);

            StoryRescueRequestedResult result = new StartStoryRescueAction
            {
                CaptiveOfficerInstanceID = han.InstanceID,
                RescuerOfficerInstanceIDs = new List<string> { luke.InstanceID },
                DurationTicks = 5,
                DurationRandomTicks = 10,
                RatingDivisor = 3,
                SuccessCombatBonus = 1,
                SuccessEspionageBonus = 1,
                CaptureRescuerOnFailure = true,
            }
                .Execute(game)
                .OfType<StoryRescueRequestedResult>()
                .Single();

            Assert.AreSame(han, result.Captive);
            Assert.AreSame(luke, result.Rescuers.Single());
            Assert.AreEqual(5, result.DurationTicks);
            Assert.AreEqual(10, result.DurationRandomTicks);
            Assert.AreEqual(3, result.RatingDivisor);
            Assert.IsTrue(result.CaptureRescuerOnFailure);
        }

        [Test]
        public void StartStoryPickup_ResolvesCollectorAndPrisonerLocation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(vader, empirePlanet);
            game.AttachNode(han, rebelPlanet);

            List<GameResult> results = new StartStoryPickupAction
            {
                CollectorOfficerInstanceID = vader.InstanceID,
                LocationOfficerInstanceID = han.InstanceID,
                CaptiveFactionInstanceID = "rebels",
                DurationTicks = 1,
                CaptivesCanEscapeAfterPickup = true,
            }.Execute(game);

            StoryPickupRequestedResult request = results
                .OfType<StoryPickupRequestedResult>()
                .Single();
            Assert.AreSame(vader, request.Collector);
            Assert.AreSame(rebelPlanet, request.Location);
            Assert.IsTrue(request.CaptivesCanEscapeAfterPickup);
            Assert.IsTrue(results.OfType<OfficerPickupResult>().Single().InProgress);
        }

        [Test]
        public void StartStoryFinalBattle_ResolvesAuthoredParticipantsAndRules()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            Officer palpatine = EntityFactory.CreateOfficer("palpatine", "empire");
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(vader, empirePlanet);
            game.AttachNode(palpatine, empirePlanet);

            StoryFinalBattleRequestedResult request = new StartStoryFinalBattleAction
            {
                LukeOfficerInstanceID = luke.InstanceID,
                VaderOfficerInstanceID = vader.InstanceID,
                PalpatineOfficerInstanceID = palpatine.InstanceID,
                CaptorFactionInstanceID = "empire",
                DurationTicks = 1,
                VictoryForceRank = 100,
                MinimumFailureInjury = 1,
                MaximumFailureInjury = 200,
                CaptivesCanEscapeOnVictory = true,
            }
                .Execute(game)
                .OfType<StoryFinalBattleRequestedResult>()
                .Single();

            Assert.AreSame(luke, request.Luke);
            Assert.AreSame(vader, request.Vader);
            Assert.AreSame(palpatine, request.Palpatine);
            Assert.AreEqual(100, request.VictoryForceRank);
            Assert.AreEqual(1, request.MinimumFailureInjury);
            Assert.AreEqual(200, request.MaximumFailureInjury);
        }

        [Test]
        public void IncreaseOfficerForce_RankGapReward_UsesMaximumFormula()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            vader.ForceValue = 100;
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(vader, empirePlanet);
            IncreaseOfficerForceAction action = new IncreaseOfficerForceAction
            {
                OfficerInstanceID = luke.InstanceID,
                ReferenceOfficerInstanceID = vader.InstanceID,
                MinimumIncrease = 1,
                PositiveRankGapPercent = 25,
                SuppressRankChangeMessage = true,
            };

            ForceExperienceResult result = action
                .Execute(game)
                .OfType<ForceExperienceResult>()
                .Single();

            Assert.AreEqual(15, result.ExperienceGained);
            Assert.AreEqual(55, luke.ForceValue);
            Assert.IsTrue(result.SuppressRankChangeMessage);
        }

        [Test]
        public void RevealOfficerForcePotential_DormantJedi_InitializesAuthoredForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsJedi = true;
            leia.IsForceEligible = false;
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;
            game.AttachNode(leia, rebelPlanet);
            RevealOfficerForcePotentialAction action = new RevealOfficerForcePotentialAction
            {
                OfficerInstanceID = leia.InstanceID,
            };

            ForceExperienceResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0.5 }))
                .OfType<ForceExperienceResult>()
                .Single();

            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(13, leia.ForceValue);
            Assert.AreEqual(13, result.ExperienceGained);
            Assert.IsTrue(result.SuppressRankChangeMessage);
            Assert.IsEmpty(action.Execute(game, new FixedRandomProvider(new[] { 0.5 })));
        }

        [Test]
        public void ApplyOfficerInjury_InclusiveRange_AppliesRolledSeverity()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            ApplyOfficerInjuryAction action = new ApplyOfficerInjuryAction
            {
                OfficerInstanceID = luke.InstanceID,
                MinimumInjury = 1,
                MaximumInjury = 100,
            };

            OfficerInjuredResult result = action
                .Execute(game, new FixedRandomProvider(new[] { 0.49 }))
                .OfType<OfficerInjuredResult>()
                .Single();

            Assert.AreEqual(50, result.Severity);
            Assert.AreEqual(50, luke.InjuryPoints);
        }

        [Test]
        public void RandomPlanetIncident_ResourceIncrease_UsesAuthoredQuartileAndLimits()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            RandomPlanetIncidentAction action = new RandomPlanetIncidentAction
            {
                ActionType = PlanetIncidentActionType.ResourceChange,
                MaximumRawMaterials = 15,
                MaximumEnergy = 15,
            };

            List<GameResult> results = action.Execute(game, new SequenceRNG(new[] { 0, 2 }));

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            PlanetIncidentResult incident = results.OfType<PlanetIncidentResult>().Single();
            Assert.AreEqual(IncidentType.Resource, incident.IncidentType);
            Assert.AreEqual(PlanetStatType.RawMaterial, incident.ChangedStat);
            Assert.AreEqual(4, incident.OldValue);
            Assert.AreEqual(5, incident.NewValue);
        }

        [Test]
        public void RandomPlanetIncident_NeutralPlanet_StillChangesResourcesWithoutFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            RandomPlanetIncidentAction action = new RandomPlanetIncidentAction
            {
                ActionType = PlanetIncidentActionType.ResourceChange,
            };

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(new[] { 0, 2 }))
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.IsNull(result.Faction);
            Assert.AreEqual(5, planet.NumRawResourceNodes);
        }

        [Test]
        public void RandomPlanetIncident_NaturalDisaster_GuaranteesOneResourceLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            RandomPlanetIncidentAction action = new RandomPlanetIncidentAction
            {
                ActionType = PlanetIncidentActionType.NaturalDisaster,
                DisasterLossProbabilityPerResource = 0,
                FacilityDestructionProbability = 0,
            };

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(2, planet.NumRawResourceNodes);
            Assert.AreEqual(3, planet.EnergyCapacity);
            Assert.AreEqual(1, results.OfType<PlanetStatChangedResult>().Count());
            Assert.AreEqual(
                IncidentType.Disaster,
                results.OfType<PlanetIncidentResult>().Single().IncidentType
            );
        }

        [Test]
        public void RandomPlanetIncident_NaturalDisaster_DestroysAuthoredFacilityTypes()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 1;
            planet.EnergyCapacity = 1;
            Building shipyard = new Building
            {
                InstanceID = "shipyard",
                OwnerInstanceID = planet.OwnerInstanceID,
                BuildingType = BuildingType.Shipyard,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);
            RandomPlanetIncidentAction action = new RandomPlanetIncidentAction
            {
                ActionType = PlanetIncidentActionType.NaturalDisaster,
                DisasterLossProbabilityPerResource = 0,
                FacilityDestructionProbability = 1,
                DisasterFacilityTypes = new List<BuildingType> { BuildingType.Shipyard },
            };

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99));

            Assert.IsFalse(planet.Buildings.Contains(shipyard));
            Assert.AreSame(
                shipyard,
                results.OfType<GameObjectDestroyedResult>().Single().DestroyedObject
            );
            Assert.AreSame(
                shipyard,
                results.OfType<PlanetIncidentResult>().Single().DestroyedObjects.Single()
            );
        }

        [Test]
        public void Chance_Success_ExecutesEveryChildAction()
        {
            GameRoot game = BuildGame(out _, out _);
            ChanceAction action = new ChanceAction
            {
                Probability = 1,
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "first", Value = 1 },
                    new SetEventVariableAction { Key = "second", Value = 2 },
                },
            };

            action.Execute(game, new FixedRNG());

            Assert.AreEqual(1, game.GetEventVariable("first"));
            Assert.AreEqual(2, game.GetEventVariable("second"));
        }

        [Test]
        public void RandomChoice_ExecutesEveryActionInSelectedWeightedChoice()
        {
            GameRoot game = BuildGame(out _, out _);
            RandomChoiceAction action = new RandomChoiceAction
            {
                Choices = new List<RandomChoice>
                {
                    new RandomChoice
                    {
                        Weight = 1,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "wrong", Value = 1 },
                        },
                    },
                    new RandomChoice
                    {
                        Weight = 3,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "first", Value = 1 },
                            new SetEventVariableAction { Key = "second", Value = 2 },
                        },
                    },
                },
            };

            action.Execute(game, new SequenceRNG(new[] { 3 }));

            Assert.Zero(game.GetEventVariable("wrong"));
            Assert.AreEqual(1, game.GetEventVariable("first"));
            Assert.AreEqual(2, game.GetEventVariable("second"));
        }
    }
}
