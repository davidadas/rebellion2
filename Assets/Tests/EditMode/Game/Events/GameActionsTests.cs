using System;
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
            attacker.ForceValue = 100;
            defender.ForceValue = 100;
            game.AttachNode(attacker, empPlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = "a1",
                SecondOfficerInstanceID = "d1",
            };

            List<GameResult> results = action.Execute(game);

            OfficerEncounterRequestedResult request = results
                .OfType<OfficerEncounterRequestedResult>()
                .Single();
            Assert.AreSame(attacker, request.EncounteredOfficer);
            Assert.AreSame(defender, request.OpposingOfficer);
        }

        [Test]
        public void TriggerDuel_SecondOfficerParticipated_ReversesAuthoredOrder()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer vader = EntityFactory.CreateOfficer("vader", "empire");
            luke.ForceValue = 100;
            vader.ForceValue = 100;
            game.AttachNode(luke, rebelPlanet);
            vader.IsCaptured = true;
            game.AttachNode(vader, rebelPlanet);
            vader.IsCaptured = false;
            MissionCompletedResult completion = new MissionCompletedResult
            {
                Participants = new List<IMissionParticipant> { vader },
            };
            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
                AudioPath = "encounter-voice",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                completion
            );

            OfficerEncounterRequestedResult request = action
                .Execute(game, game.Random, context)
                .OfType<OfficerEncounterRequestedResult>()
                .Single();

            Assert.AreSame(vader, request.EncounteredOfficer);
            Assert.AreSame(luke, request.OpposingOfficer);
            Assert.AreEqual("encounter-voice", request.AudioPath);
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
                FirstOfficerInstanceID = luke.InstanceID,
                SecondOfficerInstanceID = vader.InstanceID,
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
        public void SendMessage_RecipientFromSubject_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.DisplayName = "Luke Skywalker";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                MessageType = MessageType.Advice,
                Subject = "A message for {subject}",
                Body = "Report from {location}",
                AmbientAudio = new MessageAudio { Path = "Audio/Luke/dialogue" },
            };

            NarrativeMessageResult result = action
                .Execute(game)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("rebels", result.Recipient.InstanceID);
            Assert.AreSame(luke, result.SubjectNode);
            Assert.AreSame(rebelPlanet, result.Location);
            Assert.AreEqual("Audio/Luke/dialogue", result.AudioPath);
        }

        [Test]
        public void SendMessage_ConditionalBodySegments_ComposeFromOfficerState()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.InjuryPoints = 12;
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                Body = "Luke learned the truth. ",
                BodySegments = new List<NarrativeBodySegment>
                {
                    new NarrativeBodySegment
                    {
                        Conditionals = new List<GameConditional>
                        {
                            new IsInjuredConditional { OfficerInstanceID = luke.InstanceID },
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

            Assert.AreEqual("Luke learned the truth. Luke was injured.", result.Body);
        }

        [Test]
        public void SendMessage_EncounterVoice_UsesTriggerResultPath()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
            };
            OfficerEncounterResult encounter = new OfficerEncounterResult
            {
                EncounteredOfficer = luke,
                AudioPath = "selected-encounter-voice",
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

            Assert.AreEqual("selected-encounter-voice", result.AudioPath);
        }

        [Test]
        public void SendMessage_OfficerVoicePreset_UsesSubjectVoiceSet()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MissionSuccessVoicePaths.Add("luke-success");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                OfficerVoice = new MessageOfficerVoice
                {
                    Preset = OfficerVoiceLineType.MissionSuccess,
                },
            };

            NarrativeMessageResult result = action
                .Execute(game, new FixedRNG(0), null)
                .OfType<NarrativeMessageResult>()
                .Single();

            Assert.AreEqual("luke-success", result.OfficerVoicePath);
        }

        [Test]
        public void IfAction_EventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.SetEventVariable("luke.stage", 2);
            IfAction action = new IfAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EvaluateEventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = EventVariableComparison.GreaterThanOrEqual,
                        Value = 2,
                    },
                },
                Then = new GameActionBlock
                {
                    Actions = new List<GameAction>
                    {
                        new SetEventVariableAction
                        {
                            Key = "luke.stage",
                            Operation = EventVariableOperation.Add,
                            Operand = 1,
                        },
                    },
                },
                Else = new GameActionBlock
                {
                    Actions = new List<GameAction>
                    {
                        new SetEventVariableAction { Key = "wrong", Operand = 1 },
                    },
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
        public void CreateMission_ValidTargetAndParticipants_EmitsDefinitionRequest()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            game.AttachNode(han, rebelPlanet);
            game.AttachNode(luke, rebelPlanet);
            game.AttachNode(leia, rebelPlanet);
            CreateMissionAction action = new CreateMissionAction
            {
                MissionDefinitionID = "BOUNTY_HUNTER_CAPTURE",
                Target = new MissionUnitReference { UnitInstanceID = han.InstanceID },
                Participants = new List<MissionUnitReference>
                {
                    new MissionUnitReference { UnitInstanceID = luke.InstanceID },
                },
                Decoys = new List<MissionUnitReference>
                {
                    new MissionUnitReference { UnitInstanceID = leia.InstanceID },
                },
            };

            CustomMissionRequestedResult result = action
                .Execute(game)
                .OfType<CustomMissionRequestedResult>()
                .Single();

            Assert.AreEqual("BOUNTY_HUNTER_CAPTURE", result.MissionDefinitionID);
            Assert.AreEqual(han.InstanceID, result.TargetInstanceID);
            CollectionAssert.AreEqual(new[] { luke.InstanceID }, result.MainParticipantInstanceIDs);
            CollectionAssert.AreEqual(
                new[] { leia.InstanceID },
                result.DecoyParticipantInstanceIDs
            );
        }

        [Test]
        public void AdjustOfficerForce_Percent_AdjustsCurrentRank()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            game.AttachNode(luke, rebelPlanet);
            AdjustOfficerForceAction action = new AdjustOfficerForceAction
            {
                OfficerInstanceID = luke.InstanceID,
                Percent = 25,
            };

            ForceExperienceResult result = action
                .Execute(game)
                .OfType<ForceExperienceResult>()
                .Single();

            Assert.AreEqual(10, result.ExperienceGained);
            Assert.AreEqual(50, luke.ForceValue);
            Assert.IsTrue(result.SuppressRankChangeMessage);
        }

        [Test]
        public void AdjustOfficerRating_Amount_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.Diplomacy, 40);
            game.AttachNode(luke, rebelPlanet);

            Assert.IsEmpty(
                new AdjustOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = OfficerRating.Diplomacy,
                    Amount = 5,
                }.Execute(game)
            );

            Assert.AreEqual(45, luke.GetBaseRating(OfficerRating.Diplomacy));
        }

        [Test]
        public void AdjustOfficerRating_Percent_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.ShipResearch, 40);
            game.AttachNode(luke, rebelPlanet);

            new AdjustOfficerRatingAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.ShipResearch,
                Percent = -25,
            }.Execute(game);

            Assert.AreEqual(30, luke.GetBaseRating(OfficerRating.ShipResearch));
        }

        [Test]
        public void AdjustOfficerRating_MultipleAdjustmentModes_Throws()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);

            Assert.Throws<InvalidOperationException>(() =>
                new AdjustOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = OfficerRating.Combat,
                    Amount = 5,
                    Percent = 10,
                }.Execute(game)
            );
        }

        [Test]
        public void SetOfficerForceState_EligibilityTransition_InitializesForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsJedi = false;
            leia.IsForceEligible = false;
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;
            game.AttachNode(leia, rebelPlanet);
            SetOfficerForceStateAction action = new SetOfficerForceStateAction
            {
                OfficerInstanceID = leia.InstanceID,
                IsJedi = true,
                IsEligible = true,
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
            action.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(13, leia.ForceValue);
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
        public void AdjustPlanetResource_RawMaterials_IncreasesExplicitAmount()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            AdjustPlanetResourceAction action = new AdjustPlanetResourceAction
            {
                Resource = PlanetResource.RawMaterials,
                Amount = 1,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                planet
            );

            List<GameResult> results = action.Execute(game, new SequenceRNG(), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            PlanetIncidentResult incident = results.OfType<PlanetIncidentResult>().Single();
            Assert.AreEqual(IncidentType.Resource, incident.IncidentType);
            Assert.AreEqual(PlanetStatType.RawMaterial, incident.ChangedStat);
            Assert.AreEqual(4, incident.OldValue);
            Assert.AreEqual(5, incident.NewValue);
        }

        [Test]
        public void AdjustPlanetResource_NeutralPlanet_ReportsNoFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            AdjustPlanetResourceAction action = new AdjustPlanetResourceAction
            {
                Resource = PlanetResource.RawMaterials,
                Amount = 1,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                planet
            );

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(), context)
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
                        Selectors = new List<UnitSelector>
                        {
                            new SelectRandomUnits
                            {
                                ChancePercent = 100,
                                Queries = new List<SelectUnits>
                                {
                                    new SelectUnits
                                    {
                                        PlanetInstanceID = planet.InstanceID,
                                        UnitCategory = UnitCategory.ManufacturingFacility,
                                    },
                                },
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
        public void Random_WeightedSelection_ExecutesEveryActionInSelectedOutcome()
        {
            GameRoot game = BuildGame(out _, out _);
            RandomAction action = new RandomAction
            {
                Outcomes = new List<RandomOutcome>
                {
                    new RandomOutcome
                    {
                        Weight = 1,
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction { Key = "wrong", Operand = 1 },
                        },
                    },
                    new RandomOutcome
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
