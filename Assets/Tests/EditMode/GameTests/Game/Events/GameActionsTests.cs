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
    /// <summary>
    /// Creates complete action contexts for focused action tests without expanding the production API.
    /// </summary>
    internal static class GameActionTestExtensions
    {
        /// <summary>
        /// Executes an action with the game's random-number provider.
        /// </summary>
        internal static List<GameResult> Execute(this GameAction action, GameRoot game)
        {
            GameActionContext context = new GameActionContext(game, game.Random);
            action.Execute(context);
            return context.Results;
        }

        /// <summary>
        /// Executes an action with a caller-supplied random-number provider.
        /// </summary>
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

        /// <summary>
        /// Executes an action with a caller-supplied event activation context.
        /// </summary>
        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random,
            GameEventExecutionContext activation
        )
        {
            GameActionContext context = new GameActionContext(game, random, activation);
            action.Execute(context);
            return context.Results;
        }

        /// <summary>
        /// Executes an action with the unit definitions required to spawn runtime units.
        /// </summary>
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

        /// <summary>
        /// Executes an action and returns the authoritative work it requested.
        /// </summary>
        internal static List<GameRequest> ExecuteRequests(this GameAction action, GameRoot game)
        {
            GameActionContext context = new GameActionContext(game, game.Random);
            action.Execute(context);
            return context.Requests;
        }

        /// <summary>
        /// Executes an action with a caller-supplied event activation and returns its requests.
        /// </summary>
        internal static List<GameRequest> ExecuteRequests(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random,
            GameEventExecutionContext activation
        )
        {
            GameActionContext context = new GameActionContext(game, random, activation);
            action.Execute(context);
            return context.Requests;
        }

        /// <summary>
        /// Executes an action with a unit factory and returns its authoritative requests.
        /// </summary>
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

    /// <summary>
    /// Verifies data-defined action behavior and authored XML contracts.
    /// </summary>
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
                    <SelectBinding Binding=""$participants""/>
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

            Assert.AreEqual("$participants", action.Units.OfType<SelectBinding>().Single().Binding);
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
        public void PlaceUnits_Selectors_RoundTripsTransferStructure()
        {
            PlaceUnitsAction action = new PlaceUnitsAction
            {
                Units = new List<GameEventSelector>
                {
                    new SelectBinding { Binding = "$participants" },
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
                "$participants",
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
        public void AddToVoid_ElementUnitInstanceID_DeserializesUnitInstanceID()
        {
            AddToVoidAction action = (AddToVoidAction)
                SerializationHelper.Deserialize<GameAction>(
                    "<AddToVoid><UnitInstanceID>LUKE_SKYWALKER</UnitInstanceID></AddToVoid>"
                );

            Assert.AreEqual("LUKE_SKYWALKER", action.UnitInstanceID);
        }

        [Test]
        public void AddToVoid_ActiveOfficer_RetainsDetachedOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);

            new AddToVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            Assert.IsNull(luke.GetParent());
            Assert.IsTrue(game.IsInVoid(luke));
        }

        [Test]
        public void RemoveFromVoid_RetainedOfficerSelector_RoundTripsSelector()
        {
            RemoveFromVoidAction action = new RemoveFromVoidAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetBinding = "$destination",
                        IsCaptured = true,
                        IncludeRetained = true,
                    },
                },
            };

            string xml = SerializationHelper.Serialize<GameAction>(action);
            RemoveFromVoidAction restored = (RemoveFromVoidAction)
                SerializationHelper.Deserialize<GameAction>(xml);

            SelectOfficers selector = restored.Selectors.OfType<SelectOfficers>().Single();
            Assert.AreEqual("$destination", selector.PlanetBinding);
            Assert.AreEqual(true, selector.IsCaptured);
            Assert.IsTrue(selector.IncludeRetained);
        }

        [Test]
        public void RemoveFromVoid_OfficerInVoid_DetachesAndPreservesPreviousParent()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, origin);
            new AddToVoidAction { UnitInstanceID = luke.InstanceID }.Execute(game);

            List<GameResult> results = new RemoveFromVoidAction
            {
                UnitInstanceID = luke.InstanceID,
            }.Execute(game);

            Assert.IsEmpty(results);
            Assert.IsNull(luke.GetParent());
            Assert.AreEqual(origin.InstanceID, luke.LastParentInstanceID);
        }

        [Test]
        public void RemoveFromVoid_KilledOfficer_ThrowsInvalidOperationException()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer officer = EntityFactory.CreateOfficer("officer", "rebels");
            game.AttachNode(officer, origin);
            new PersonnelSystem(game).KillOfficer(officer);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                new RemoveFromVoidAction { UnitInstanceID = officer.InstanceID }.Execute(game)
            );

            StringAssert.Contains("cannot restore killed officer", exception.Message);
            Assert.IsTrue(game.IsInVoid(officer));
        }

        [Test]
        public void RemoveFromVoid_RetainedOfficerSelector_DetachesMatchingOfficer()
        {
            GameRoot game = BuildGame(out _, out Planet origin);
            Officer han = EntityFactory.CreateOfficer("han", "rebels");
            han.IsCaptured = true;
            game.AttachNode(han, origin);
            game.AddToVoid(han);
            RemoveFromVoidAction action = new RemoveFromVoidAction
            {
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers
                    {
                        PlanetInstanceID = origin.InstanceID,
                        IsCaptured = true,
                        IncludeRetained = true,
                    },
                },
            };

            action.Execute(game);

            Assert.IsNull(han.GetParent());
            Assert.AreEqual(origin.InstanceID, han.LastParentInstanceID);
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
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
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
        public void RevealToFaction_SelectedOfficer_EmitsConcreteObservation()
        {
            GameRoot game = BuildGame(out Planet empirePlanet, out _);
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, empirePlanet);
            RevealToFactionAction action = new RevealToFactionAction
            {
                FactionInstanceID = "rebels",
                Selectors = new List<GameEventSelector>
                {
                    new SelectOfficers { InstanceID = officer.InstanceID },
                },
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent { InstanceID = "INFORMANTS" },
                new GameEventState(),
                empirePlanet
            );

            List<GameResult> results = action.Execute(game, game.Random, context);

            IntelligenceRevealedResult intelligence = results
                .OfType<IntelligenceRevealedResult>()
                .Single();
            Assert.AreEqual("rebels", intelligence.Recipient.InstanceID);
            CollectionAssert.AreEqual(new[] { officer }, intelligence.Observations);
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
        public void SendMessage_ConditionalBodies_ComposesFromOfficerState()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.InjuryPoints = 12;
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                Body = "Luke learned the truth. ",
                ConditionalBodies = new List<ConditionalMessageBody>
                {
                    new ConditionalMessageBody
                    {
                        Conditions = new List<GameConditional>
                        {
                            new IsInjuredConditional { OfficerInstanceID = luke.InstanceID },
                        },
                        Body = "Luke was injured.",
                        ElseBody = "Luke escaped unharmed.",
                    },
                },
            };

            MessageDeliveryRequest result = action
                .ExecuteRequests(game)
                .OfType<MessageDeliveryRequest>()
                .Single();

            Assert.AreEqual("Luke learned the truth. Luke was injured.", result.Body);
        }

        [Test]
        public void SendMessage_AudioBinding_UsesTriggerBindingPath()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            game.AttachNode(luke, rebelPlanet);
            SendMessageAction action = new SendMessageAction
            {
                SubjectInstanceID = luke.InstanceID,
                BackgroundAudio = new MessageAudio { Binding = "$audioPath" },
            };
            DuelResult encounter = new DuelResult
            {
                EncounteredOfficer = luke,
                AudioPath = "selected-encounter-voice",
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                encounter,
                new GameEventTrigger("core:duel.completed", ("AudioPath", "audioPath"))
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
                Conditions = new List<GameConditional>
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
        public void IncreaseOfficerForce_PercentOfEffectiveRank_AdjustsForceRating()
        {
            GameRoot game = BuildGame(out _, out Planet rebelPlanet);
            Officer luke = EntityFactory.CreateOfficer("luke", "rebels");
            luke.ForceValue = 40;
            game.AttachNode(luke, rebelPlanet);
            IncreaseOfficerForceAction action = new IncreaseOfficerForceAction
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
        public void ChangePlanetStat_RawResourceNodes_IncreasesExplicitAmount()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangePlanetStatAction action = new ChangePlanetStatAction
            {
                Stat = PlanetStat.RawResourceNodes,
                Amount = 1,
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                null,
                planet
            );

            List<GameResult> results = action.Execute(game, new SequenceRNG(), context);

            Assert.AreEqual(5, planet.NumRawResourceNodes);
            Assert.AreEqual(
                PlanetChangeCategory.RawMaterial,
                results.OfType<PlanetStatChangedResult>().Single().Category
            );
        }

        [Test]
        public void ChangePlanetStat_NeutralPlanet_ReportsNoFaction()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.OwnerInstanceID = null;
            planet.NumRawResourceNodes = 4;
            planet.EnergyCapacity = 8;
            ChangePlanetStatAction action = new ChangePlanetStatAction
            {
                Stat = PlanetStat.RawResourceNodes,
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
        public void ReducePlanetStats_MinimumLoss_GuaranteesOnePointLoss()
        {
            GameRoot game = BuildGame(out Planet planet, out _);
            planet.NumRawResourceNodes = 3;
            planet.EnergyCapacity = 3;
            ReducePlanetStatsAction action = new ReducePlanetStatsAction
            {
                LossProbabilityPerResource = 0,
                MinimumTotalLoss = 1,
                Stats = new List<PlanetStatReference>
                {
                    new PlanetStatReference { Stat = PlanetStat.RawResourceNodes },
                    new PlanetStatReference { Stat = PlanetStat.EnergyCapacity },
                },
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
        }

        [Test]
        public void RecordPlanetIncident_PriorDestroyedBuilding_IncludesFacility()
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
                    new ReducePlanetStatsAction
                    {
                        LossProbabilityPerResource = 0,
                        MinimumTotalLoss = 1,
                        Stats = new List<PlanetStatReference>
                        {
                            new PlanetStatReference { Stat = PlanetStat.RawResourceNodes },
                            new PlanetStatReference { Stat = PlanetStat.EnergyCapacity },
                        },
                    },
                    new DestroyUnitsAction
                    {
                        Selectors = new List<GameEventSelector>
                        {
                            new SelectRandom
                            {
                                ChancePercent = 100,
                                Selectors = new List<GameEventSelector>
                                {
                                    new SelectBuildings
                                    {
                                        PlanetInstanceID = planet.InstanceID,
                                        Category = BuildingSelectionCategory.ManufacturingFacility,
                                    },
                                },
                            },
                        },
                    },
                    new RecordPlanetIncidentAction { IncidentType = PlanetIncidentType.Disaster },
                },
            };
            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                null,
                planet
            );

            List<GameResult> results = gameEvent.Execute(game, new FixedRNG(0.99), context).Results;

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

            Assert.Zero(game.EventRuntime.GetVariable("wrong"));
            Assert.AreEqual(1, game.EventRuntime.GetVariable("first"));
            Assert.AreEqual(2, game.EventRuntime.GetVariable("second"));
        }

        /// <summary>
        /// Creates a two-faction game with one colonized planet owned by each faction.
        /// </summary>
        private GameRoot BuildGame(out Planet empirePlanet, out Planet rebelPlanet)
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            PlanetSector system = new PlanetSector { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(empirePlanet, system);
            rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(rebelPlanet, system);
            return game;
        }
    }
}
