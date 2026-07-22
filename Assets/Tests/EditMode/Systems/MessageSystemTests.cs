using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MessageSystemTests
    {
        [Test]
        public void ProcessResults_WithMessageDelivery_AddsMessageToFaction()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem { InstanceID = "system", DisplayName = "Yavin" };
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Yavin" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet 1",
                OwnerInstanceID = faction.InstanceID,
            };
            game.AttachNode(fleet, planet);

            MessageSystem messageSystem = new MessageSystem(
                game,
                new[]
                {
                    new MessageDefinition
                    {
                        ResultType = MessageResultType.FleetArrived,
                        MessageType = MessageType.Fleet,
                        TitleTemplate = "{fleet} arrived",
                        BodyTemplate = "{system}",
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
            Assert.AreEqual("Fleet 1 arrived", message.Title);
            Assert.AreEqual("Yavin", message.Body);
            Assert.AreEqual(game.CurrentTick, message.CreatedTick);
        }

        [Test]
        public void ProcessResults_WithoutMatchingDefinition_DoesNotCreateMessageBucket()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "alliance" };
            game.Factions.Add(faction);

            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                DisplayName = "Fleet 1",
                OwnerInstanceID = faction.InstanceID,
            };
            Planet destination = new Planet { InstanceID = "planet" };

            MessageSystem messageSystem = new MessageSystem(game, new List<MessageDefinition>());

            messageSystem.ProcessResults(
                new[]
                {
                    new UnitArrivedResult { Unit = fleet, Destination = destination },
                }
            );

            Assert.IsTrue(faction.Messages.Values.All(messages => messages.Count == 0));
        }

        [Test]
        public void ProcessResults_MessagesOlderThanRetention_RemovesExpiredMessages()
        {
            GameConfig config = TestConfig.Create();
            config.Messages.RetentionTicks = 300;
            GameRoot game = new GameRoot(config) { CurrentTick = 401 };
            Faction faction = new Faction { InstanceID = "alliance" };
            game.Factions.Add(faction);
            Message expired = new Message(MessageType.Conflict, "Expired") { CreatedTick = 100 };
            Message retained = new Message(MessageType.Conflict, "Retained") { CreatedTick = 101 };
            faction.AddMessage(expired);
            faction.AddMessage(retained);
            MessageSystem messageSystem = new MessageSystem(game, new List<MessageDefinition>());

            messageSystem.ProcessResults(new List<GameResult>());

            CollectionAssert.AreEqual(new[] { retained }, faction.Messages[MessageType.Conflict]);
        }
    }
}
