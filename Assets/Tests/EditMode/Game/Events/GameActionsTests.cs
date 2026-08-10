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
        public void TriggerDuel_ValidIDs_EmitsRequest()
        {
            GameRoot game = BuildGame(out Planet empPlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
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
        public void TriggerDuel_ArrivingSecondOfficer_ReversesAuthoredOrder()
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
            TriggerDuelAction action = new TriggerDuelAction
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
        public void TriggerDuel_ForceRankChance_UsesSummedRankThreshold()
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
            TriggerDuelAction action = new TriggerDuelAction
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
                BackgroundImage = new MessageBackgroundImage { Key = "mission_report" },
                VoicePaths = new List<RecipientVoicePath>
                {
                    new RecipientVoicePath
                    {
                        RecipientFactionInstanceID = "rebels",
                        Path = "rebel-report",
                    },
                    new RecipientVoicePath
                    {
                        RecipientFactionInstanceID = "empire",
                        Path = "empire-report",
                    },
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
                        ExpectedValue = 2,
                    },
                },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "luke.stage",
                        Operation = EventVariableOperation.Add,
                        Operand = 1,
                    },
                },
                ElseActions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Operand = 1 },
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
        public void SetOfficerImages_ConfiguredValues_UpdatesOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerImagesAction action = new SetOfficerImagesAction
            {
                OfficerInstanceID = luke.InstanceID,
                DisplayImagePath = "jedi-display",
                SmallDisplayImagePath = "jedi-small-display",
                EncyclopediaImagePath = "jedi-encyclopedia",
            };

            Assert.IsEmpty(action.Execute(game));

            Assert.AreEqual("jedi-display", luke.DisplayImagePath);
            Assert.AreEqual("jedi-small-display", luke.SmallDisplayImagePath);
            Assert.AreEqual("jedi-encyclopedia", luke.EncyclopediaImagePath);
        }

        [Test]
        public void SetOfficerVoiceSet_ConfiguredValues_ReplacesSelectedVoicePools()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.PersonnelArrivedVoicePaths.Add("old");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerVoiceSetAction action = new SetOfficerVoiceSetAction
            {
                OfficerInstanceID = luke.InstanceID,
                PersonnelArrivedVoicePaths = new List<string> { "jedi-arrived" },
            };

            Assert.IsEmpty(action.Execute(game));

            CollectionAssert.AreEqual(new[] { "jedi-arrived" }, luke.PersonnelArrivedVoicePaths);
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
        public void StartMission_ValidRoles_EmitsDefinitionRequest()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            game.AttachNode(han, rebelPlanet);
            StartMissionAction action = new StartMissionAction
            {
                MissionDefinitionID = "BOUNTY_HUNTER_CAPTURE",
                Roles = new List<MissionRoleAssignment>
                {
                    new MissionRoleAssignment { Name = "Target", UnitInstanceID = han.InstanceID },
                },
            };

            CustomMissionRequestedResult result = action
                .Execute(game)
                .OfType<CustomMissionRequestedResult>()
                .Single();

            Assert.AreEqual("BOUNTY_HUNTER_CAPTURE", result.MissionDefinitionID);
            Assert.AreEqual("Target", result.Roles.Single().Name);
            Assert.AreEqual(han.InstanceID, result.Roles.Single().UnitInstanceID);
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
        public void RevealOfficerForcePotential_AuthoredOfficer_ActivatesAndInitializesForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsJedi = false;
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

            Assert.IsTrue(leia.IsJedi);
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
        public void ChangeResources_ResourceIncrease_UsesAuthoredQuartileAndLimits()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeResourcesAction action = new ChangeResourcesAction
            {
                MaximumRawMaterials = 15,
                MaximumEnergy = 15,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                planet
            );

            List<GameResult> results = action.Execute(game, new SequenceRNG(new[] { 2 }), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            PlanetIncidentResult incident = results.OfType<PlanetIncidentResult>().Single();
            Assert.AreEqual(IncidentType.Resource, incident.IncidentType);
            Assert.AreEqual(PlanetStatType.RawMaterial, incident.ChangedStat);
            Assert.AreEqual(4, incident.OldValue);
            Assert.AreEqual(5, incident.NewValue);
        }

        [Test]
        public void ChangeResources_NeutralPlanet_StillChangesResourcesWithoutFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeResourcesAction action = new ChangeResourcesAction();
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                planet
            );

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(new[] { 2 }), context)
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.IsNull(result.Faction);
            Assert.AreEqual(5, planet.NumRawResourceNodes);
        }

        [Test]
        public void ReduceResources_MinimumLoss_GuaranteesOneResourceLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            ReduceResourcesAction action = new ReduceResourcesAction
            {
                LossProbabilityPerResource = 0,
                MinimumTotalLoss = 1,
            };
            GameEvent gameEvent = new GameEvent { InstanceID = "disaster" };
            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                null,
                planet
            );

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
            Assert.AreEqual(3, planet.EnergyCapacity);
            Assert.AreEqual(1, results.OfType<PlanetStatChangedResult>().Count());
            Assert.AreEqual(
                IncidentType.Disaster,
                results.OfType<PlanetIncidentResult>().Single().IncidentType
            );
        }

        [Test]
        public void DestroyUnits_BuildingCandidate_AddsFacilityToDisasterResult()
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
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "disaster",
                Actions = new List<GameAction>
                {
                    new ReduceResourcesAction
                    {
                        LossProbabilityPerResource = 0,
                        MinimumTotalLoss = 1,
                    },
                    new DestroyUnitsAction
                    {
                        ChancePerUnit = 1,
                        Candidates = new DestroyUnitCandidates
                        {
                            Buildings = new BuildingCandidates
                            {
                                BuildingTypes = new List<BuildingType> { BuildingType.Shipyard },
                            },
                        },
                    },
                },
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                null,
                planet
            );

            List<GameResult> results = gameEvent.Execute(game, new FixedRNG(0.99), context);

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
                    new SetEventVariableAction { Key = "first", Operand = 1 },
                    new SetEventVariableAction { Key = "second", Operand = 2 },
                },
            };

            action.Execute(game, new FixedRNG());

            Assert.AreEqual(1, game.GetEventVariable("first"));
            Assert.AreEqual(2, game.GetEventVariable("second"));
        }

        [Test]
        public void RandomChoice_WeightedSelection_ExecutesEveryActionInSelectedChoice()
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
                            new SetEventVariableAction { Key = "wrong", Operand = 1 },
                        },
                    },
                    new RandomChoice
                    {
                        Weight = 3,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "first", Operand = 1 },
                            new SetEventVariableAction { Key = "second", Operand = 2 },
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
