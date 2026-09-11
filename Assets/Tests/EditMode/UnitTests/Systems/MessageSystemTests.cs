using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MessageSystemTests
    {
        [Test]
        public void ProcessResults_WithMessageDeliveryRequest_AddsMessageToFaction()
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

            MessageSystem messageSystem = new MessageSystem(
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

        [Test]
        public void HandleRequests_WithCombatReport_DeliversReportAsMessage()
        {
            GameRoot game = new GameRoot(new GameConfig()) { CurrentTick = 42 };
            Faction faction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(faction);
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PlanetName = "Yavin",
            };
            MessageDeliveryRequest request = new MessageDeliveryRequest
            {
                Recipient = faction,
                MessageType = MessageType.Conflict,
                Subject = "Battle at Yavin",
                Body = "Victory",
                Message = report,
            };
            MessageSystem messageSystem = new MessageSystem(game, new List<MessageDefinition>());

            List<GameResult> results = messageSystem.HandleRequests(new[] { request });

            Message deliveredMessage = faction.Messages[MessageType.Conflict].Single();
            Assert.AreSame(report, deliveredMessage);
            Assert.AreSame(report, ((MessageDeliveredResult)results.Single()).Message);
            Assert.AreEqual("Battle at Yavin", report.Title);
            Assert.AreEqual("Victory", report.Body);
            Assert.AreEqual(42, report.CreatedTick);
        }

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
            MessageSystem messageSystem = new MessageSystem(game, new List<MessageDefinition>());

            messageSystem.ProcessResults(new List<GameResult>());

            CollectionAssert.AreEqual(
                new[] { expired, retained },
                faction.Messages[MessageType.Conflict]
            );
        }

        [Test]
        public void ProcessTick_MessagesOlderThanRetention_RemovesExpiredMessages()
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
            MessageSystem messageSystem = new MessageSystem(game, new List<MessageDefinition>());

            messageSystem.ProcessTick();

            CollectionAssert.AreEqual(new[] { retained }, faction.Messages[MessageType.Conflict]);
        }
    }
}
