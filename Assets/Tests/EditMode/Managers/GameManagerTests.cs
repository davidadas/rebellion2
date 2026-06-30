using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Research;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerTests
    {
        [Test]
        public void Constructor_WithFactions_RebuildsResearchCatalogs()
        {
            GameRoot game = new GameRoot();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            Assume.That(
                alliance.ResearchCatalog,
                Is.Empty,
                "Catalog must start empty to prove the rebuild populates it"
            );
            Assume.That(empire.ResearchCatalog, Is.Empty);

            _ = new GameManager(game);

            Assert.IsNotEmpty(
                alliance.ResearchCatalog,
                "Alliance research catalog should be rebuilt after GameManager construction"
            );
            Assert.IsNotEmpty(
                empire.ResearchCatalog,
                "Empire research catalog should be rebuilt after GameManager construction"
            );
        }

        [Test]
        public void ProcessTick_EventResults_AddsMessages()
        {
            GameRoot game = new GameRoot();
            Faction faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);
            game.EventPool.Add(
                new GameEvent
                {
                    InstanceID = "EVENT_RESEARCH_EXHAUSTED",
                    Actions = new List<GameAction>
                    {
                        new EmitResultAction(
                            new ResearchExhaustedResult
                            {
                                Faction = faction,
                                Discipline = ResearchDiscipline.ShipDesign,
                            }
                        ),
                    },
                }
            );

            GameManager manager = new GameManager(game);

            manager.ProcessTick();

            Assert.AreEqual(1, faction.Messages[MessageType.Manufacturing].Count);
        }

        private sealed class EmitResultAction : GameAction
        {
            private readonly GameResult _result;

            internal EmitResultAction(GameResult result)
            {
                _result = result;
            }

            public override List<GameResult> Execute(GameRoot game)
            {
                return new List<GameResult> { _result };
            }
        }
    }
}
