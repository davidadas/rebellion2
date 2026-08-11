using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTests
    {
        [Test]
        public void Execute_NestedRandomTrigger_PreservesChildSourceEvent()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "rebels" });
            GameEvent child = new GameEvent
            {
                InstanceID = "child",
                Actions = new List<GameAction>
                {
                    new SendMessageAction
                    {
                        RecipientFactionInstanceID = "rebels",
                        Subject = "Child",
                    },
                },
            };
            GameEvent root = new GameEvent
            {
                InstanceID = "root",
                Actions = new List<GameAction>
                {
                    new RandomAction
                    {
                        Outcomes = new List<RandomOutcome>
                        {
                            new RandomOutcome
                            {
                                Weight = 1,
                                Actions = new List<GameAction>
                                {
                                    new TriggerEventAction { EventInstanceID = child.InstanceID },
                                },
                            },
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
    }
}
