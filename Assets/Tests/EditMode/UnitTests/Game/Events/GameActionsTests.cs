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
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    internal static class GameActionTestExtensions
    {
        internal static List<GameResult> Execute(this GameAction action, GameRoot game)
        {
            GameActionContext context = new GameActionContext(game, game.Random);
            action.Execute(context);
            return context.Results;
        }

        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random
        )
        {
            GameActionContext context = new GameActionContext(game, random);
            action.Execute(context);
            return context.Results;
        }

        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random,
            GameEventEvaluationContext evaluation
        )
        {
            GameActionContext context = new GameActionContext(game, random, evaluation);
            action.Execute(context);
            return context.Results;
        }

        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            UnitFactory unitFactory
        )
        {
            GameActionContext context = new GameActionContext(game, game.Random, null, unitFactory);
            action.Execute(context);
            return context.Results;
        }

        internal static List<GameRequest> ExecuteRequests(this GameAction action, GameRoot game)
        {
            GameActionContext context = new GameActionContext(game, game.Random);
            action.Execute(context);
            return context.Requests;
        }

        internal static List<GameRequest> ExecuteRequests(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random,
            GameEventEvaluationContext evaluation
        )
        {
            GameActionContext context = new GameActionContext(game, random, evaluation);
            action.Execute(context);
            return context.Requests;
        }

        internal static List<GameRequest> ExecuteRequests(
            this GameAction action,
            GameRoot game,
            UnitFactory unitFactory
        )
        {
            GameActionContext context = new GameActionContext(game, game.Random, null, unitFactory);
            action.Execute(context);
            return context.Requests;
        }
    }

    [TestFixture]
    public class GameActionsTests
    {
        [Test]
        public void PlaceUnits_MixedExistingAndSpawnSources_EmitsPlacementBatch()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            Officer officer = new Officer
            {
                InstanceID = "existing-officer",
                OwnerInstanceID = "empire",
            };
            game.AttachNode(officer, destination);
            Starfighter fighterTemplate = new Starfighter
            {
                TypeID = "X_WING",
                DisplayName = "X-Wing",
            };
            Regiment regimentTemplate = new Regiment
            {
                TypeID = "ALLIANCE_REGIMENT",
                DisplayName = "Alliance Regiment",
            };
            UnitFactory factory = new UnitFactory(
                Array.Empty<Building>(),
                Array.Empty<CapitalShip>(),
                new[] { fighterTemplate },
                new[] { regimentTemplate },
                Array.Empty<SpecialForces>()
            );
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                DestinationInstanceID = destination.InstanceID,
                Units = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                    new SpawnUnits
                    {
                        TypeID = "X_WING",
                        Count = 2,
                        OwnerFactionInstanceID = "empire",
                    },
                    new SpawnUnits
                    {
                        TypeID = "ALLIANCE_REGIMENT",
                        OwnerFactionInstanceID = "empire",
                    },
                },
            };

            UnitPlacementRequest result = action
                .ExecuteRequests(game, factory)
                .OfType<UnitPlacementRequest>()
                .Single();

            Assert.AreEqual(4, result.Units.Count);
            Assert.AreSame(officer, result.Units.OfType<Officer>().Single());
            Assert.AreEqual(2, result.Units.OfType<Starfighter>().Count());
            Assert.AreEqual(1, result.Units.OfType<Regiment>().Count());
            Assert.AreSame(destination, officer.GetParent());
            Assert.IsTrue(
                result
                    .Units.Where(unit => unit != officer)
                    .Cast<ISceneNode>()
                    .All(unit => unit.GetParent() == null)
            );
            Assert.IsTrue(
                result.Units.Cast<ISceneNode>().All(unit => unit.OwnerInstanceID == "empire")
            );
            Assert.AreSame(destination, result.Destinations.Single());
        }

        [Test]
        public void PlaceUnits_SpawnSources_RoundTripsAuthoredStructure()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "SPAWN_REINFORCEMENTS",
                Actions = new List<GameAction>
                {
                    new PlaceUnitsAction
                    {
                        DestinationInstanceID = "NABOO",
                        Units = new List<GameEventSelector>
                        {
                            new SpawnUnits
                            {
                                TypeID = "X_WING",
                                Count = 3,
                                OwnerFactionInstanceID = "FNALL1",
                            },
                            new SpawnUnits
                            {
                                TypeID = "ALLIANCE_REGIMENT",
                                Count = 2,
                                OwnerFactionInstanceID = "FNALL1",
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restoredEvent = SerializationHelper.Deserialize<GameEvent>(xml);
            PlaceUnitsAction restored = restoredEvent.Actions.OfType<PlaceUnitsAction>().Single();

            StringAssert.Contains("<PlaceUnits DestinationInstanceID=\"NABOO\">", xml);
            StringAssert.Contains("<SpawnUnits", xml);
            StringAssert.Contains("TypeID=\"X_WING\"", xml);
            Assert.AreEqual("NABOO", restored.DestinationInstanceID);
            SpawnUnits[] sources = restored.Units.OfType<SpawnUnits>().ToArray();
            Assert.AreEqual(2, sources.Length);
            Assert.AreEqual("X_WING", sources[0].TypeID);
            Assert.AreEqual(3, sources[0].Count);
            Assert.AreEqual("FNALL1", sources[0].OwnerFactionInstanceID);
            Assert.AreEqual("ALLIANCE_REGIMENT", sources[1].TypeID);
            Assert.AreEqual(2, sources[1].Count);
            Assert.AreEqual("FNALL1", sources[1].OwnerFactionInstanceID);
        }

        [Test]
        public void PlaceUnits_AuthoredSpawnSources_DeserializesStructure()
        {
            const string xml =
                @"
                <PlaceUnits DestinationInstanceID=""NABOO"">
                  <Units>
                    <SpawnUnits TypeID=""X_WING"" Count=""3"" OwnerFactionInstanceID=""FNALL1""/>
                    <SpawnUnits TypeID=""ALLIANCE_REGIMENT"" Count=""2"" OwnerFactionInstanceID=""FNALL1""/>
                  </Units>
                </PlaceUnits>";

            PlaceUnitsAction action = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("NABOO", action.DestinationInstanceID);
            SpawnUnits[] sources = action.Units.OfType<SpawnUnits>().ToArray();
            Assert.AreEqual(2, sources.Length);
            Assert.AreEqual("X_WING", sources[0].TypeID);
            Assert.AreEqual(3, sources[0].Count);
            Assert.AreEqual("FNALL1", sources[0].OwnerFactionInstanceID);
            Assert.AreEqual("ALLIANCE_REGIMENT", sources[1].TypeID);
            Assert.AreEqual(2, sources[1].Count);
            Assert.AreEqual("FNALL1", sources[1].OwnerFactionInstanceID);
        }

        [Test]
        public void PlaceUnits_AuthoredSelectors_DeserializesStructure()
        {
            const string xml =
                @"
                <PlaceUnits>
                  <Units>
                    <SelectBinding Binding=""participants""/>
                  </Units>
                  <Destination>
                    <SelectFirst>
                      <From>
                        <SelectPreviousLocation UnitInstanceID=""LUKE_SKYWALKER""/>
                        <SelectPlanets InstanceID=""YAVIN""/>
                      </From>
                    </SelectFirst>
                  </Destination>
                </PlaceUnits>";

            PlaceUnitsAction action = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("participants", action.Units.OfType<SelectBinding>().Single().Binding);
            SelectFirst destination = action.Destination.OfType<SelectFirst>().Single();
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                destination.Selectors.OfType<SelectPreviousLocation>().Single().UnitInstanceID
            );
            Assert.AreEqual(
                "YAVIN",
                destination.Selectors.OfType<SelectPlanets>().Single().InstanceID
            );
        }

        [Test]
        public void PlaceUnits_InactiveExistingUnit_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            Officer officer = new Officer
            {
                InstanceID = "inactive-officer",
                OwnerInstanceID = "empire",
                IsEnabled = false,
            };
            game.AttachNode(officer, destination);
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.ExecuteRequests(game)
            );

            StringAssert.Contains("requires existing units to be active", exception.Message);
        }

        [Test]
        public void PlaceUnits_Selectors_RoundTripsTransferStructure()
        {
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                Units = new List<GameEventSelector>
                {
                    new SelectBinding { Binding = "participants" },
                },
                Destination = new List<GameEventSelector>
                {
                    new SelectFirst
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPreviousLocation { UnitInstanceID = "LUKE_SKYWALKER" },
                            new SelectPlanets { InstanceID = "YAVIN" },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            PlaceUnitsAction restored = (PlaceUnitsAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual(
                "participants",
                restored.Units.OfType<SelectBinding>().Single().Binding
            );
            SelectFirst destination = restored.Destination.OfType<SelectFirst>().Single();
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                destination.Selectors.OfType<SelectPreviousLocation>().Single().UnitInstanceID
            );
            Assert.AreEqual(
                "YAVIN",
                destination.Selectors.OfType<SelectPlanets>().Single().InstanceID
            );
        }

        [Test]
        public void ChangeOwner_UnitSelectors_EmitsOwnershipRequest()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = new Officer { InstanceID = "officer", OwnerInstanceID = "empire" };
            game.AttachNode(officer, planet);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Units = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };

            OwnershipChangeRequest result = action
                .ExecuteRequests(game)
                .OfType<OwnershipChangeRequest>()
                .Single();

            Assert.AreEqual("rebels", result.NewOwner.InstanceID);
            Assert.AreSame(officer, result.Units.Single());
            Assert.IsEmpty(result.Planets);
        }

        [Test]
        public void ChangeOwner_WithPlanetsAndUnits_RejectsAmbiguousRequest()
        {
            GameRoot game = BuildGame(out _, out _);
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "rebels",
                Planets = new List<GameEventSelector> { new SelectPlanets() },
                Units = new List<GameEventSelector> { new SelectOfficers() },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("exactly one", exception.Message);
        }

        [Test]
        public void ChangeOwner_PlanetSelectors_RoundTripsAuthoredStructure()
        {
            ChangeOwnerAction action = new ChangeOwnerAction
            {
                FactionInstanceID = "FNALL1",
                Planets = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = "NABOO" },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            ChangeOwnerAction restored = (ChangeOwnerAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            Assert.AreEqual("FNALL1", restored.FactionInstanceID);
            Assert.AreEqual("NABOO", restored.Planets.OfType<SelectPlanets>().Single().InstanceID);
            Assert.IsEmpty(restored.Units);
        }

        [Test]
        public void SetNodeState_Attributes_DeserializeState()
        {
            SetNodeStateAction action = (SetNodeStateAction)
                SerializationHelper.Deserialize<GameAction>(
                    "<SetNodeState InstanceID=\"LUKE_SKYWALKER\" State=\"Inactive\"/>"
                );

            Assert.AreEqual("LUKE_SKYWALKER", action.InstanceID);
            Assert.AreEqual(SceneNodeState.Inactive, action.State);
        }

        [Test]
        public void SetNodeState_InactiveOfficerSelector_RoundTripsSelector()
        {
            SetNodeStateAction action = new SetNodeStateAction
            {
                State = SceneNodeState.Active,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetBinding = "destination",
                        IsCaptured = true,
                        IncludeInactive = true,
                    },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            SetNodeStateAction restored = (SetNodeStateAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            SelectOfficers selector = restored.Selectors.OfType<SelectOfficers>().Single();
            Assert.AreEqual(SceneNodeState.Active, restored.State);
            Assert.AreEqual("destination", selector.PlanetBinding);
            Assert.AreEqual(true, selector.IsCaptured);
            Assert.IsTrue(selector.IncludeInactive);
        }

        [Test]
        public void SetNodeState_Inactive_DisablesOfficerWithoutDetachingIt()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);

            new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsFalse(officer.IsActive());
        }

        [Test]
        public void SetNodeState_InactiveMissionParticipant_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = "rebels",
                LocationInstanceID = rebelPlanet.InstanceID,
            };
            game.AttachNode(officer, rebelPlanet);
            game.AttachNode(mission, rebelPlanet);
            game.MoveNode(officer, mission);
            mission.Initiate(100);

            TestDelegate execute = () =>
                new SetNodeStateAction
                {
                    InstanceID = officer.InstanceID,
                    State = SceneNodeState.Inactive,
                }.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
            Assert.AreSame(mission, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void SetNodeState_Selector_DisablesEveryMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer first = EntityFactory.CreateOfficer("first", "rebels");
            Officer second = EntityFactory.CreateOfficer("second", "rebels");
            game.AttachNode(first, rebelPlanet);
            game.AttachNode(second, rebelPlanet);

            new SetNodeStateAction
            {
                State = SceneNodeState.Inactive,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers { PlanetInstanceID = rebelPlanet.InstanceID },
                },
            }.Execute(game);

            Assert.IsFalse(first.IsActive());
            Assert.IsFalse(second.IsActive());
        }

        [Test]
        public void SetNodeState_Active_EnablesOfficerAtExistingParent()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;

            List<GameResult> results = new SetNodeStateAction
            {
                InstanceID = officer.InstanceID,
                State = SceneNodeState.Active,
            }.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void SetNodeState_Planet_DisablesNonMovableNode()
        {
            GameRoot game = BuildGame(out _, out Planet planet);

            new SetNodeStateAction
            {
                InstanceID = planet.InstanceID,
                State = SceneNodeState.Inactive,
            }.Execute(game);

            Assert.IsFalse(planet.IsActive());
        }

        [Test]
        public void SetNodeState_InactiveOfficerSelector_EnablesMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            officer.IsCaptured = true;
            game.AttachNode(officer, rebelPlanet);
            officer.IsEnabled = false;
            SetNodeStateAction action = new SetNodeStateAction
            {
                State = SceneNodeState.Active,
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetInstanceID = rebelPlanet.InstanceID,
                        IsCaptured = true,
                        IncludeInactive = true,
                    },
                },
            };

            action.Execute(game);

            Assert.AreSame(rebelPlanet, officer.GetParent());
            Assert.IsTrue(officer.IsActive());
        }

        [Test]
        public void TriggerDuel_ValidIDs_EmitsRequest()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out Planet rebelPlanet);
            Officer attacker = EntityFactory.CreateOfficer("a1", "empire");
            Officer defender = EntityFactory.CreateOfficer("d1", "rebels");
            attacker.ForceValue = 100;
            defender.ForceValue = 100;
            game.AttachNode(attacker, empirePlanet);
            game.AttachNode(defender, rebelPlanet);

            TriggerDuelAction action = new TriggerDuelAction
            {
                FirstOfficerInstanceID = "a1",
                SecondOfficerInstanceID = "d1",
            };

            List<GameRequest> requests = action.ExecuteRequests(game);

            DuelRequest request = requests.OfType<DuelRequest>().Single();
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
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                completion
            );

            DuelRequest request = action
                .ExecuteRequests(game, game.Random, context)
                .OfType<DuelRequest>()
                .Single();

            Assert.AreSame(vader, request.EncounteredOfficer);
            Assert.AreSame(luke, request.OpposingOfficer);
            Assert.AreEqual("encounter-voice", request.AudioPath);
        }

        [Test]
        public void TriggerDuel_ValidOfficers_RequestsDuel()
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

            IEnumerable<GameRequest> requests = action.ExecuteRequests(
                game,
                new SequenceRNG(new[] { 20 }),
                null
            );

            Assert.AreEqual(1, requests.OfType<DuelRequest>().Count());
        }

        [Test]
        public void RevealToFaction_Targets_DeserializeSelectors()
        {
            RevealToFactionAction action = (RevealToFactionAction)
                SerializationHelper.Deserialize<GameAction>(
                    "<RevealToFaction FactionInstanceID=\"FNALL1\"><Targets><SelectPlanets InstanceID=\"NABOO\"/></Targets></RevealToFaction>"
                );

            Assert.AreEqual("FNALL1", action.FactionInstanceID);
            Assert.AreEqual("NABOO", action.Targets.OfType<SelectPlanets>().Single().InstanceID);
        }

        [Test]
        public void RevealToFaction_SelectedOfficer_EmitsConcreteObservation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, empirePlanet);
            RevealToFactionAction action = new RevealToFactionAction
            {
                FactionInstanceID = "rebels",
                Targets = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState()
            );

            List<GameResult> results = action.Execute(game, game.Random, context);

            IntelligenceRevealedResult intelligence = results
                .OfType<IntelligenceRevealedResult>()
                .Single();
            Assert.AreEqual("rebels", intelligence.Recipient.InstanceID);
            CollectionAssert.AreEqual(new[] { officer }, intelligence.Observations);
        }

        [Test]
        public void SendMessage_ExplicitRecipient_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.DisplayName = "Luke Skywalker";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                MessageType = MessageType.Advice,
                Subject = "A message for {subject}",
                Body = "Report from {location}",
                BackgroundAudio = new MessageAudio { Path = "Audio/Luke/dialogue" },
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("rebels", result.Recipient.InstanceID);
            Assert.AreSame(luke, result.SubjectNode);
            Assert.AreSame(rebelPlanet, result.Location);
            Assert.AreEqual("Audio/Luke/dialogue", result.BackgroundAudioPath);
        }

        [Test]
        public void SendMessage_OfficerSubject_DoesNotIncludeSubjectImageByDefault()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.IsNull(result.OverlayImagePath);
        }

        [Test]
        public void SendMessage_ShowSubjectImage_IncludesOfficerMessageImage()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                ShowSubjectImage = true,
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("Officers/Luke/message", result.OverlayImagePath);
        }

        [Test]
        public void SendMessage_ExplicitOverlayImage_UsesAuthoredImage()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.MessageImagePath = "Officers/Luke/message";
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                OverlayImage = new MessageImage { Path = "Story/portrait" },
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("Story/portrait", result.OverlayImagePath);
        }

        [Test]
        public void SendMessage_RecipientOmitted_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out _);
            SendMessageAction action = new SendMessageAction();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            Assert.AreEqual("SendMessage requires RecipientFactionInstanceID.", exception.Message);
        }

        [Test]
        public void SendMessage_InactiveSubject_EmitsResolvedResult()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            luke.IsEnabled = false;
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                Subject = "Rescue failed",
                Body = "Luke remains captured.",
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("rebels", result.Recipient.InstanceID);
            Assert.AreSame(luke, result.SubjectNode);
            Assert.AreSame(rebelPlanet, result.Location);
        }

        [Test]
        public void SendMessage_AudioBinding_UsesTriggerBindingPath()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                BackgroundAudio = new MessageAudio { Binding = "audioPath" },
            };
            DuelResult encounter = new DuelResult
            {
                EncounteredOfficer = luke,
                AudioPath = "selected-encounter-voice",
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                encounter,
                new DuelCompletedTrigger
                {
                    Bindings = new List<GameEventBinding>
                    {
                        new GameEventBinding { Argument = "AudioPath", As = "audioPath" },
                    },
                }
            );

            MessageDeliveryRequest result = action
                .ExecuteRequests(game, game.Random, context)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("selected-encounter-voice", result.BackgroundAudioPath);
        }

        [Test]
        public void SendMessage_OfficerVoicePreset_UsesSubjectVoiceSet()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.VoiceSet.MissionSuccessPaths.Add("luke-success");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = luke.InstanceID,
                OfficerVoice = new MessageOfficerVoice
                {
                    Preset = OfficerVoiceLineType.MissionSuccess,
                },
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game, new FixedRNG(0), null)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("luke-success", result.OfficerVoicePath);
        }

        [Test]
        public void SendMessage_MultipleBackgroundSources_ThrowsException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer officer = EntityFactory.CreateOfficer("officer", rebelPlanet.OwnerInstanceID);
            game.AttachNode(officer, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                RecipientFactionInstanceID = "rebels",
                SubjectInstanceID = officer.InstanceID,
                BackgroundImage = new MessageBackgroundImage
                {
                    Key = "advice",
                    Path = "custom-background",
                },
            };

            Assert.Throws<InvalidOperationException>(() => action.Execute(game));
        }

        [Test]
        public void IfAction_EventVariable_SelectsBranchAndPersistsMutation()
        {
            GameRoot game = BuildGame(out _, out _);
            game.EventRuntime.SetVariable("luke.stage", 2);
            IfAction action = new IfAction
            {
                Conditionals = new List<GameConditional>
                {
                    new EvaluateEventVariableConditional
                    {
                        Key = "luke.stage",
                        Comparison = ComparisonOperator.GreaterThanOrEqual,
                        CompareTo = 2,
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
                Else = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "wrong", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRandomProvider(new[] { 0d }));

            Assert.IsEmpty(results);
            Assert.AreEqual(3, game.EventRuntime.GetVariable("luke.stage"));
            Assert.AreEqual(0, game.EventRuntime.GetVariable("wrong"));
        }

        [Test]
        public void SendUnits_ValidReferences_EmitsAuthoritativeRequest()
        {
            GameRoot game = BuildGame(out Planet destination, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            SendUnitsAction action = new SendUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                DestinationInstanceID = destination.InstanceID,
            };

            UnitMovementRequest result = action
                .ExecuteRequests(game)
                .OfType<UnitMovementRequest>()
                .Single();

            CollectionAssert.AreEqual(new[] { officer }, result.Units);
            CollectionAssert.AreEqual(new[] { destination }, result.Destinations);
            Assert.AreSame(origin, officer.GetParent());
        }

        [Test]
        public void SendUnits_IncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet destination, out _);
            SendUnitsAction action = new SendUnitsAction
            {
                DestinationInstanceID = destination.InstanceID,
                Units = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = destination.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only movable units", exception.Message);
        }

        [Test]
        public void SendUnits_SelectFirstDestination_EmitsAllOrderedCandidates()
        {
            GameRoot game = BuildGame(out Planet first, out Planet origin);
            Planet second = new Planet
            {
                InstanceID = "second",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(second, first.GetParent());
            Officer officer = EntityFactory.CreateOfficer("traveler", "rebels");
            game.AttachNode(officer, origin);
            SendUnitsAction action = new SendUnitsAction
            {
                UnitInstanceID = officer.InstanceID,
                Destination = new List<GameEventSelector>
                {
                    new SelectFirst
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectPlanets { InstanceID = first.InstanceID },
                            new SelectPlanets { InstanceID = second.InstanceID },
                        },
                    },
                },
            };

            UnitMovementRequest result = action
                .ExecuteRequests(game)
                .OfType<UnitMovementRequest>()
                .Single();

            CollectionAssert.AreEqual(new[] { first, second }, result.Destinations);
        }

        [Test]
        public void SetCaptureStatus_IncompatibleSelector_ThrowsPreciseError()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                IsCaptured = true,
                CaptorFactionInstanceID = "rebels",
                Selectors = new List<GameEventSelector>
                {
                    new SelectPlanets { InstanceID = planet.InstanceID },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("only officers", exception.Message);
        }

        [Test]
        public void SetCaptureStatus_NormalCapture_AllowsEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            };

            OfficerCaptureStateResult result = action
                .Execute(game)
                .OfType<OfficerCaptureStateResult>()
                .Single();

            Assert.IsTrue(officer.IsCaptured);
            Assert.AreEqual("empire", officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
            Assert.AreSame(officer, result.TargetOfficer);
        }

        [Test]
        public void SetCaptureStatus_AuthoredNonEscapingCapture_DisablesEscape()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            game.AttachNode(officer, planet);
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
                CanEscape = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.CanEscape);
        }

        [Test]
        public void SetCaptureStatus_Release_ClearsCaptorAndCaptureOnlyState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            officer.IsEnabled = false;
            SetCaptureStatusAction action = new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            };

            action.Execute(game);

            Assert.IsFalse(officer.IsCaptured);
            Assert.IsNull(officer.CaptorInstanceID);
            Assert.IsTrue(officer.CanEscape);
        }

        [Test]
        public void SetCaptureStatus_RecaptureAfterRelease_RestoresDefaultEscapeState()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", planet.OwnerInstanceID);
            officer.IsCaptured = true;
            officer.CaptorInstanceID = "empire";
            officer.CanEscape = false;
            game.AttachNode(officer, planet);
            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = false,
            }.Execute(game);

            new SetCaptureStatusAction
            {
                OfficerInstanceID = officer.InstanceID,
                IsCaptured = true,
                CaptorFactionInstanceID = "empire",
            }.Execute(game);

            Assert.IsTrue(officer.IsCaptured);
            Assert.IsTrue(officer.CanEscape);
        }

        [Test]
        public void SetDisplayName_CapitalShip_MarksNameAsAssigned()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Generic Ship",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            SetDisplayNameAction action = new SetDisplayNameAction
            {
                TargetInstanceID = ship.InstanceID,
                Name = "Named Ship",
            };

            action.Execute(game);

            Assert.AreEqual("Named Ship", ship.DisplayName);
            Assert.IsTrue(ship.HasAssignedName);
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
                MessageImagePath = "jedi-message",
                EncyclopediaImagePath = "jedi-encyclopedia",
            };

            Assert.IsEmpty(action.Execute(game));

            Assert.AreEqual("jedi-display", luke.DisplayImagePath);
            Assert.AreEqual("jedi-small-display", luke.SmallDisplayImagePath);
            Assert.AreEqual("jedi-message", luke.MessageImagePath);
            Assert.AreEqual("jedi-encyclopedia", luke.EncyclopediaImagePath);
        }

        [Test]
        public void SetOfficerVoiceSet_ConfiguredValues_ReplacesSelectedVoicePools()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.VoiceSet.PersonnelArrivedPaths.Add("old");
            game.AttachNode(luke, rebelPlanet);
            SetOfficerVoiceSetAction action = new SetOfficerVoiceSetAction
            {
                OfficerInstanceID = luke.InstanceID,
                PersonnelArrived = new List<string> { "jedi-arrived" },
            };

            Assert.IsEmpty(action.Execute(game));

            CollectionAssert.AreEqual(
                new[] { "jedi-arrived" },
                luke.VoiceSet.PersonnelArrivedPaths
            );
        }

        [Test]
        public void IncreaseForceRank_PercentOfEffectiveRank_AdjustsForceRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            game.AttachNode(luke, rebelPlanet);
            IncreaseForceRankAction action = new IncreaseForceRankAction
            {
                OfficerInstanceID = luke.InstanceID,
                PercentOfEffective = 25,
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsEmpty(results);
            Assert.AreEqual(50, luke.ForceValue);
        }

        [Test]
        public void ChangeOfficerRating_Amount_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.Diplomacy, 40);
            game.AttachNode(luke, rebelPlanet);

            Assert.IsEmpty(
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = OfficerRating.Diplomacy,
                    Amount = 5,
                }.Execute(game)
            );

            Assert.AreEqual(45, luke.GetBaseRating(OfficerRating.Diplomacy));
        }

        [Test]
        public void ChangeOfficerRating_PercentOfStoredRating_AdjustsStoredRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.ShipResearch, 40);
            game.AttachNode(luke, rebelPlanet);

            new ChangeOfficerRatingAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.ShipResearch,
                PercentOfStored = -25,
            }.Execute(game);

            Assert.AreEqual(30, luke.GetBaseRating(OfficerRating.ShipResearch));
        }

        [Test]
        public void ChangeOfficerRating_MultipleAdjustmentModes_Throws()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);

            Assert.Throws<InvalidOperationException>(() =>
                new ChangeOfficerRatingAction
                {
                    OfficerInstanceID = luke.InstanceID,
                    Rating = OfficerRating.Combat,
                    Amount = 5,
                    PercentOfStored = 10,
                }.Execute(game)
            );
        }

        [Test]
        public void PerformSkillCheck_SuccessfulRoll_ExecutesSuccessActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            luke.IsEnabled = false;
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.Combat,
                ProbabilityTable = MissionTypeIDs.Rescue,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = 1,
                    },
                },
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.59));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("result"));
        }

        [Test]
        public void PerformSkillCheck_FailedRoll_ExecutesFailureActions()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.Combat, 50);
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int> { [50] = 60 };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.Combat,
                ProbabilityTable = MissionTypeIDs.Rescue,
                OnSuccess = new List<GameAction>(),
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction
                    {
                        Key = "result",
                        Operation = EventVariableOperation.Set,
                        Operand = -1,
                    },
                },
            };

            action.Execute(game, new FixedRNG(0.60));

            Assert.AreEqual(-1, game.EventRuntime.GetVariable("result"));
        }

        [Test]
        public void PerformSkillCheck_InjuredOfficer_UsesEffectiveRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.SetBaseRating(OfficerRating.Combat, 50);
            luke.InjuryPoints = 20;
            game.AttachNode(luke, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Rescue = new Dictionary<int, int>
            {
                [0] = 0,
                [50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.Combat,
                ProbabilityTable = MissionTypeIDs.Rescue,
                OnFailure = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failed", Operand = 1 },
                },
            };

            action.Execute(game, new FixedRNG(0.5));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("failed"));
        }

        [Test]
        public void PerformSkillCheck_NegativeRatingMultiplier_UsesScaledScore()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            han.SetBaseRating(OfficerRating.Combat, 50);
            game.AttachNode(han, rebelPlanet);
            game.Config.ProbabilityTables.Mission.Abduction = new Dictionary<int, int>
            {
                [-51] = 0,
                [-50] = 100,
            };
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = han.InstanceID,
                Rating = OfficerRating.Combat,
                ProbabilityTable = MissionTypeIDs.Abduction,
                RatingMultiplier = -1,
                OnSuccess = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "succeeded", Operand = 1 },
                },
            };

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("succeeded"));
            Assert.IsEmpty(results);
        }

        [Test]
        public void PerformSkillCheck_MissingOfficer_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = "missing",
                Rating = OfficerRating.Combat,
                ProbabilityTable = MissionTypeIDs.Rescue,
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve officer", exception.Message);
        }

        [Test]
        public void PerformSkillCheck_MissingProbabilityTable_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            PerformSkillCheckAction action = new PerformSkillCheckAction
            {
                OfficerInstanceID = luke.InstanceID,
                Rating = OfficerRating.Combat,
                ProbabilityTable = "missing",
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                action.Execute(game)
            );

            StringAssert.Contains("could not resolve probability table", exception.Message);
        }

        [Test]
        public void SetForceEligible_EligibilityTransition_InitializesForceOnce()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer leia = EntityFactory.CreateOfficer("leia", "rebels");
            leia.IsForceSensitive = false;
            leia.IsForceEligible = false;
            leia.JediLevel = 10;
            leia.JediLevelVariance = 5;
            game.AttachNode(leia, rebelPlanet);
            SetForceSensitiveAction sensitivity = new SetForceSensitiveAction
            {
                OfficerInstanceID = leia.InstanceID,
            };
            SetForceEligibleAction eligibility = new SetForceEligibleAction
            {
                OfficerInstanceID = leia.InstanceID,
            };

            sensitivity.Execute(game);
            List<GameResult> results = eligibility.Execute(
                game,
                new FixedRandomProvider(new[] { 0.5 })
            );

            Assert.IsTrue(leia.IsForceSensitive);
            Assert.IsTrue(leia.IsForceEligible);
            Assert.AreEqual(13, leia.ForceValue);
            Assert.IsEmpty(results);
            eligibility.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

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
        public void ChangeRawResourceNodes_IncreasesExplicitAmount()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new SequenceRNG(), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            Assert.AreEqual(
                PlanetChangeCategory.RawMaterial,
                results.OfType<PlanetStatChangedResult>().Single().Category
            );
        }

        [Test]
        public void ChangeRawResourceNodes_NeutralPlanet_ReportsNoFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetBinding = "target",
                Amount = 1,
            };
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("target", planet);

            PlanetStatChangedResult result = action
                .Execute(game, new SequenceRNG(), context)
                .OfType<PlanetStatChangedResult>()
                .Single();

            Assert.IsNull(result.Faction);
            Assert.AreEqual(5, planet.NumRawResourceNodes);
        }

        [Test]
        public void ChangeRawResourceNodes_BoundAmount_AppliesReusedInteger()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                null
            );
            context.Bind("change", -2);
            ChangeRawResourceNodesAction action = new ChangeRawResourceNodesAction
            {
                PlanetInstanceID = planet.InstanceID,
                AmountBinding = "change",
            };

            action.Execute(game, new ThrowingRNG(), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
        }

        [Test]
        public void ChangeEnergyCapacity_RolledAmount_AppliesInclusiveIntegerRoll()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.EnergyCapacity = 8;
            ChangeEnergyCapacityAction action = new ChangeEnergyCapacityAction
            {
                PlanetInstanceID = planet.InstanceID,
                RollInteger = new RollInteger { Minimum = -3, Maximum = -1 },
            };

            action.Execute(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(6, planet.EnergyCapacity);
        }

        [Test]
        public void ChangePopularSupport_IncreaseRebalancesOtherFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            ChangePopularSupportAction action = new ChangePopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "empire",
                Amount = 10,
            };

            List<GameResult> results = action.Execute(game);

            Assert.AreEqual(70, planet.GetPopularSupport("empire"));
            Assert.AreEqual(30, planet.GetPopularSupport("rebels"));
            Assert.AreEqual(2, results.OfType<PlanetStatChangedResult>().Count());
            Assert.IsTrue(
                results
                    .OfType<PlanetStatChangedResult>()
                    .All(result => result.Category == PlanetChangeCategory.Loyalty)
            );
        }

        [Test]
        public void SetPopularSupport_AbsoluteValue_PreservesUnallocatedSupport()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.SetPopularSupport("empire", 60);
            planet.SetPopularSupport("rebels", 40);
            SetPopularSupportAction action = new SetPopularSupportAction
            {
                PlanetInstanceID = planet.InstanceID,
                FactionInstanceID = "rebels",
                Support = 20,
            };

            action.Execute(game);

            Assert.AreEqual(60, planet.GetPopularSupport("empire"));
            Assert.AreEqual(20, planet.GetPopularSupport("rebels"));
        }

        [Test]
        public void DamagePlanetResources_MinimumLoss_GuaranteesOnePointLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetBinding = "target",
                LossProbabilityPerResource = 0,
                MinimumTotalLoss = 1,
            };
            GameEvent gameEvent = new GameEvent { InstanceID = "disaster" };
            GameEventEvaluationContext context = new GameEventEvaluationContext(gameEvent, null);
            context.Bind("target", planet);

            List<GameResult> results = action.Execute(game, new FixedRNG(0.99), context);

            Assert.AreEqual(2, planet.NumRawResourceNodes);
            Assert.AreEqual(3, planet.EnergyCapacity);
            Assert.AreEqual(1, results.OfType<PlanetStatChangedResult>().Count());
        }

        [Test]
        public void DamagePlanetResources_NaNProbabilityWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = double.NaN,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void DamagePlanetResources_NegativeMinimumLossWithNoResources_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 0;
            planet.EnergyCapacity = 0;
            DamagePlanetResourcesAction action = new DamagePlanetResourcesAction
            {
                PlanetInstanceID = planet.InstanceID,
                LossProbabilityPerResource = 0.5,
                MinimumTotalLoss = -1,
            };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void RollDouble_ExtremeFiniteRange_ReturnsFiniteValue()
        {
            RollDouble roll = new RollDouble
            {
                Minimum = -double.MaxValue,
                Maximum = double.MaxValue,
            };

            double result = roll.Roll(new FixedRNG(0.5));

            Assert.IsFalse(double.IsNaN(result));
            Assert.IsFalse(double.IsInfinity(result));
            Assert.AreEqual(0, result);
        }

        [Test]
        public void RollChance_RolledProbability_ExecutesActionsOnSuccess()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                RollDouble = new RollDouble { Minimum = 0.7, Maximum = 0.8 },
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "success", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5, 0.6 }));

            Assert.AreEqual(1, game.EventRuntime.GetVariable("success"));
        }

        [Test]
        public void RollChance_NaNProbability_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction { Probability = double.NaN };

            TestDelegate execute = () => action.Execute(game);

            Assert.Throws<InvalidOperationException>(execute);
        }

        [Test]
        public void RollChance_FailedProbability_DoesNotExecuteActions()
        {
            GameRoot game = BuildGame(out _, out _);
            RollChanceAction action = new RollChanceAction
            {
                Probability = 0.25,
                Actions = new List<GameAction>
                {
                    new SetEventVariableAction { Key = "failure", Operand = 1 },
                },
            };

            action.Execute(game, new SequenceRNG(doubleValues: new[] { 0.5 }));

            Assert.Zero(game.EventRuntime.GetVariable("failure"));
        }

        [Test]
        public void RollOutcome_WeightedSelection_ExecutesEveryActionInSelectedOutcome()
        {
            GameRoot game = BuildGame(out _, out _);
            RollOutcomeAction action = new RollOutcomeAction
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

            Assert.Zero(game.EventRuntime.GetVariable("wrong"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(2, game.EventRuntime.GetVariable("second"));
        }

        [Test]
        public void DestroyUnits_SelectedUnit_DeletesUnitFromGame()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                OwnerInstanceID = planet.OwnerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(regiment, planet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectRegiments { InstanceID = regiment.InstanceID },
                },
            };

            action.Execute(game, new FixedRNG(0), null);

            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void DestroyUnits_ParentAndChildSelected_DestroysSubtreeOnce()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = "empire" };
            CapitalShip ship = new CapitalShip { InstanceID = "ship", OwnerInstanceID = "empire" };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            DestroyUnitsAction action = new DestroyUnitsAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectFleets { InstanceID = fleet.InstanceID },
                    new SelectCapitalShips { InstanceID = ship.InstanceID },
                },
            };

            List<GameResult> results = action.Execute(game);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID));
            Assert.IsNull(game.GetSceneNodeByInstanceID<CapitalShip>(ship.InstanceID));
            CollectionAssert.AreEquivalent(
                new ISceneNode[] { fleet, ship },
                results.OfType<GameObjectDestroyedResult>().Select(result => result.DestroyedObject)
            );
        }

        private GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector1" };
            game.AttachNode(sector, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, sector);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, sector);
            return game;
        }
    }
}
