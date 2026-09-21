using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MessageObserverTests
    {
        /// <summary>Verifies that all automatic templates resolve before any earlier message is persisted.</summary>
        [Test]
        public void ProcessResults_LaterTemplateFails_DoesNotDeliverEarlierMessage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            Planet planet = new Planet { InstanceID = "planet" };
            Fleet fleet = new Fleet { InstanceID = "fleet", OwnerInstanceID = faction.InstanceID };
            MessageObserver observer = CreateObserver(
                game,
                new[]
                {
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.FleetArrived,
                        MessageType = MessageType.Fleet,
                        Subject = "Arrived",
                    },
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.ManufacturingIdle,
                        ManufacturingType = ManufacturingType.Ship,
                        MessageType = MessageType.Manufacturing,
                        Subject = "{unknown}",
                    },
                }
            );

            Assert.Throws<InvalidOperationException>(() =>
                observer.ProcessResults(
                    new GameResult[]
                    {
                        new UnitArrivedResult { Unit = fleet, Destination = planet },
                        new ManufacturingIdleResult
                        {
                            Faction = faction,
                            ProductionPlanet = planet,
                            ManufacturingType = ManufacturingType.Ship,
                        },
                    }
                )
            );

            Assert.IsTrue(faction.Messages.Values.All(messages => messages.Count == 0));
        }

        /// <summary>
        /// Verifies that authored provenance suppresses only that result's automatic message.
        /// </summary>
        [Test]
        public void ProcessResults_MixedAuthoredAndAutomaticResults_DeliversOnlyAutomaticMessage()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Destination" };
            Fleet automaticFleet = new Fleet
            {
                InstanceID = "automatic",
                DisplayName = "Automatic",
                OwnerInstanceID = faction.InstanceID,
            };
            Fleet authoredFleet = new Fleet
            {
                InstanceID = "authored",
                DisplayName = "Authored",
                OwnerInstanceID = faction.InstanceID,
            };
            MessageObserver system = CreateObserver(
                game,
                new[]
                {
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.FleetArrived,
                        MessageType = MessageType.Fleet,
                        Subject = "{fleet}",
                        Body = "{system}",
                    },
                }
            );

            List<GameResult> results = system.ProcessResults(
                new GameResult[]
                {
                    null,
                    new UnitArrivedResult
                    {
                        Unit = authoredFleet,
                        Destination = planet,
                        SourceEventInstanceID = "event",
                    },
                    new UnitArrivedResult { Unit = automaticFleet, Destination = planet },
                }
            );

            MessageDeliveredResult delivery = (MessageDeliveredResult)results.Single();
            Assert.AreEqual("Automatic", delivery.Message.Title);
            Assert.AreSame(delivery.Message, faction.Messages[MessageType.Fleet].Single());
        }

        /// <summary>
        /// Verifies with message delivery request adds message to faction.
        /// </summary>
        [Test]
        public void ProcessResults_WithFleetArrival_AddsMessageToFaction()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Sumitra",
            };
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Yavin" };
            game.AttachNode(planetSector, game.Galaxy);
            game.AttachNode(planet, planetSector);

            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet 1",
                OwnerInstanceID = faction.InstanceID,
            };
            game.AttachNode(fleet, planet);

            MessageObserver messageSystem = CreateObserver(
                game,
                new[]
                {
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.FleetArrived,
                        MessageType = MessageType.Fleet,
                        Subject = "{fleet} arrived",
                        Body = "{system}",
                    },
                }
            );

            messageSystem.ProcessResults(
                new[]
                {
                    new UnitArrivedResult { Unit = fleet, Destination = planet },
                }
            );

            Message message = faction.Messages[MessageType.Fleet].Single();
            Assert.IsInstanceOf<StatusMessage>(message);
            Assert.AreEqual("Fleet 1 arrived", message.Title);
            Assert.AreEqual("Yavin", message.Body);
            Assert.AreEqual(game.CurrentTick, message.CreatedTick);
        }

        /// <summary>
        /// Verifies without matching definition does not create message bucket.
        /// </summary>
        [Test]
        public void ProcessResults_WithoutMatchingDefinition_DoesNotCreateMessageBucket()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);

            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet 1",
                OwnerInstanceID = faction.InstanceID,
            };
            Planet destination = new Planet { InstanceID = "planet" };

            MessageObserver messageSystem = CreateObserver(game, new List<MessageDefinition>());

            messageSystem.ProcessResults(
                new[]
                {
                    new UnitArrivedResult { Unit = fleet, Destination = destination },
                }
            );

            Assert.IsTrue(faction.Messages.Values.All(messages => messages.Count == 0));
        }

        /// <summary>
        /// Verifies messages older than retention does not expire messages.
        /// </summary>
        [Test]
        public void ProcessResults_MessagesOlderThanRetention_DoesNotExpireMessages()
        {
            GameConfig config = TestConfig.Create();
            config.Messages.RetentionTicks = 300;
            GameRoot game = new GameRoot(config) { CurrentTick = 401 };
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            Message expired = new StatusMessage(MessageType.Conflict, "Expired")
            {
                CreatedTick = 100,
            };
            Message retained = new StatusMessage(MessageType.Conflict, "Retained")
            {
                CreatedTick = 101,
            };
            faction.AddMessage(expired);
            faction.AddMessage(retained);
            MessageObserver messageSystem = CreateObserver(game, new List<MessageDefinition>());

            messageSystem.ProcessResults(new List<GameResult>());

            CollectionAssert.AreEqual(
                new[] { expired, retained },
                faction.Messages[MessageType.Conflict]
            );
        }

        /// <summary>
        /// Creates message observation with its shared factory and delivery operations.
        /// </summary>
        /// <param name="game">The game containing the recipient factions.</param>
        /// <param name="definitions">The definitions used to resolve automatic messages.</param>
        /// <returns>The observer that selects and delivers messages for result batches.</returns>
        private static MessageObserver CreateObserver(
            GameRoot game,
            IEnumerable<MessageDefinition> definitions
        )
        {
            MessageFactory factory = new MessageFactory(definitions);
            return new MessageObserver(game, factory, new MessageCommands(game, factory));
        }
    }
}
